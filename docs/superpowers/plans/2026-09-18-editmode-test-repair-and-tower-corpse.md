# EditMode Test Repair and Tower Corpse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复当前 17 项 EditMode 失败，并让蓝、红防御塔死亡后留下同阵营颜色、约 `3 × 3` 的独立贴地平面残骸。

**Architecture:** 保留现有活单位与独立残骸对象的边界，扩展 `DeathCorpsePresenter` 使 Plane 缩放可配置，并统一区分 PlayMode 的延迟销毁与 EditMode 的立即销毁。其余失败只修正 EditMode 测试夹具的生命周期模拟和对象选择，不改变已人工验收的运行时玩法；塔通过 `PrototypeSceneBuilder` 显式接入扩展后的组件。

**Tech Stack:** Unity 6.3 LTS（6000.3.15f1）、C#、Unity Test Framework、NUnit、URP、Git。

## Global Constraints

- 干员、小兵和敌方干员继续使用 `(0.15f, 1f, 0.15f)` 的默认残骸缩放。
- 防御塔残骸使用 `(0.3f, 1f, 0.3f)` 的 Plane 缩放，实际水平面积约为 `3 × 3`。
- 防御塔仍保持最大生命值 `500`、攻击力 `20`；不调整防御、射程、攻击间隔或攻击目标能力。
- 残骸不包含 `CombatUnit`、`HealthBarPresenter`、`BasicAttackController`、`TowerCombatController`，不在 `Targetable` 层，并且当前没有碰撞体。
- PlayMode 保留“立即隐藏、帧末销毁”；EditMode 使用 `DestroyImmediate`。
- 不修改移动、手动攻击、攻击中断、小兵作战或胜负规则。
- 不处理工作区中与本功能无关的 Package、ProjectSettings、日志和未跟踪文档变更。
- 允许场景构建器覆盖 `Assets/Game/Scenes/PrototypeArena.unity`。

---

## File Structure

- `Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs`：唯一的死亡残骸创建器；新增可配置缩放和安全销毁入口。
- `Assets/Game/Editor/PrototypeSceneBuilder.cs`：为两座塔传入阵营材质、Ground 层和塔残骸尺寸。
- `Assets/Tests/EditMode/CombatCommandResolverTests.cs`：补齐 `PlayerCommandController.Awake` 生命周期模拟。
- `Assets/Tests/EditMode/LaneMinionControllerTests.cs`：为需要计时攻击的用例配置攻击所有者。
- `Assets/Tests/EditMode/MinionWaveSpawnerTests.cs`：使用唯一名称选择固定地面小兵。
- `Assets/Tests/EditMode/ProjectileTests.cs`：在 EditMode 测试中显式创建弹丸可视组件。
- `Assets/Tests/EditMode/DeathCorpsePresenterTests.cs`：验证 EditMode 立即销毁、默认缩放和自定义塔缩放。
- `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs`：验证已保存场景中的塔残骸接入、血条清理和胜负结算。
- `Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs`：保留运行时帧末销毁与无配置单位不生成残骸的契约。
- `Assets/Game/Scenes/PrototypeArena.unity`：由构建器重建，保存两座塔的残骸组件配置。

---

### Task 1: Repair the fourteen EditMode fixture failures unrelated to corpse destruction

**Files:**
- Modify: `Assets/Tests/EditMode/CombatCommandResolverTests.cs`
- Modify: `Assets/Tests/EditMode/LaneMinionControllerTests.cs`
- Modify: `Assets/Tests/EditMode/MinionWaveSpawnerTests.cs`
- Modify: `Assets/Tests/EditMode/ProjectileTests.cs`

**Interfaces:**
- Consumes: existing private Unity lifecycle methods `PlayerCommandController.Awake()` and `Projectile.Awake()`; existing `BasicAttackController.Configure(CombatUnit)`.
- Produces: EditMode fixtures that initialize the same dependencies Unity initializes in PlayMode; no runtime API changes.

- [ ] **Step 1: Record the existing red baseline for the four suites**

Run in Unity Test Runner, EditMode:

```text
ArknightsFrontline.Tests.EditMode.CombatCommandResolverTests
ArknightsFrontline.Tests.EditMode.LaneMinionControllerTests
ArknightsFrontline.Tests.EditMode.MinionWaveSpawnerTests
ArknightsFrontline.Tests.EditMode.ProjectileTests
```

Expected baseline: exactly 14 failures distributed as `10 + 1 + 1 + 2`, with the messages already captured in the design spec: null `UnitMotor`, null retained minion target, multiple matching ground minions, and missing projectile Renderer.

- [ ] **Step 2: Initialize `PlayerCommandController` in the resolver fixture**

In `CombatCommandResolverTests.CreatePlayer`, immediately after `AddComponent<PlayerCommandController>()`, add the same explicit EditMode lifecycle invocation already used by `PlayerCommandControllerTests`:

```csharp
controller = player.AddComponent<PlayerCommandController>();
typeof(PlayerCommandController)
    .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
    .Invoke(controller, null);
```

Do not modify `PlayerCommandController` runtime behavior to accommodate a test-only lifecycle omission.

- [ ] **Step 3: Configure the attack owner in the cadence test**

In `MinionKeepsCurrentTargetAndAttackCadenceWhenCloserEnemyEntersRange`, configure the attack component before its first `Tick`:

```csharp
BasicAttackController attack = minion.gameObject.AddComponent<BasicAttackController>();
attack.Configure(minion);
```

Keep the existing assertions that the current target remains selected and that the second attack waits for the remaining `0.4f` interval.

- [ ] **Step 4: Select a unique spawned ground minion**

Replace the ambiguous prefix-based `Single` call in `SpawnedMinionsUseFixedCombatProfilesAndMovementSpeeds`:

```csharp
CombatUnit blueGround = GetSpawnedMinions()
    .Single(unit => unit.name == "BlueGroundMinion_1_1");
```

Keep the red air selection unchanged because each wave contains exactly one red air minion.

- [ ] **Step 5: Initialize projectile visuals in the EditMode helper**

Change `ProjectileTests.CreateProjectile` so every test gets the same visual setup performed by runtime `Awake`:

```csharp
private Projectile CreateProjectile()
{
    GameObject gameObject = new GameObject("Projectile");
    gameObjects.Add(gameObject);
    Projectile projectile = gameObject.AddComponent<Projectile>();
    typeof(Projectile)
        .GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
        .Invoke(projectile, null);
    return projectile;
}
```

- [ ] **Step 6: Run the four repaired suites**

Run the four EditMode suites from Step 1.

Expected: all tests in those suites pass; the Test Runner no longer reports the 14 fixture failures. If a failure remains, read its new exact message before making another change.

- [ ] **Step 7: Check and commit the fixture repair**

Run:

```bash
git diff --check -- Assets/Tests/EditMode/CombatCommandResolverTests.cs Assets/Tests/EditMode/LaneMinionControllerTests.cs Assets/Tests/EditMode/MinionWaveSpawnerTests.cs Assets/Tests/EditMode/ProjectileTests.cs
git add Assets/Tests/EditMode/CombatCommandResolverTests.cs Assets/Tests/EditMode/LaneMinionControllerTests.cs Assets/Tests/EditMode/MinionWaveSpawnerTests.cs Assets/Tests/EditMode/ProjectileTests.cs
git commit -m "test: repair editmode lifecycle fixtures"
```

Expected: whitespace check succeeds and the commit contains only the four EditMode test files.

---

### Task 2: Add configurable corpse footprint and correct EditMode destruction

**Files:**
- Modify: `Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs`
- Modify: `Assets/Tests/EditMode/DeathCorpsePresenterTests.cs`
- Modify: `Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs`

**Interfaces:**
- Consumes: `CombatUnit.Died`, shared team `Material`, Ground layer index.
- Produces: existing `Configure(CombatUnit, Material, int)` with unchanged default behavior; new overload `Configure(CombatUnit, Material, int, Vector3)` for tower footprint configuration.

- [ ] **Step 1: Add failing tests for default and custom corpse scale**

In `GroundUnitDeathCreatesSameMaterialCorpseAtGroundOffset`, add the unchanged default contract:

```csharp
Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.15f, 1f, 0.15f)));
```

Add a new EditMode test:

```csharp
[Test]
public void ConfiguredCorpseScaleControlsPlaneFootprint()
{
    CreateGround();
    CombatUnit unit = CreateUnit("BlueTower", Altitude.Ground, new Vector3(-40f, 3f, 0f));
    Material material = CreateMaterial(Color.blue);
    unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(
        unit,
        material,
        GroundLayer,
        new Vector3(0.3f, 1f, 0.3f));

    unit.TakePhysicalDamage(unit.MaxHealth);

    GameObject corpse = FindAndTrackNewCorpse("BlueTower_Corpse");
    Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.3f, 1f, 0.3f)));
    Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
    Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
    Assert.That(corpse.GetComponent<Collider>(), Is.Null);
}
```

- [ ] **Step 2: Run the new custom-scale test and confirm red**

Run `DeathCorpsePresenterTests.ConfiguredCorpseScaleControlsPlaneFootprint` in EditMode.

Expected: compile failure because the four-argument `Configure` overload does not exist, or assertion failure because the scale remains `(0.15, 1, 0.15)`.

- [ ] **Step 3: Add the serialized default scale and overload**

In `DeathCorpsePresenter`, add:

```csharp
private static readonly Vector3 DefaultCorpseScale = new Vector3(0.15f, 1f, 0.15f);

[SerializeField] private Vector3 corpseScale = new Vector3(0.15f, 1f, 0.15f);
```

Keep the existing three-argument public API by delegating to the new overload:

```csharp
public void Configure(CombatUnit combatUnit, Material corpseMaterial, int groundLayer)
{
    Configure(combatUnit, corpseMaterial, groundLayer, DefaultCorpseScale);
}

public void Configure(
    CombatUnit combatUnit,
    Material corpseMaterial,
    int groundLayer,
    Vector3 corpseScale)
{
    if (combatUnit == null)
    {
        throw new ArgumentNullException(nameof(combatUnit));
    }

    if (corpseMaterial == null)
    {
        throw new ArgumentNullException(nameof(corpseMaterial));
    }

    UnsubscribeFromDeath();
    this.combatUnit = combatUnit;
    this.corpseMaterial = corpseMaterial;
    this.groundLayer = groundLayer;
    this.corpseScale = corpseScale;
    SubscribeToDeath();
}
```

Change `CreateCorpse` to use the configured value:

```csharp
corpse.transform.localScale = corpseScale;
```

- [ ] **Step 4: Add the environment-aware destruction helper**

Replace both direct `Destroy` calls in `DeathCorpsePresenter` with this focused helper:

```csharp
private static void DestroyUnityObject(UnityEngine.Object target)
{
    if (target == null)
    {
        return;
    }

    if (Application.isPlaying)
    {
        Destroy(target);
        return;
    }

    DestroyImmediate(target);
}
```

Use it for both objects:

```csharp
DestroyUnityObject(gameObject);
```

and:

```csharp
collider.enabled = false;
DestroyUnityObject(collider);
```

- [ ] **Step 5: Align the EditMode source-destruction test with immediate semantics**

Replace the current EditMode `[UnityTest]` with a synchronous test:

```csharp
[Test]
public void UnitDeathDestroysSourceImmediatelyInEditMode()
{
    CreateGround();
    CombatUnit unit = CreateUnit("RenderedUnit", Altitude.Ground, Vector3.zero);
    unit.gameObject.AddComponent<MeshRenderer>();
    unit.gameObject.AddComponent<DeathCorpsePresenter>().Configure(
        unit,
        CreateMaterial(Color.white),
        GroundLayer);

    unit.TakePhysicalDamage(unit.MaxHealth);

    Assert.That(unit == null, Is.True);
    FindAndTrackNewCorpse("RenderedUnit_Corpse");
}
```

Remove the now-unused `System.Collections` and `UnityEngine.TestTools` imports from the EditMode file. The existing PlayMode `UnitDeathDestroysItsHealthBarWithoutAddingOneToTheCorpse` test continues to verify next-frame runtime destruction.

- [ ] **Step 6: Rename the PlayMode opt-in coverage so towers are no longer described as globally excluded**

Rename `ConfiguredPlayerAndMinionsCreateCorpsesWhileTowersDoNot` to:

```csharp
ConfiguredPresentersCreateCorpsesWhileUnconfiguredUnitsDoNot
```

Keep the local fixture towers unconfigured because this test verifies that `DeathCorpsePresenter` remains opt-in. Replace the tower-specific exclusion assertion text with an explicit unconfigured unit:

```csharp
CombatUnit unconfiguredUnit = CreateUnit(
    "UnconfiguredUnit",
    TeamId.Red,
    Altitude.Ground,
    Vector3.zero,
    10f);
int corpseCountBeforeUnconfiguredDeath = FindCorpses().Length;
unconfiguredUnit.TakePhysicalDamage(unconfiguredUnit.MaxHealth);
yield return null;
Assert.That(FindCorpses().Length, Is.EqualTo(corpseCountBeforeUnconfiguredDeath));
```

Do not kill the two local towers in this test; the saved-scene tower behavior is covered in Task 3.

- [ ] **Step 7: Run corpse tests**

Run:

```text
EditMode: ArknightsFrontline.Tests.EditMode.DeathCorpsePresenterTests
PlayMode: ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests
```

Expected: all tests pass; no `Destroy may not be called from edit mode` messages; default corpses remain `0.15` scale and configured tower-style corpses use `0.3` scale.

- [ ] **Step 8: Check and commit the presenter extension**

Run:

```bash
git diff --check -- Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs Assets/Tests/EditMode/DeathCorpsePresenterTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git add Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs Assets/Tests/EditMode/DeathCorpsePresenterTests.cs Assets/Tests/PlayMode/DeathCorpsePresentationPlayModeTests.cs
git commit -m "fix: support safe configurable death corpses"
```

Expected: the commit contains only the presenter and its direct EditMode/PlayMode tests.

---

### Task 3: Configure both saved-scene towers to create corpses

**Files:**
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs`

**Interfaces:**
- Consumes: Task 2 overload `DeathCorpsePresenter.Configure(CombatUnit, Material, int, Vector3)`.
- Produces: both saved tower roots with `DeathCorpsePresenter` configured to `(0.3f, 1f, 0.3f)`.

- [ ] **Step 1: Extend the saved-scene smoke assertions before modifying the builder**

In `AssertTowerIsCombatReady`, add:

```csharp
Assert.That(tower.GetComponent<DeathCorpsePresenter>(), Is.Not.Null,
    $"{tower.name} needs a DeathCorpsePresenter.");
```

Expand `SavedOutcomeControllerResolvesDestroyedTower` so it captures the visible state before death and asserts the corpse after one frame:

```csharp
GameObject redTowerObject = arena.RedTower.gameObject;
CombatUnit redTower = redTowerObject.GetComponent<CombatUnit>();
Renderer redTowerRenderer = redTowerObject.transform.Find("RedTowerVisual").GetComponent<Renderer>();
Material redMaterial = redTowerRenderer.sharedMaterial;
Transform healthBar = redTowerObject.transform.Find("HealthBar");
Assert.That(healthBar, Is.Not.Null);

redTower.TakePhysicalDamage(redTower.MaxHealth);
yield return null;

Assert.That(outcome.IsMatchOver, Is.True);
Assert.That(outcome.Outcome, Is.EqualTo(MatchOutcome.BlueVictory));
Assert.That(redTowerObject == null, Is.True);
Assert.That(healthBar == null, Is.True);
GameObject corpse = GameObject.Find("RedTower_Corpse");
Assert.That(corpse, Is.Not.Null);
Assert.That(corpse.transform.localScale, Is.EqualTo(new Vector3(0.3f, 1f, 0.3f)));
Assert.That(corpse.GetComponent<Renderer>().sharedMaterial, Is.SameAs(redMaterial));
Assert.That(corpse.GetComponent<CombatUnit>(), Is.Null);
Assert.That(corpse.GetComponent<HealthBarPresenter>(), Is.Null);
Assert.That(corpse.GetComponent<BasicAttackController>(), Is.Null);
Assert.That(corpse.GetComponent<TowerCombatController>(), Is.Null);
Assert.That(corpse.layer, Is.EqualTo(LayerMask.NameToLayer("Default")));
Assert.That(corpse.GetComponent<Collider>(), Is.Null);
```

- [ ] **Step 2: Run the two scene tests and confirm red before scene rebuild**

Run:

```text
ArenaSceneSmokeTests.PrototypeArenaContainsRequiredRoots
ArenaSceneSmokeTests.SavedOutcomeControllerResolvesDestroyedTower
```

Expected: failure because the saved tower roots do not yet have `DeathCorpsePresenter` and do not create `RedTower_Corpse`.

- [ ] **Step 3: Pass Ground layer into tower construction**

Update both `CreateTower` calls in `PrototypeSceneBuilder.Build`:

```csharp
Transform blueTower = CreateTower(
    arenaRoot.transform,
    "BlueTower",
    layout.BlueTower,
    blueMaterial,
    targetableLayer,
    groundLayer,
    TeamId.Blue);
```

and the equivalent red call. Update the signature:

```csharp
private static Transform CreateTower(
    Transform parent,
    string towerName,
    Vector3 position,
    Material material,
    int targetableLayer,
    int groundLayer,
    TeamId team)
```

- [ ] **Step 4: Configure the tower corpse presenter**

Immediately after adding the tower health bar, add:

```csharp
tower.AddComponent<HealthBarPresenter>();
DeathCorpsePresenter corpsePresenter = tower.AddComponent<DeathCorpsePresenter>();
corpsePresenter.Configure(
    combatUnit,
    material,
    groundLayer,
    new Vector3(0.3f, 1f, 0.3f));
```

Keep the existing attack and tower controller configuration unchanged.

- [ ] **Step 5: Compile editor scripts before rebuilding the scene**

Open the project in Unity 6000.3.15f1 and wait for compilation to finish.

Expected: Console contains no C# compilation errors. Do not run the scene builder while Safe Mode or compile errors are active.

- [ ] **Step 6: Rebuild the scene**

Preferred command when licensing is available:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath /Users/ynie/Documents/Hobby/Game/arknights-frontline -executeMethod ArknightsFrontline.Editor.PrototypeSceneBuilder.Build -logFile /Users/ynie/Documents/Hobby/Game/arknights-frontline/build-tower-corpse.log
```

If the Licensing Client blocks batch mode, use the editor menu:

```text
Arknights Frontline → Build Prototype Arena
```

Expected: `PrototypeArena.unity` is overwritten and both `BlueTower` and `RedTower` serialize a `DeathCorpsePresenter` with scale `(0.3, 1, 0.3)`.

- [ ] **Step 7: Run the saved-scene tower tests**

Run the two PlayMode tests from Step 2.

Expected: both pass; destroying the red tower leaves `RedTower_Corpse`, removes the health bar, and still resolves `BlueVictory`.

- [ ] **Step 8: Check and commit builder plus rebuilt scene**

Run:

```bash
git diff --check -- Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git add Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git commit -m "feat: leave tower corpses after destruction"
```

Expected: the commit includes only the builder, tower smoke tests, and rebuilt scene.

---

### Task 4: Full regression verification and manual acceptance handoff

**Files:**
- Verify only: `Assets/Game/Scripts/Combat/DeathCorpsePresenter.cs`
- Verify only: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Verify only: `Assets/Tests/EditMode/*.cs`
- Verify only: `Assets/Tests/PlayMode/*.cs`
- Verify only: `Assets/Game/Scenes/PrototypeArena.unity`

**Interfaces:**
- Consumes: all deliverables from Tasks 1–3.
- Produces: recorded EditMode, relevant PlayMode, scene, and manual acceptance evidence; no new runtime interface.

- [ ] **Step 1: Run all EditMode tests**

Preferred batch command:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath /Users/ynie/Documents/Hobby/Game/arknights-frontline -runTests -testPlatform EditMode -testResults /Users/ynie/Documents/Hobby/Game/arknights-frontline/TestResults/tower-corpse-editmode.xml -logFile /Users/ynie/Documents/Hobby/Game/arknights-frontline/TestResults/tower-corpse-editmode.log
```

Fallback: Unity Test Runner → EditMode → Run All.

Expected: all 88 original EditMode tests plus the new custom-scale test pass, with zero failures and no edit-mode `Destroy` warnings. Record the actual discovered count rather than hard-coding success if Unity discovers a different total after additions.

- [ ] **Step 2: Run the relevant PlayMode regression suites**

Run:

```text
ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests
ArknightsFrontline.Tests.PlayMode.DeathCorpsePresentationPlayModeTests
ArknightsFrontline.Tests.PlayMode.BasicCombatPlayModeTests
ArknightsFrontline.Tests.PlayMode.MinionLanePlayModeTests
```

Expected: all tests in these suites pass. These suites cover saved-scene configuration, frame-delayed destruction, health-bar cleanup, win/draw settlement, projectile ordering, and minion behavior.

- [ ] **Step 3: Run the remaining PlayMode tests as a broad regression signal**

Run PlayMode → Run All.

Expected for this feature: no new failure caused by tower corpse configuration. If previously existing unrelated failures remain, capture their exact names and messages and report them separately; do not delete assertions or expand this plan without a new diagnosis.

- [ ] **Step 4: Inspect the final Git scope**

Run:

```bash
git status --short
git diff --check
git log --oneline -5
```

Expected: feature changes are committed in three focused commits. Existing unrelated Package, ProjectSettings, logs, and untracked documentation remain untouched.

- [ ] **Step 5: Hand off manual acceptance**

Ask the user to open `PrototypeArena`, enter Play Mode, and verify both teams separately:

```text
1. The living tower shows its health bar.
2. Reducing tower health to zero removes the 3D tower and health bar.
3. A same-color, approximately 3 × 3 plane remains at the tower base.
4. The plane cannot be selected or attacked and does not block movement.
5. Destroying RedTower yields BlueVictory; destroying BlueTower yields RedVictory.
```

Do not claim manual acceptance until the user reports the result.
