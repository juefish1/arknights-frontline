# 干员体型与单位血条 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让原型场景中的玩家干员体积明显大于小兵，并让干员和每个小兵始终显示可随生命值变化的头顶血条。

**Architecture:** 新增 `HealthBarPresenter`，它仅负责从同对象的 `CombatUnit` 读取生命比例，并在对象头顶维护一个面向主相机的世界空间 Canvas。场景构建器和小兵生成器仅负责挂载该组件；死亡时活单位对象被销毁，作为子物体的血条自然消失，不与尸体绑定。

**Tech Stack:** Unity 6.3 LTS、C#、Unity UI (`UnityEngine.UI`)、NUnit EditMode/PlayMode 测试。

## Global Constraints

- 玩家干员保留胶囊模型，仅通过 Transform 放大体积；攻击、移动与战斗数值不变。
- 血条始终显示：满血显示完整绿条，受伤后按 `CurrentHealth / MaxHealth` 缩短。
- 范围仅包含 `Player_Exusiai` 与 `MinionWaveSpawner` 创建的地面、空中小兵；不包含塔、训练目标与尸体。
- 不修改用户已保存的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages 或 ProjectSettings。
- 每个生产代码行为先由失败测试定义；若 Unity Licensing Client 阻止自动测试，保留命令日志并在编辑器内完成同等人工验收。

---

### Task 1: 添加可独立验证的血条表现组件

**Files:**
- Create: `Assets/Game/Scripts/Combat/HealthBarPresenter.cs`
- Create: `Assets/Game/Scripts/Combat/HealthBarPresenter.cs.meta`（由 Unity 生成）
- Create: `Assets/Tests/EditMode/HealthBarPresenterTests.cs`
- Modify: `Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs:18-120`

**Interfaces:**
- Consumes: `CombatUnit.MaxHealth`、`CombatUnit.CurrentHealth`、`UnityEngine.Camera.main`。
- Produces: `HealthBarPresenter.Configure(CombatUnit combatUnit)`、`HealthBarPresenter.Tick()`、只读 `FillAmount` 与 `IsVisible`，供测试与场景组件使用。

- [ ] **Step 1: 写入失败的初始显示、受伤同步与死亡清理测试**

```csharp
[Test]
public void ConfiguredHealthBarIsVisibleAndFullAtMaximumHealth()
{
    CombatUnit unit = CreateUnit(100f);
    HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();

    presenter.Configure(unit);
    presenter.Tick();

    Assert.That(presenter.IsVisible, Is.True);
    Assert.That(presenter.FillAmount, Is.EqualTo(1f));
Assert.That(unit.transform.Find("HealthBar"), Is.Not.Null);
}
```

同一测试类中加入：

```csharp
[Test]
public void TickReflectsCurrentHealthAsFillAmount()
{
    CombatUnit unit = CreateUnit(100f);
    HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
    presenter.Configure(unit);

    unit.TakePhysicalDamage(35f);
    presenter.Tick();

    Assert.That(presenter.FillAmount, Is.EqualTo(0.65f).Within(0.0001f));
}
```

在 `DeathCorpsePresentationPlayModeTests` 中创建带 `CombatUnit`、`DeathCorpsePresenter` 和 `HealthBarPresenter` 的独立地面单位；记录它的 `HealthBar` 子物体，然后击杀单位并等待一帧：

```csharp
Transform healthBar = unit.transform.Find("HealthBar");
unit.TakePhysicalDamage(unit.MaxHealth);
yield return null;

Assert.That(unit, Is.Null);
Assert.That(healthBar, Is.Null);
Assert.That(GameObject.Find("HealthBar_Corpse"), Is.Null);
```

- [ ] **Step 2: 运行该测试，确认因为缺少组件而失败**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.HealthBarPresenterTests.ConfiguredHealthBarIsVisibleAndFullAtMaximumHealth -testResults TestResults/health-bar-red.xml -logFile TestResults/health-bar-red.log -quit
```

Expected: 编译失败，指出 `HealthBarPresenter` 尚不存在。随后以相同命令将 `-testPlatform` 改为 `PlayMode`、`-testFilter` 改为 `ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests`，确认死亡清理测试也因同一缺失组件失败。

- [ ] **Step 3: 实现最小的世界空间血条组件**

```csharp
public sealed class HealthBarPresenter : MonoBehaviour
{
    private const float Width = 80f;
    private const float Height = 12f;
    private const float WorldScale = 0.01f;
    private const float HeadOffset = 0.35f;

    private CombatUnit combatUnit;
    private Transform barTransform;
    private Image fillImage;

    public float FillAmount => fillImage == null ? 0f : fillImage.fillAmount;
    public bool IsVisible => barTransform != null && barTransform.gameObject.activeSelf;

    private void Awake()
    {
        CombatUnit unit = GetComponent<CombatUnit>();
        if (unit != null)
        {
            Configure(unit);
        }
    }

    private void Update()
    {
        Tick();
    }

    public void Configure(CombatUnit unit)
    {
        combatUnit = unit ?? throw new ArgumentNullException(nameof(unit));
        EnsureVisuals();
    }

    public void Tick()
    {
        if (combatUnit == null)
        {
            return;
        }

        EnsureVisuals();
        fillImage.fillAmount = Mathf.Clamp01(combatUnit.CurrentHealth / combatUnit.MaxHealth);
    }
}
```

`EnsureVisuals` 必须创建名称为 `HealthBar` 的子对象、`Canvas`（`RenderMode.WorldSpace`）、深色背景 `Image` 和前景绿色 `Image`。前景 `Image.type` 设为 `Image.Type.Filled`、`fillMethod` 设为 `Horizontal`，并使用左到右填充。根 RectTransform 的尺寸为 `80 x 12`，世界缩放为 `0.01`。

- [ ] **Step 4: 运行初始显示、受伤同步与死亡清理测试，确认通过**

Run the Step 2 command again.

Expected: 两个 EditMode 测试均 PASS；死亡 PlayMode 测试也 PASS，且尸体只保留 `<UnitName>_Corpse` 平面，不带血条。

- [ ] **Step 5: 为头顶定位与相机朝向写入失败测试**

```csharp
[Test]
public void TickPlacesHealthBarAboveTheUnitRenderer()
{
    GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    gameObjects.Add(player);
    player.transform.position = new Vector3(0f, 1f, 0f);
    CombatUnit unit = player.AddComponent<CombatUnit>();
    unit.Configure(TeamId.Blue, Altitude.Ground, 100f, 0f, 0f, 0f, 0f, false, false);
    HealthBarPresenter presenter = unit.gameObject.AddComponent<HealthBarPresenter>();
    presenter.Configure(unit);

    presenter.Tick();

    Assert.That(
        unit.transform.Find("HealthBar").position.y,
        Is.GreaterThan(player.GetComponent<Renderer>().bounds.max.y));
}
```

- [ ] **Step 6: 运行测试，确认它在定位实现之前失败**

Run the Step 2 command with `-testFilter ArknightsFrontline.Tests.EditMode.HealthBarPresenterTests.TickPlacesHealthBarAboveTheUnitRenderer`.

Expected: FAIL，血条仍停在默认位置，未位于胶囊顶部之上。

- [ ] **Step 7: 用最小改动实现头顶定位和相机朝向**

在 `Tick` 的生命比例更新后添加：

```csharp
PositionAboveUnit();
FaceMainCamera();
```

`PositionAboveUnit` 使用当前对象根 `Renderer.bounds.max.y + 0.35f`；没有 Renderer 时回退到 `transform.position + Vector3.up * 1.5f`。`FaceMainCamera` 在存在 `Camera.main` 时使血条正面朝向相机。

- [ ] **Step 8: 运行组件的 EditMode 与死亡 PlayMode 测试并提交**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.HealthBarPresenterTests -testResults TestResults/health-bar-presenter-editmode.xml -logFile TestResults/health-bar-presenter-editmode.log -quit
```

Expected: PASS。

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests -testResults TestResults/health-bar-death-playmode.xml -logFile TestResults/health-bar-death-playmode.log -quit
```

Expected: PASS。

```bash
git add Assets/Game/Scripts/Combat/HealthBarPresenter.cs Assets/Game/Scripts/Combat/HealthBarPresenter.cs.meta Assets/Tests/EditMode/HealthBarPresenterTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git commit -m "feat: add unit health bar presenter"
```

### Task 2: 将血条接入玩家干员和波次小兵

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:143-161`
- Modify: `Assets/Game/Scripts/Arena/MinionWaveSpawner.cs:188-211`
- Modify: `Assets/Tests/EditMode/MinionWaveSpawnerTests.cs:28-53`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs:13-36`

**Interfaces:**
- Consumes: `HealthBarPresenter` 的自动 `Awake` 配置和 `CombatUnit`。
- Produces: 场景构建器创建的玩家与波次生成器创建的全部小兵都具有 `HealthBarPresenter`。

- [ ] **Step 1: 扩展小兵组件测试，使其先失败**

在 `SpawnWaveNowCreatesFourMinionsPerSideWithRequiredComponents` 的循环中加入：

```csharp
Assert.That(minion.GetComponent<HealthBarPresenter>(), Is.Not.Null);
```

- [ ] **Step 2: 运行小兵测试并确认失败**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter ArknightsFrontline.Tests.EditMode.MinionWaveSpawnerTests.SpawnWaveNowCreatesFourMinionsPerSideWithRequiredComponents -testResults TestResults/minion-health-bar-red.xml -logFile TestResults/minion-health-bar-red.log -quit
```

Expected: FAIL，生成的小兵没有 `HealthBarPresenter`。

- [ ] **Step 3: 在小兵创建链路中添加组件**

在 `MinionWaveSpawner.SpawnMinion` 中、`CombatUnit` 配置完成后添加：

```csharp
minionObject.AddComponent<HealthBarPresenter>();
```

不要向塔、训练目标或尸体添加该组件。

- [ ] **Step 4: 再次运行小兵测试，确认通过**

Run the Step 2 command again.

Expected: PASS。

- [ ] **Step 5: 写入场景玩家的失败测试**

在 `PrototypeArenaContainsRequiredRoots` 中追加：

```csharp
GameObject player = GameObject.Find("Player_Exusiai");
Assert.That(player, Is.Not.Null);
Assert.That(player.GetComponent<HealthBarPresenter>(), Is.Not.Null);
```

- [ ] **Step 6: 运行场景测试并确认失败**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests.PrototypeArenaContainsRequiredRoots -testResults TestResults/player-health-bar-red.xml -logFile TestResults/player-health-bar-red.log -quit
```

Expected: FAIL，保存的原型场景中玩家未挂载血条组件。

- [ ] **Step 7: 将血条接入玩家并重建测试场景**

在 `PrototypeSceneBuilder.CreatePlayer` 中、`CombatUnit.Configure(...)` 之后添加：

```csharp
player.AddComponent<HealthBarPresenter>();
```

通过 Unity 菜单 `Arknights Frontline → Build Prototype Arena` 重建测试场景。此动作会覆盖 `Assets/Game/Scenes/PrototypeArena.unity`，但不改动任何其它用户场景或项目设置。

- [ ] **Step 8: 运行小兵和场景测试，确认通过并提交**

Run the Step 2 and Step 6 commands again.

Expected: PASS。

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Game/Scripts/Arena/MinionWaveSpawner.cs Assets/Tests/EditMode/MinionWaveSpawnerTests.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git commit -m "feat: show health bars for player and minions"
```

### Task 3: 放大场景构建器中的干员胶囊体

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs:143-161`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs:13-36`

**Interfaces:**
- Consumes: `Player_Exusiai` 现有胶囊体、`ArenaLayout.BlueDeployment`。
- Produces: 玩家胶囊 `localScale == (1.6, 2.0, 1.6)` 且中心位于部署点上方 `2.0` 单位；其底部保持在地面高度。

- [ ] **Step 1: 写入体型与贴地位置的失败测试**

在 `PrototypeArenaContainsRequiredRoots` 中加入：

```csharp
Assert.That(player.transform.localScale, Is.EqualTo(new Vector3(1.6f, 2f, 1.6f)));
Assert.That(player.transform.position.y, Is.EqualTo(2f).Within(0.0001f));
```

- [ ] **Step 2: 运行场景测试，确认失败**

Run the Task 2 Step 6 command again.

Expected: FAIL，当前玩家胶囊仍是 `Vector3.one` 且 Y 位置为 `1`。

- [ ] **Step 3: 在创建玩家时设置体型和贴地位置**

将 `CreatePlayer` 中初始位置与缩放设为：

```csharp
player.transform.localScale = new Vector3(1.6f, 2f, 1.6f);
player.transform.position = deployment + Vector3.up * 2f;
```

不要调整 `UnitMotor.Configure` 与 `CombatUnit.Configure` 的参数。

- [ ] **Step 4: 重建场景并运行所有相关测试**

通过菜单重新执行 `Arknights Frontline → Build Prototype Arena`，然后运行：

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ArknightsFrontline.Tests.EditMode.HealthBarPresenterTests|ArknightsFrontline.Tests.EditMode.MinionWaveSpawnerTests" -testResults TestResults/unit-identity-editmode.xml -logFile TestResults/unit-identity-editmode.log -quit

/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests" -testResults TestResults/unit-identity-playmode.xml -logFile TestResults/unit-identity-playmode.log -quit
```

Expected: 两个命令均 PASS；死亡尸体测试确保销毁活单位时血条不会留在尸体上。

- [ ] **Step 5: 在编辑器中人工验收并提交**

1. 打开 `PrototypeArena` 并进入 Play Mode。
2. 确认蓝方干员是明显更大的胶囊体，双脚贴在地面。
3. 确认干员、地面小兵和空中小兵都始终显示绿色血条；塔和训练目标没有血条。
4. 对任意小兵或干员造成伤害，确认对应绿色填充立即缩短。
5. 击杀单位，确认血条消失且只留下平面尸体；空中尸体仍正常落地。

```bash
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git commit -m "feat: distinguish player size from minions"
```

## Plan self-review

- Spec coverage: Task 1 实现常显、比例更新、头顶定位与相机朝向；Task 2 接入玩家和两类小兵；Task 3 以明确比例放大玩家并在死亡场景中验证血条离开活单位。
- Scope: 不涉及塔、训练目标、尸体碰撞、数值文字或用户项目设置。
- Type consistency: 所有场景接入均使用 `HealthBarPresenter`，组件读取既有 `CombatUnit`，不存在额外生命数据源。
- Placeholder scan: 已检查，不含待定实现或未定义接口。

## 验收缺陷跟进：保存场景中的干员死亡表现（2026-09-10）

人工验收发现干员血量归零后，原胶囊和空血条仍留在场景中。根因是 `DeathCorpsePresenter.Configure` 在编辑器构建场景时绑定事件，但配置字段未序列化，运行时也没有恢复绑定。此前直接在运行时调用 `Configure` 的测试未覆盖此路径。

- [x] 将死亡表现配置持久化，并在组件加载时恢复死亡事件订阅；重复配置不得产生重复尸体。
- [x] 兼容已保存、缺少这些配置字段的旧场景：从单位组件、模型材质和 Ground 层恢复默认配置，无需重建场景。
- [x] 加入真实 `PrototypeArena` 场景加载后击杀玩家的回归测试，检查原模型隐藏、原对象与血条销毁、同色平面落地且没有碰撞。
- [x] 验证序列化配置恢复以及既有地面、空中小兵死亡表现，并记录自动测试结果。

验证记录（Unity 6000.3.15f1）：

- 修复前：两个新增回归测试均失败，分别表现为源 Renderer 未隐藏、恢复配置的组件没有生成尸体。结果：`TestResults/corpse-red-20260910.xml`。
- 修复后：`ArenaSceneSmokeTests` 与 `DeathCorpsePresentationPlayModeTests` 共 8 项全部通过，Unity 退出码为 0。结果：`TestResults/corpse-final-playmode-20260910.xml`。
- 测试维护：场景测试结束后卸载原型场景；小兵死亡测试限定本次创建的单位；销毁断言使用 Unity 的对象判空语义；及时清理测试尸体。
- 验证限制：补跑死亡组件和血条 EditMode 测试共 9 项，6 项通过、3 项失败；失败均来自旧死亡测试在 EditMode 中调用运行时 `Destroy`。本次未改变运行时销毁语义，这些测试仍需迁移或调整。结果：`TestResults/corpse-editmode-20260910.xml`。
- 本次未重建或写入 `PrototypeArena.unity`，也未修改 Packages、ProjectSettings；保留工作区原有修改。
