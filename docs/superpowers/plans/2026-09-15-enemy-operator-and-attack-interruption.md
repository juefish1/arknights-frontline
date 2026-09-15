# 敌方干员表现与攻击中断 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将 `TrainingDummy_Red` 改为与我方同尺寸、带血条和死亡尸体的敌方干员，并确保交战目标离开射程时销毁手动攻击指令且长帧不会补发大量子弹。

**Architecture:** `PlayerCommandController` 提供原子化的指令取消入口和递增修订号，`CombatCommandResolver` 用修订号区分每次手动指令并记录“接敌/交战”状态，`BasicAttackController` 只执行当前目标的单次节奏推进且每帧最多发射一次。场景构建器继续作为敌方干员表现的唯一来源，复用现有 `HealthBarPresenter` 与 `DeathCorpsePresenter`。

**Tech Stack:** Unity 6000.3.15f1、C#、Unity Test Framework、NUnit、Unity Input System。

## Global Constraints

- `TrainingDummy_Red` 使用 `PrimitiveType.Capsule`，局部缩放为 `(1.6f, 2f, 1.6f)`，世界位置为 `(20f, 2f, 0f)`。
- 敌方干员保持红方、地面、1000 生命、0 攻击、2 防御、0 攻击范围、0 攻击间隔、不能攻击地面或空中。
- 敌方干员始终显示血条；生命归零后生成红色平面尸体并销毁活体与血条。
- 指定目标在首次进入射程前继续追击，首次进入射程立即攻击。
- 已交战目标离开射程、死亡或失效时，必须清除整条攻击指令；重新进入射程不得自动恢复。
- `AttackNearestInRange` 是一次性选择，不得保留为持续自动索敌状态。
- 已经生成的投射物不因攻击指令中断而取消。
- `BasicAttackController.Tick(float)` 每次调用最多产生一个攻击请求，并丢弃超过一个攻击间隔的积压时间。
- 不修改小兵、塔、投射物伤害规则、自动攻击模式或敌方干员 AI。
- 保留工作区中与本任务无关的 Packages、ProjectSettings、日志和既有未提交修改。

---

### Task 1: 增加原子化指令取消入口

**Files:**

- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs:27-108`
- Modify: `Assets/Tests/EditMode/PlayerCommandControllerTests.cs:12-117`

**Interfaces:**

- Consumes: 既有 `PlayerCommandController.Issue(UnitCommand)`、`UnitMotor.Stop()`。
- Produces: `PlayerCommandController.CommandRevision : int`、`PlayerCommandController.CancelCurrentCommand() : void`。

- [ ] **Step 1: 写入失败的取消行为测试**

在 `PlayerCommandControllerTests` 中加入：

```csharp
[Test]
public void CancelCurrentCommandClearsCommandTargetAndMovement()
{
    PlayerCommandController controller = CreateController(out UnitMotor motor, out GameObject player);
    GameObject target = new GameObject("Target");
    controller.Issue(UnitCommand.Attack(target));
    int revisionAfterIssue = controller.CommandRevision;
    motor.SetDestination(new Vector3(10f, 0f, 0f));

    controller.CancelCurrentCommand();

    Assert.That(controller.CurrentCommand, Is.Null);
    Assert.That(controller.CurrentTarget, Is.Null);
    Assert.That(motor.IsMoving, Is.False);
    Assert.That(controller.CommandRevision, Is.GreaterThan(revisionAfterIssue));
    Object.DestroyImmediate(target);
    Object.DestroyImmediate(player);
}

[Test]
public void IssuingSameCommandAgainAdvancesCommandRevision()
{
    PlayerCommandController controller = CreateController(out _, out GameObject player);
    GameObject target = new GameObject("Target");
    controller.Issue(UnitCommand.Attack(target));
    int firstRevision = controller.CommandRevision;

    controller.Issue(UnitCommand.Attack(target));

    Assert.That(controller.CommandRevision, Is.GreaterThan(firstRevision));
    Object.DestroyImmediate(target);
    Object.DestroyImmediate(player);
}
```

这两个测试捕获的回归分别是：取消后仍残留命令/移动状态，以及对同一目标重发手动命令无法被解析器识别为新指令。

- [ ] **Step 2: 运行测试确认 RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.PlayerCommandControllerTests -testResults TestResults/attack-interruption-task1-red.xml -logFile TestResults/attack-interruption-task1-red.log -quit
```

Expected: 编译失败，指出 `CommandRevision` 和 `CancelCurrentCommand` 不存在。

- [ ] **Step 3: 实现最小取消 API**

在 `PlayerCommandController` 的公开属性旁加入：

```csharp
public int CommandRevision { get; private set; }
```

在 `Issue` 写入 `CurrentCommand` 后递增修订号：

```csharp
CurrentCommand = command;
CommandRevision++;
```

在 `Issue` 方法后加入：

```csharp
public void CancelCurrentCommand()
{
    CurrentCommand = null;
    currentTarget = null;
    motor.Stop();
    CommandRevision++;
}
```

不要通过 `Issue(UnitCommand.Stop())` 取消，因为 `Stop` 仍是一个持久的非空命令，不满足“销毁本次攻击指令”。

- [ ] **Step 4: 运行测试确认 GREEN**

重复 Step 2 命令，将输出文件名改为 `attack-interruption-task1-green`。

Expected: `PlayerCommandControllerTests` 全部 PASS，原有 Move/Attack/Stop 切换行为保持正常。

- [ ] **Step 5: 提交 Task 1**

```bash
git add Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/EditMode/PlayerCommandControllerTests.cs
git commit -m "feat: add player command cancellation"
```

---

### Task 2: 将手动攻击解析为一次性接敌/交战状态

**Files:**

- Modify: `Assets/Game/Scripts/Combat/CombatCommandResolver.cs:10-169`
- Modify: `Assets/Tests/EditMode/CombatCommandResolverTests.cs:12-175`

**Interfaces:**

- Consumes: Task 1 `PlayerCommandController.CommandRevision`、`CancelCurrentCommand()`，既有 `TargetSelector.FindNearestInRange(CombatUnit)`、`BasicAttackController.SetTarget(CombatUnit)`。
- Produces: 指定目标的接敌/交战中断规则，以及一次性 `AttackNearestInRange` 规则；不新增公开战斗 API。

- [ ] **Step 1: 写入失败的指定目标中断测试**

在 `CombatCommandResolverTests` 中加入：

```csharp
[Test]
public void EngagedSpecifiedTargetLeavingRangeCancelsCommandAndDoesNotReacquire()
{
    CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
        out CombatCommandResolver resolver, out BasicAttackController attack);
    CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
    int requestCount = 0;
    attack.AttackRequested += (_, _) => requestCount++;
    controller.Issue(UnitCommand.Attack(target.gameObject));
    resolver.Tick(0f);
    attack.Tick(0f);
    Assert.That(requestCount, Is.EqualTo(1));

    target.transform.position = new Vector3(8f, 0f, 0f);
    resolver.Tick(0f);

    Assert.That(controller.CurrentCommand, Is.Null);
    Assert.That(controller.CurrentTarget, Is.Null);
    Assert.That(resolver.CurrentTarget, Is.Null);
    Assert.That(attack.CurrentTarget, Is.Null);
    Assert.That(motor.IsMoving, Is.False);

    target.transform.position = new Vector3(2f, 0f, 0f);
    resolver.Tick(10f);
    attack.Tick(10f);
    Assert.That(requestCount, Is.EqualTo(1));

    controller.Issue(UnitCommand.Attack(target.gameObject));
    resolver.Tick(0f);
    attack.Tick(0f);
    Assert.That(requestCount, Is.EqualTo(2));
}
```

生产代码若继续保留当前攻击命令、重新追击或自动恢复目标，该测试必须失败。

- [ ] **Step 2: 写入失败的一次性最近目标测试**

把现有 `NearestIntentNeverSetsDestinationWhenNoLegalTargetIsInRange` 追加断言：

```csharp
Assert.That(controller.CurrentCommand, Is.Null);
```

再加入：

```csharp
[Test]
public void NearestIntentDoesNotRetargetAfterEngagedTargetLeavesRange()
{
    CreatePlayer(out _, out UnitMotor motor, out PlayerCommandController controller,
        out CombatCommandResolver resolver, out BasicAttackController attack);
    CombatUnit first = CreateUnit("First", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, false);
    controller.Issue(UnitCommand.AttackNearestInRange());
    resolver.Tick(0f);
    Assert.That(resolver.CurrentTarget, Is.EqualTo(first));

    first.transform.position = new Vector3(8f, 0f, 0f);
    CreateUnit("Replacement", TeamId.Red, new Vector3(1f, 0f, 0f), 1f, false);
    resolver.Tick(0f);

    Assert.That(controller.CurrentCommand, Is.Null);
    Assert.That(resolver.CurrentTarget, Is.Null);
    Assert.That(attack.CurrentTarget, Is.Null);
    Assert.That(motor.IsMoving, Is.False);
}
```

该测试确保一次指令选中的目标离开后不会在同一条指令中切换到后来出现的目标。

- [ ] **Step 3: 运行 Task 2 测试确认 RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.CombatCommandResolverTests -testResults TestResults/attack-interruption-task2-red.xml -logFile TestResults/attack-interruption-task2-red.log -quit
```

Expected: FAIL；现实现会在目标离开后保留攻击命令并重新追击，且无目标的 `AttackNearestInRange` 不会清除命令。

- [ ] **Step 4: 实现接敌/交战状态与命令取消**

在 `CombatCommandResolver` 增加：

```csharp
private int observedCommandRevision = -1;
private bool hasEngagedCurrentCommand;
```

在读取到非空 `command` 后、进入 `switch` 前同步每次新指令：

```csharp
if (observedCommandRevision != commandSource.CommandRevision)
{
    observedCommandRevision = commandSource.CommandRevision;
    hasEngagedCurrentCommand = false;
    ClearCombatTarget();
}
```

将 `ResolveSpecifiedTarget` 改为：

```csharp
private void ResolveSpecifiedTarget(GameObject targetObject)
{
    CombatUnit target = targetObject == null ? null : targetObject.GetComponent<CombatUnit>();
    if (!TargetRules.IsLegal(owner, target))
    {
        CancelCurrentCommand();
        return;
    }

    if (!IsInAttackRange(target))
    {
        if (hasEngagedCurrentCommand)
        {
            CancelCurrentCommand();
            return;
        }

        ClearCombatTarget();
        motor.SetDestination(target.transform.position);
        return;
    }

    EngageTarget(target);
}
```

将 `ResolveNearestTarget` 改为只在尚未交战时选择一次目标：

```csharp
private void ResolveNearestTarget()
{
    if (hasEngagedCurrentCommand)
    {
        if (!TargetRules.IsLegal(owner, CurrentTarget) || !IsInAttackRange(CurrentTarget))
        {
            CancelCurrentCommand();
            return;
        }

        EngageTarget(CurrentTarget);
        return;
    }

    CombatUnit target = TargetSelector.FindNearestInRange(owner);
    if (target == null)
    {
        CancelCurrentCommand();
        return;
    }

    EngageTarget(target);
}
```

加入以下私有方法：

```csharp
private void EngageTarget(CombatUnit target)
{
    motor.Stop();
    CurrentTarget = target;
    attackController.SetTarget(target);
    hasEngagedCurrentCommand = true;
}

private void CancelCurrentCommand()
{
    motor.Stop();
    ClearCombatTarget();
    commandSource.CancelCurrentCommand();
    observedCommandRevision = commandSource.CommandRevision;
    hasEngagedCurrentCommand = false;
}
```

在 `CurrentCommand` 为空、玩家死亡以及 Move/Stop 分支中将 `hasEngagedCurrentCommand = false`。死亡分支仍不需要生成 Stop 命令。

- [ ] **Step 5: 运行 Task 2 测试确认 GREEN**

重复 Step 3 命令，将输出文件名改为 `attack-interruption-task2-green`。

Expected: `CombatCommandResolverTests` 全部 PASS，包括原有“首次进入范围立即攻击”和“追击时刷新目标位置”。

- [ ] **Step 6: 提交 Task 2**

```bash
git add Assets/Game/Scripts/Combat/CombatCommandResolver.cs Assets/Tests/EditMode/CombatCommandResolverTests.cs
git commit -m "fix: cancel interrupted manual attacks"
```

---

### Task 3: 禁止攻击计时补发积压子弹

**Files:**

- Modify: `Assets/Game/Scripts/Combat/BasicAttackController.cs:84-115`
- Modify: `Assets/Tests/EditMode/BasicAttackControllerTests.cs:12-185`

**Interfaces:**

- Consumes: 既有 `BasicAttackController.Tick(float)` 与 `CombatUnit.AttackInterval`。
- Produces: 每次 `Tick` 最多一个 `AttackRequested`，触发后从零开始新的完整间隔。

- [ ] **Step 1: 将补发测试改为失败的单发节奏测试**

用以下测试替换 `AttackTimerEmitsEveryPositiveIntervalCrossedByOneTick`：

```csharp
[Test]
public void LongTickEmitsAtMostOneRequestAndRestartsFullInterval()
{
    CombatUnit player = CreateUnit("Player", TeamId.Blue, Vector3.zero, 5f, 0.5f, true);
    CombatUnit target = CreateUnit("Target", TeamId.Red, new Vector3(2f, 0f, 0f), 1f, 1f, false);
    BasicAttackController attack = player.gameObject.AddComponent<BasicAttackController>();
    attack.Configure(player);
    int requestCount = 0;
    attack.AttackRequested += (_, _) => requestCount++;

    attack.SetTarget(target);
    attack.Tick(0f);
    attack.Tick(1.2f);
    Assert.That(requestCount, Is.EqualTo(2));

    attack.Tick(0.49f);
    Assert.That(requestCount, Is.EqualTo(2));
    attack.Tick(0.01f);
    Assert.That(requestCount, Is.EqualTo(3));
}
```

若恢复 `while` 补发或保留 `1.2 - 0.5` 的剩余积压，断言会失败。

- [ ] **Step 2: 运行测试确认 RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.BasicAttackControllerTests -testResults TestResults/attack-cadence-task3-red.xml -logFile TestResults/attack-cadence-task3-red.log -quit
```

Expected: FAIL；长 `Tick(1.2f)` 会把请求数增加到 3，而期望为 2。

- [ ] **Step 3: 用单次判断替换补发循环**

将正攻击间隔分支改为：

```csharp
elapsedSinceAttack += Mathf.Max(0f, deltaTime);
if (elapsedSinceAttack < interval)
{
    return;
}

elapsedSinceAttack = 0f;
RequestAttack();
```

保留首次锁定立即攻击和零间隔每次 `Tick` 最多一次的现有分支。

- [ ] **Step 4: 运行测试确认 GREEN**

重复 Step 2 命令，将输出文件名改为 `attack-cadence-task3-green`。

Expected: `BasicAttackControllerTests` 全部 PASS，长帧、目标死亡和零间隔路径均最多发出预期数量的请求。

- [ ] **Step 5: 提交 Task 3**

```bash
git add Assets/Game/Scripts/Combat/BasicAttackController.cs Assets/Tests/EditMode/BasicAttackControllerTests.cs
git commit -m "fix: cap attacks to one request per tick"
```

---

### Task 4: 将训练目标升级为敌方干员并同步场景

**Files:**

- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:168-179`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs:31-129`
- Modify: `Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs:40-116`
- Modify: `Assets/Game/Scenes/PrototypeArena.unity`（仅由场景构建器重建生成）

**Interfaces:**

- Consumes: `CombatUnit.Configure(...)`、`HealthBarPresenter`、`DeathCorpsePresenter.Configure(CombatUnit, Material, int)`。
- Produces: 保存场景内同尺寸、带血条和死亡表现的 `TrainingDummy_Red`。

- [ ] **Step 1: 写入失败的场景结构测试**

在 `PrototypeArenaContainsRequiredRoots` 的玩家断言后加入：

```csharp
GameObject enemyOperator = GameObject.Find("TrainingDummy_Red");
Assert.That(enemyOperator, Is.Not.Null);
Assert.That(enemyOperator.transform.localScale, Is.EqualTo(player.transform.localScale));
Assert.That(enemyOperator.transform.position.y, Is.EqualTo(player.transform.position.y));
Assert.That(enemyOperator.GetComponent<CapsuleCollider>(), Is.Not.Null);
Assert.That(enemyOperator.GetComponent<HealthBarPresenter>(), Is.Not.Null);
Assert.That(enemyOperator.GetComponent<DeathCorpsePresenter>(), Is.Not.Null);
Assert.That(enemyOperator.transform.Find("HealthBar"), Is.Not.Null);
```

此测试对当前立方体、缩放 `(1,1,1)`、无血条和无死亡组件的保存场景失败。

- [ ] **Step 2: 写入失败的保存场景死亡测试**

在 `ArenaSceneSmokeTests` 中加入：

```csharp
[UnityTest]
public IEnumerator SavedEnemyOperatorDeathCreatesGroundedCorpse()
{
    SceneManager.LoadScene("PrototypeArena");
    yield return null;

    GameObject enemyOperator = GameObject.Find("TrainingDummy_Red");
    CombatUnit unit = enemyOperator.GetComponent<CombatUnit>();
    Material material = enemyOperator.GetComponent<Renderer>().sharedMaterial;
    Vector3 position = enemyOperator.transform.position;

    unit.TakePhysicalDamage(unit.MaxHealth);
    yield return null;

    Assert.That(enemyOperator == null, Is.True);
    GameObject corpse = GameObject.Find("TrainingDummy_Red_Corpse");
    Assert.That(corpse, Is.Not.Null);
    Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
    Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(position.x, 0.01f, position.z)));
    Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
    Assert.That(corpse.transform.Find("HealthBar"), Is.Null);
    Collider collider = corpse.GetComponent<Collider>();
    Assert.That(collider == null || !collider.enabled, Is.True);
}
```

同时将 `ConfiguredPlayerAndMinionsCreateCorpsesWhileTowerAndTrainingTargetDoNot` 重命名为 `ConfiguredPlayerAndMinionsCreateCorpsesWhileTowersDoNot`，删除它临时创建 `TrainingDummy_Red` 及断言该对象没有 `DeathCorpsePresenter` 的代码；保留两座塔死亡不生成尸体的断言。

- [ ] **Step 3: 运行场景测试确认 RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests" -testResults TestResults/enemy-operator-task4-red.xml -logFile TestResults/enemy-operator-task4-red.log -quit
```

Expected: FAIL；保存场景的敌方干员尺寸、碰撞体类型、血条和尸体组件不符合断言。

- [ ] **Step 4: 修改场景构建器**

将 `CreateTrainingDummy` 的 `groundLayer` 作为第四个参数从 `Build` 传入：

```csharp
CreateTrainingDummy(arenaRoot.transform, redMaterial, targetableLayer, groundLayer);
```

并将方法替换为：

```csharp
private static void CreateTrainingDummy(
    Transform parent,
    Material material,
    int targetableLayer,
    int groundLayer)
{
    GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    dummy.name = "TrainingDummy_Red";
    dummy.transform.SetParent(parent, false);
    dummy.transform.localScale = new Vector3(1.6f, 2f, 1.6f);
    dummy.transform.position = new Vector3(20f, 2f, 0f);
    dummy.layer = targetableLayer;
    dummy.GetComponent<Renderer>().sharedMaterial = material;

    CombatUnit combatUnit = dummy.AddComponent<CombatUnit>();
    combatUnit.Configure(TeamId.Red, Altitude.Ground, 1000f, 0f, 2f, 0f, 0f, false, false);
    dummy.AddComponent<HealthBarPresenter>();
    DeathCorpsePresenter presenter = dummy.AddComponent<DeathCorpsePresenter>();
    presenter.Configure(combatUnit, material, groundLayer);
}
```

- [ ] **Step 5: 重建场景并运行 GREEN**

确保 Unity 编辑器未打开该项目，然后运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -executeMethod ArknightsFrontline.Editor.PrototypeSceneBuilder.Build -logFile TestResults/enemy-operator-build.log -quit
```

确认退出码为 0 后，重复 Step 3 的 PlayMode 命令，将结果文件名改为 `enemy-operator-task4-green`。

Expected: 构建命令退出码 0；两组 PlayMode 测试全部 PASS；`PrototypeArena.unity` 中敌方干员的序列化 `DeathCorpsePresenter` 保留红方材质与 Ground 层引用。

- [ ] **Step 6: 提交 Task 4**

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Game/Scenes/PrototypeArena.unity Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git commit -m "feat: present training target as enemy operator"
```

---

### Task 5: 完整回归与人工验收

**Files:**

- Verify only: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Verify only: `Assets/Game/Scripts/Combat/CombatCommandResolver.cs`
- Verify only: `Assets/Game/Scripts/Combat/BasicAttackController.cs`
- Verify only: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Verify only: `Assets/Game/Scenes/PrototypeArena.unity`

**Interfaces:**

- Consumes: Tasks 1–4 的全部行为。
- Produces: 可交付的自动测试结果与人工验收步骤。

- [ ] **Step 1: 运行完整 EditMode**

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults TestResults/enemy-operator-attack-final-editmode.xml -logFile TestResults/enemy-operator-attack-final-editmode.log -quit
```

Expected: 全部 EditMode 测试 PASS，退出码 0。

- [ ] **Step 2: 运行完整 PlayMode**

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults TestResults/enemy-operator-attack-final-playmode.xml -logFile TestResults/enemy-operator-attack-final-playmode.log -quit
```

Expected: 全部 PlayMode 测试 PASS，退出码 0。

- [ ] **Step 3: 检查改动边界**

```bash
git diff --check
git status --short
git diff -- Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Game/Scripts/Combat/CombatCommandResolver.cs Assets/Game/Scripts/Combat/BasicAttackController.cs Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/EditMode/PlayerCommandControllerTests.cs Assets/Tests/EditMode/CombatCommandResolverTests.cs Assets/Tests/EditMode/BasicAttackControllerTests.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
```

Expected: 本任务代码无空白错误；未把 Packages、ProjectSettings、日志和其他既有工作区修改加入任务提交。

- [ ] **Step 4: 人工验收**

1. 打开 `PrototypeArena`，确认 `TrainingDummy_Red` 是与蓝色干员同尺寸的红色胶囊，头顶血条可见。
2. 右键一个射程外目标，确认蓝色干员追击并在首次进入射程时立即攻击。
3. 让已交战目标离开射程，确认蓝色干员停止追击且不再发射子弹。
4. 让原目标重新进入射程并等待数秒，确认不会自动恢复；再次右键目标后应立即重新攻击。
5. 使用 `A + 左键` 点击无合法目标的地面，随后让目标进入射程，确认不会自动攻击。
6. 将 `TrainingDummy_Red` 的生命清空，确认活体和血条消失，只留下红色平面尸体。

Expected: 六项均符合设计，无瞬间连发。

## Plan self-review

- Spec coverage: Tasks 1–4 覆盖敌方干员尺寸、血条、死亡表现、接敌/交战中断、一次性最近目标和长帧单发；Task 5 覆盖完整自动与人工验收。
- Placeholder scan: 无 `TBD`、`TODO`、“类似前文”或未定义实现步骤。
- Type consistency: `CommandRevision` 与 `CancelCurrentCommand()` 在 Task 1 产生并由 Task 2 精确消费；其他任务仅使用仓库现有公开接口。
- Scope: 未引入自动攻击、敌方 AI、投射物取消或无关重构。
