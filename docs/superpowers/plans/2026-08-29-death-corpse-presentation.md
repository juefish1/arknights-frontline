# 死亡尸体平面表现 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** 为干员、地面小兵和空中小兵生成独立的同色平面尸体；空中尸体在 0.3 秒内落地，不改变既有战斗规则。

**Architecture:** DeathCorpsePresenter 订阅 CombatUnit.Died、创建不带 CombatUnit 的尸体并销毁源单位。CorpseFallController 只负责空中尸体的确定性下落，暴露 Tick(float) 供测试；场景构建器与波次生成器是唯一接入点。

**Tech Stack:** Unity 6000.3.15f1、URP、Unity Test Framework、NUnit、C# 9 花括号命名空间、macOS。

## Global Constraints

- 权威规格：docs/superpowers/specs/2026-08-29-death-corpse-presentation-design.md。
- 只覆盖玩家干员、地面小兵、空中小兵；不覆盖塔、训练目标、尸体碰撞/阻挡、对象池和尸体回收。
- 尸体不得有 CombatUnit、Targetable 层或启用的 primitive collider，不能成为攻击或鼠标命令目标。
- 空中尸体在 0.3f 秒内线性落到 Ground 层上方 0.01f；向下射线未命中时回退 Y = 0f。
- 不修改右键、A+左键、停止、目标选择、波次、塔结算或胜负规则。
- 不暂存用户已有的场景、Packages、ProjectSettings、日志和其他未提交文件；除非用户再次明确要求，不重建场景。
- 若 Unity 在测试发现前仍因 LicenseClient 505 / Unsupported protocol version '1.18.1' 阻断，记录为环境阻塞，绝不宣称 Unity 测试通过；继续执行静态编译和差异检查。

---

## File Structure

| 路径 | 职责 |
| --- | --- |
| Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs | 订阅死亡、生成尸体、隐藏并销毁源单位。 |
| Assets/Game/Scripts/Combat/CorpseFallController.cs | 以固定时长让空中尸体落到地面。 |
| Assets/Game/Scripts/Arena/MinionWaveSpawner.cs | 为地面与空中小兵配置尸体组件。 |
| Assets/Game/Editor/PrototypeSceneBuilder.cs | 为玩家干员配置尸体组件并传入 Ground 层。 |
| Assets/Tests/EditMode/DeathCorpsePresenterTests.cs | 确定性验证尸体生成、下落和目标排除。 |
| Assets/Tests/EditMode/MinionWaveSpawnerTests.cs | 验证所有小兵获得尸体组件。 |
| Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs | 验证真实帧中的源对象销毁与空中下落。 |

### Task 1: 独立尸体生成与下落组件

**Files:**

- Create: Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs
- Create: Assets/Game/Scripts/Combat/CorpseFallController.cs
- Create: Assets/Tests/EditMode/DeathCorpsePresenterTests.cs

**Interfaces:**

- Consumes: CombatUnit, Altitude, Material, Unity Renderer、Collider、Physics.Raycast。
- Produces: DeathCorpsePresenter.Configure(CombatUnit combatUnit, Material corpseMaterial, int groundLayer); CorpseFallController.Configure(Vector3 landingPosition, float duration); CorpseFallController.Tick(float deltaTime); bool CorpseFallController.HasLanded { get; }.

- [ ] **Step 1: Write the failing EditMode tests**

新建 DeathCorpsePresenterTests。每个测试都创建 Ground 层编号 8 的 Plane、带 CombatUnit 的源单位、材质与 presenter；TearDown 以 Object.DestroyImmediate 清理测试对象和材质。

~~~csharp
[Test]
public void GroundUnitDeathCreatesSameMaterialCorpseAtGroundOffset()
{
    CombatUnit unit = CreateUnit("BlueGround", Altitude.Ground, new Vector3(3f, 1f, -2f));
    Material material = CreateMaterial(Color.blue);
    unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, material, GroundLayer);

    unit.TakePhysicalDamage(unit.MaxHealth);

    GameObject corpse = FindCorpse("BlueGround_Corpse");
    Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.EqualTo(material));
    Assert.That(corpse.transform.position, Is.EqualTo(new Vector3(3f, 0.01f, -2f)));
    Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
    Assert.That(corpse.layer, Is.Not.EqualTo(TargetableLayer));
    Collider collider = corpse.GetComponent<Collider>();
    Assert.That(collider == null || !collider.enabled, Is.True);
}

[Test]
public void AirCorpseFallsToGroundOnlyAfterConfiguredDuration()
{
    CombatUnit unit = CreateUnit("RedAir", Altitude.Air, new Vector3(-4f, 5f, 2f));
    unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(unit, CreateMaterial(Color.red), GroundLayer);

    unit.TakePhysicalDamage(unit.MaxHealth);
    CorpseFallController fall = FindCorpse("RedAir_Corpse").GetComponent<CorpseFallController>();
    fall.Tick(0.15f);
    Assert.That(fall.transform.position.y, Is.GreaterThan(0.01f));
    fall.Tick(0.15f);
    Assert.That(fall.HasLanded, Is.True);
    Assert.That(fall.transform.position.y, Is.EqualTo(0.01f).Within(0.0001f));
}
~~~

再创建未配置 presenter 的塔/训练目标式 CombatUnit 并击杀，断言没有新增 _Corpse 对象。这锁定非范围对象不受影响。

- [ ] **Step 2: Run tests to verify RED**

在临时干净 checkout 运行：

~~~bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform EditMode \
  -testFilter ArknightsFrontline.Tests.EditMode.DeathCorpsePresenterTests \
  -testResults /private/tmp/death-corpse-editmode-red.xml
~~~

预期：因两个新组件缺失而失败；若先报 LicenseClient 505，记录为环境阻塞并终止 Unity。

- [ ] **Step 3: Write minimal implementation**

DeathCorpsePresenter.Configure 必须验证 combatUnit 与 corpseMaterial 非空、替换旧事件订阅并保存 Ground 层。OnUnitDied 只执行一次，使用以下核心逻辑：

~~~csharp
private void OnUnitDied(CombatUnit _)
{
    if (hasSpawnedCorpse) return;
    hasSpawnedCorpse = true;
    GameObject corpse = CreateCorpse();
    if (combatUnit.Altitude == Altitude.Air)
        corpse.AddComponent<CorpseFallController>().Configure(ResolveLandingPosition(), 0.3f);
    foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
    Destroy(gameObject);
}
~~~

CreateCorpse() 以 GameObject.CreatePrimitive(PrimitiveType.Plane) 创建 gameObject.name + "_Corpse"，设为 Default 层、缩放为 new Vector3(0.15f, 1f, 0.15f)，指定共享材质。立刻禁用且延迟销毁 primitive Collider，确保当前帧也不阻挡交互。地面单位直接使用 ResolveLandingPosition()；空中单位保留死亡时 X/Y/Z 为动画起点。

ResolveLandingPosition() 从 transform.position + Vector3.up 向 Ground 层射线 100f；命中使用命中点 Y，否则使用 0f，最后加 0.01f。OnDestroy 必须解除死亡事件订阅。

CorpseFallController 存储起点、终点、总时长、已过时间和 HasLanded；Configure 拒绝 duration <= 0f；Tick 累加非负 delta：

~~~csharp
float progress = Mathf.Clamp01(elapsed / duration);
transform.position = Vector3.Lerp(startPosition, landingPosition, progress);
if (progress >= 1f)
{
    transform.position = landingPosition;
    HasLanded = true;
}
~~~

Update 调用 Tick(Time.deltaTime)。不得为尸体添加 CombatUnit、Targetable 层、UnitMotor、攻击、输入或碰撞组件。

- [ ] **Step 4: Run tests to verify GREEN**

重复 Step 2 命令，随后移除 -testFilter 运行全量 EditMode。预期为新增测试与既有测试通过；若仍是同一 LicenseClient 阻塞，记录阻塞并改做 Unity Roslyn/Bee Runtime 与 EditMode 静态编译。执行：

~~~bash
git diff --check HEAD -- \
  Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs \
  Assets/Game/Scripts/Combat/CorpseFallController.cs \
  Assets/Tests/EditMode/DeathCorpsePresenterTests.cs
~~~

预期无输出。

- [ ] **Step 5: Commit Task 1**

~~~bash
git add Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs.meta \
  Assets/Game/Scripts/Combat/CorpseFallController.cs Assets/Game/Scripts/Combat/CorpseFallController.cs.meta \
  Assets/Tests/EditMode/DeathCorpsePresenterTests.cs Assets/Tests/EditMode/DeathCorpsePresenterTests.cs.meta
git commit -m "feat: add death corpse presentation"
~~~

### Task 2: 为场景干员与兵线接入尸体表现

**Files:**

- Modify: Assets/Game/Editor/PrototypeSceneBuilder.cs
- Modify: Assets/Game/Scripts/Arena/MinionWaveSpawner.cs
- Modify: Assets/Tests/EditMode/MinionWaveSpawnerTests.cs
- Create: Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs

**Interfaces:**

- Consumes: DeathCorpsePresenter.Configure(CombatUnit, Material, int) 和 CorpseFallController.HasLanded。
- Produces: 玩家与每个生成小兵拥有已配置 presenter；BlueTower、RedTower 和 TrainingDummy_Red 不拥有 presenter。

- [ ] **Step 1: Write the failing integration and PlayMode tests**

在 MinionWaveSpawnerTests.SpawnWaveNowCreatesFourMinionsPerSideWithRequiredComponents 的每个小兵断言块中加入：

~~~csharp
Assert.That(minion.GetComponent<DeathCorpsePresenter>(), Is.Not.Null);
~~~

新建 DeathCorpsePresentationPlayModeTests。不要加载尚未重建的 PrototypeArena；在测试内创建 Ground 平面、两座塔、带蓝红材质和 Ground 层的 MinionWaveSpawner，调用 SpawnWaveNow()。取一个蓝地面兵与红空中兵，分别执行 TakePhysicalDamage(unit.MaxHealth)。测试必须 yield return null 后确认原地面兵已不再出现在 FindObjectsByType<CombatUnit>；空中尸体存在且 0.15 秒后 Y 变小、再等待 0.2 秒后 HasLanded 为真且没有 CombatUnit。单独构造带 presenter 的玩家式 CombatUnit，验证它也生成尸体；再构造不带 presenter 的塔/训练目标式 CombatUnit，验证它们不生成尸体。

- [ ] **Step 2: Run tests to verify RED**

在临时干净 checkout 运行：

~~~bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -runTests -testPlatform PlayMode \
  -testFilter ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests \
  -testResults /private/tmp/death-corpse-playmode-red.xml
~~~

预期：构建器与 spawner 尚未接入 presenter 而失败；若 Unity 在发现测试前报 LicenseClient 505，则记录环境阻塞。

- [ ] **Step 3: Write minimal integration**

在 PrototypeSceneBuilder.Build() 中将 groundLayer 传给 CreatePlayer：

~~~csharp
GameObject player = CreatePlayer(arenaRoot.transform, layout.BlueDeployment, blueMaterial, groundLayer);
~~~

将 CreatePlayer 改为 (Transform parent, Vector3 deployment, Material material, int groundLayer)，并在 combatUnit.Configure(...) 后添加：

~~~csharp
DeathCorpsePresenter presenter = player.AddComponent<DeathCorpsePresenter>();
presenter.Configure(combatUnit, material, groundLayer);
~~~

将 MinionWaveSpawner.Configure 末尾扩展 int groundLayer，新增序列化字段，在 Awake 的同一路径传递它；在每个小兵的 CombatUnit.Configure 后添加：

~~~csharp
DeathCorpsePresenter presenter = minionObject.AddComponent<DeathCorpsePresenter>();
presenter.Configure(combatUnit, material, groundLayer);
~~~

在 builder 调用 waveSpawner.Configure(...) 时于 targetableLayer 后传入 groundLayer，并更新所有测试调用。MinionWaveSpawnerTests 的 CreateSpawner 必须创建并清理蓝红材质、传入 Ground 层 8，避免向 presenter 传入空材质。不得向塔或训练目标接入该组件，不得重建或保存场景。

- [ ] **Step 4: Run focused GREEN, full suite, and static checks**

重复 Step 2 命令，随后运行完整 EditMode 与 PlayMode。预期：尸体范围、源对象销毁、空中动画和目标排除均通过；若为 LicenseClient 阻塞，则记录而非宣称通过。

无论 Unity 是否可运行，都静态编译 Runtime、EditMode、PlayMode 源集，执行：

~~~bash
git diff --check HEAD -- \
  Assets/Game/Editor/PrototypeSceneBuilder.cs \
  Assets/Game/Scripts/Arena/MinionWaveSpawner.cs \
  Assets/Tests/EditMode/MinionWaveSpawnerTests.cs \
  Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git status --short
~~~

预期：无差异格式错误，且暂存文件仅是本任务脚本、测试和其 Unity .meta 文件。

- [ ] **Step 5: Commit Task 2**

~~~bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs \
  Assets/Game/Scripts/Arena/MinionWaveSpawner.cs \
  Assets/Tests/EditMode/MinionWaveSpawnerTests.cs \
  Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs \
  Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs.meta
git commit -m "feat: show corpses for player and minions"
~~~

## Plan self-review

- 规格覆盖：Task 1 实现独立平面、同色、Ground 回退和 0.3 秒空中下落；Task 2 接入干员和两类小兵，并锁定塔/训练目标排除。
- 范围控制：未加入尸体碰撞、阻挡、回收、对象池或输入/战斗改动。
- 类型一致性：DeathCorpsePresenter.Configure(CombatUnit, Material, int)、CorpseFallController.Tick(float) 与 HasLanded 在定义、调用和测试中同名。
- 占位关键字扫描通过；各任务均定义了调用 API、测试命令和提交范围。
