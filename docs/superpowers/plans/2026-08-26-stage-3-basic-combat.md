# 阶段 3：基础战斗 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在当前原型场景中实现玩家对红方训练目标的合法目标判定、追击、普攻投射物、物理伤害与死亡闭环。

**Architecture:** `CombatUnit` 保存单位状态，`TargetRules` 与 `TargetSelector` 是无副作用规则入口。`CombatCommandResolver` 将阶段 2 的命令转换为移动或攻击目标，`BasicAttackController` 负责攻击节奏，`Projectile` 在命中时重验合法性并结算物理伤害。场景构建器仅添加一个训练目标，不建设兵线、塔、AI、技能或 HUD。

**Tech Stack:** Unity 6000.3.15f1、Unity Input System 1.17.0、URP、NUnit、Unity Test Framework、C# 9（花括号命名空间）、macOS。

## Global Constraints

- 以 `docs/superpowers/specs/2026-08-26-stage-3-basic-combat-design.md` 为本计划的权威规格；不得与阶段 2 的 Q+左键语义冲突。
- 只实现基础战斗闭环：不引入兵线、防御塔战斗、电脑、技能、多段攻击、减速、重生、撤退、统计、HUD、数据表或通用效果系统。
- 阶段 2 的 `PlayerCommandController.CurrentCommand` 是输入意图来源；阶段 3 不改变它，也不得把 `AttackNearestInRange` 变成前往鼠标位置的移动。
- 所有新增 C# 代码使用 C# 9 兼容的花括号命名空间。运行时代码位于 `Assets/Game/Scripts`，测试位于 `Assets/Tests`。
- 不修改或提交用户持有的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages、ProjectSettings、build logs 或其他无关未提交文件。场景改变只通过 `Assets/Game/Editor/PrototypeSceneBuilder.cs` 生成，并且只提交其源代码与必要新增资产/元文件。
- Unity 测试在临时干净 checkout 中运行；若 Unity 6000.3.15f1 在编译前因 LicenseClient `ResponseCode: 505 / Unsupported protocol version '1.18.1'` 失败，记录为环境阻塞，绝不宣称测试已通过。

---

### Task 1：单位状态、目标规则与范围选择

**Files:**
- Create: `Assets/Game/Scripts/Combat/CombatUnit.cs`
- Create: `Assets/Game/Scripts/Combat/TargetRules.cs`
- Create: `Assets/Game/Scripts/Combat/TargetSelector.cs`
- Create: `Assets/Tests/EditMode/CombatUnitTests.cs`
- Create: `Assets/Tests/EditMode/TargetRulesTests.cs`
- Create: `Assets/Tests/EditMode/TargetSelectorTests.cs`

**Interfaces:**
- Consumes: `ArknightsFrontline.Common.TeamId` and `Altitude`.
- Produces: `CombatUnit.Configure(TeamId, Altitude, float maxHealth, float attackPower, float defense, float attackRange, float attackInterval, bool canAttackGround, bool canAttackAir)`, `CurrentHealth`, `IsDead`, `TakePhysicalDamage(float)`, `Died`; `TargetRules.IsLegal(CombatUnit, CombatUnit)`; `TargetSelector.FindNearestInRange(CombatUnit)`.

- [ ] **Step 1: Write the failing EditMode tests**

Create tests using real temporary `GameObject` instances. Required examples:

```csharp
[Test]
public void LegalTargetRequiresEnemyAliveAndSupportedAltitude()
{
    CombatUnit attacker = CreateUnit("Blue", TeamId.Blue, Altitude.Ground, true, false);
    CombatUnit redGround = CreateUnit("RedGround", TeamId.Red, Altitude.Ground, false, false);
    CombatUnit redAir = CreateUnit("RedAir", TeamId.Red, Altitude.Air, false, false);

    Assert.That(TargetRules.IsLegal(attacker, redGround), Is.True);
    Assert.That(TargetRules.IsLegal(attacker, redAir), Is.False);
}

[Test]
public void DamageUsesMinimumOneAndDeathFiresOnlyOnce()
{
    CombatUnit unit = CreateUnit("Target", TeamId.Red, Altitude.Ground, false, false, 10f, 2f);
    int deathCount = 0;
    unit.Died += _ => deathCount++;

    unit.TakePhysicalDamage(1f);
    unit.TakePhysicalDamage(100f);
    unit.TakePhysicalDamage(100f);

    Assert.That(unit.CurrentHealth, Is.EqualTo(0f));
    Assert.That(unit.IsDead, Is.True);
    Assert.That(deathCount, Is.EqualTo(1));
}

[Test]
public void SelectorReturnsClosestLegalTargetInsideHorizontalRange()
{
    CombatUnit attacker = CreateUnitAt("Blue", TeamId.Blue, Altitude.Ground, Vector3.zero, 5f, true, false);
    CombatUnit near = CreateUnitAt("Near", TeamId.Red, Altitude.Ground, new Vector3(3f, 9f, 0f), 1f, false, false);
    CreateUnitAt("Far", TeamId.Red, Altitude.Ground, new Vector3(4f, 0f, 0f), 1f, false, false);

    Assert.That(TargetSelector.FindNearestInRange(attacker), Is.EqualTo(near));
}
```

Add tests for same-team rejection, dead-target rejection, range boundary inclusion, and stable tie selection by instance ID.

- [ ] **Step 2: Run focused EditMode tests and observe RED**

Run the installed Unity editor in a temporary clean checkout with test filter `ArknightsFrontline.Tests.EditMode.CombatUnitTests|ArknightsFrontline.Tests.EditMode.TargetRulesTests|ArknightsFrontline.Tests.EditMode.TargetSelectorTests`. Expected: compilation failure because combat types do not exist. If LicenseClient fails before compilation, save the exact error and continue only after recording the blocked RED state.

- [ ] **Step 3: Implement the smallest combat data and rules API**

`CombatUnit` must clamp configured values: max health at least `1f`, attack range and interval at least `0f`, defense at least `0f`; set `CurrentHealth = MaxHealth` on `Configure`. `TakePhysicalDamage` subtracts non-negative incoming damage, clamps to zero, and invokes `Died` once when health first reaches zero. It does not calculate armor mitigation.

`TargetRules.IsLegal` must return false for null, self, same-team, dead attacker, dead candidate, or unsupported candidate altitude. `TargetSelector.FindNearestInRange` iterates `Object.FindObjectsByType<CombatUnit>(FindObjectsSortMode.InstanceID)`, filters through `TargetRules`, compares XZ distance using `Vector2.Distance`, includes `distance <= AttackRange`, and returns the first instance-ID-sorted unit at equal distance.

- [ ] **Step 4: Run focused and complete EditMode GREEN**

Run the focused tests and all EditMode tests. Expected: all pass with no compiler warnings or errors; otherwise record the licensing bootstrap block exactly.

- [ ] **Step 5: Commit only Task 1 files and metadata**

```bash
git add Assets/Game/Scripts/Combat/CombatUnit.cs Assets/Game/Scripts/Combat/TargetRules.cs Assets/Game/Scripts/Combat/TargetSelector.cs Assets/Game/Scripts/Combat.meta Assets/Game/Scripts/Combat/CombatUnit.cs.meta Assets/Game/Scripts/Combat/TargetRules.cs.meta Assets/Game/Scripts/Combat/TargetSelector.cs.meta Assets/Tests/EditMode/CombatUnitTests.cs Assets/Tests/EditMode/CombatUnitTests.cs.meta Assets/Tests/EditMode/TargetRulesTests.cs Assets/Tests/EditMode/TargetRulesTests.cs.meta Assets/Tests/EditMode/TargetSelectorTests.cs Assets/Tests/EditMode/TargetSelectorTests.cs.meta
git commit -m "feat: add combat units and target rules"
```

### Task 2：攻击意图解析与基础攻击节奏

**Files:**
- Create: `Assets/Game/Scripts/Combat/CombatCommandResolver.cs`
- Create: `Assets/Game/Scripts/Combat/BasicAttackController.cs`
- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Create: `Assets/Tests/EditMode/CombatCommandResolverTests.cs`
- Create: `Assets/Tests/EditMode/BasicAttackControllerTests.cs`
- Modify: `Assets/Tests/EditMode/PlayerCommandControllerTests.cs`

**Interfaces:**
- Consumes: Task 1 types, `PlayerCommandController.CurrentCommand`, `UnitMotor`, `UnitCommandKind`, and `UnitCommand.TargetObject`.
- Produces: `CombatCommandResolver.Tick(float)`, `CurrentTarget`; `BasicAttackController.SetTarget(CombatUnit)`, `ClearTarget()`, `Tick(float)`, `AttackRequested`.

- [ ] **Step 1: Write the failing resolver and attack-timer tests**

Test a player object with `UnitMotor`, `PlayerCommandController`, `CombatUnit`, `CombatCommandResolver`, and `BasicAttackController` configured through public test setup methods. Required behaviors:

```csharp
[Test]
public void SpecifiedTargetOutsideRangeMovesUntilInRangeThenArmsAttack()
{
    resolver.Tick(0f);
    Assert.That(motor.IsMoving, Is.True);

    player.transform.position = new Vector3(target.transform.position.x - 4f, 0f, 0f);
    resolver.Tick(0f);

    Assert.That(motor.IsMoving, Is.False);
    Assert.That(resolver.CurrentTarget, Is.EqualTo(target));
}

[Test]
public void NearestIntentNeverSetsDestinationWhenNoLegalTargetIsInRange()
{
    controller.Issue(UnitCommand.AttackNearestInRange());
    resolver.Tick(0f);

    Assert.That(resolver.CurrentTarget, Is.Null);
    Assert.That(motor.IsMoving, Is.False);
}

[Test]
public void AttackTimerRaisesOneRequestPerIntervalForInRangeLegalTarget()
{
    attack.SetTarget(target);
    attack.Tick(0f);
    attack.Tick(0.49f);
    attack.Tick(0.01f);

    Assert.That(requestCount, Is.EqualTo(2));
}
```

Add tests that invalidating a target clears it, `Move` and `Stop` clear attack state, and `AttackNearestInRange` picks the nearest legal target currently in range.

Add a `PlayerCommandControllerTests` case that kills the player's `CombatUnit`, calls `controller.Issue(UnitCommand.Move(new Vector3(10f, 0f, 0f)))`, and verifies the motor remains stopped and `CurrentCommand` has not been replaced.

- [ ] **Step 2: Run focused EditMode tests and observe RED**

Run `CombatCommandResolverTests|BasicAttackControllerTests`; expected compilation failure because these components do not exist, or LicenseClient block documented before compilation.

- [ ] **Step 3: Implement resolver and attack timing**

`CombatCommandResolver` requires `CombatUnit`, `UnitMotor`, `PlayerCommandController`, and `BasicAttackController`. On every `Tick`, inspect `CurrentCommand`:

- for `Attack`, resolve `TargetObject.GetComponent<CombatUnit>()`; invalid targets call `ClearCombatTarget`; valid out-of-range targets call `motor.SetDestination(target.transform.position)` and clear `BasicAttackController`; valid in-range targets call `motor.Stop()`, store target, and `SetTarget(target)`;
- for `AttackNearestInRange`, choose via `TargetSelector`; missing target calls `ClearCombatTarget`; found target stops motor and sets it for attack;
- for `Move`, `Stop`, or absent command, call `ClearCombatTarget` without replacing a Move destination.

`ClearCombatTarget` clears the stored target and calls `BasicAttackController.ClearTarget()`. `CombatCommandResolver.Tick` first checks its owner: when dead it stops the motor and clears attack state; it also subscribes to `CombatUnit.Died` to make that stop immediate. `BasicAttackController` subscribes to owner death and clears its target. `BasicAttackController.Tick` only accumulates time while its target remains legal and within the owner’s XZ `AttackRange`; on the first eligible tick and then every `AttackInterval`, it invokes `AttackRequested(owner, target)`. An interval of `0f` may emit at most one request per `Tick` call.

Modify `PlayerCommandController.Issue` to look up an attached `CombatUnit` and return before assigning `CurrentCommand` or touching the motor when that unit is dead. This is the only Stage 3 dependency added to the player command component; it prevents new input from restarting a dead player.

- [ ] **Step 4: Run focused and complete EditMode GREEN**

Run the focused suite and all EditMode tests. Expected: green or a precisely recorded LicenseClient pre-compilation block.

- [ ] **Step 5: Commit only Task 2 files and metadata**

```bash
git add Assets/Game/Scripts/Combat/CombatCommandResolver.cs Assets/Game/Scripts/Combat/CombatCommandResolver.cs.meta Assets/Game/Scripts/Combat/BasicAttackController.cs Assets/Game/Scripts/Combat/BasicAttackController.cs.meta Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/EditMode/CombatCommandResolverTests.cs Assets/Tests/EditMode/CombatCommandResolverTests.cs.meta Assets/Tests/EditMode/BasicAttackControllerTests.cs Assets/Tests/EditMode/BasicAttackControllerTests.cs.meta Assets/Tests/EditMode/PlayerCommandControllerTests.cs
git commit -m "feat: resolve combat commands and attack timing"
```

### Task 3：投射物、伤害与原型场景训练目标

**Files:**
- Create: `Assets/Game/Scripts/Combat/Projectile.cs`
- Modify: `Assets/Game/Scripts/Combat/BasicAttackController.cs`
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs`
- Create: `Assets/Tests/EditMode/ProjectileTests.cs`
- Create: `Assets/Tests/PlayMode/BasicCombatPlayModeTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2 `CombatUnit`, `TargetRules`, `BasicAttackController.AttackRequested`, player scene setup, existing `Targetable` layer and material creation helpers.
- Produces: `Projectile.Initialize(CombatUnit attacker, CombatUnit target, float damage, float speed)`, deterministic `Tick(float)`, scene object `TrainingDummy_Red`.

- [ ] **Step 1: Write the failing projectile and end-to-end tests**

Required EditMode test:

```csharp
[Test]
public void ProjectileDamagesLegalTargetAtArrival()
{
    projectile.Initialize(attacker, target, 12f, 16f);
    projectile.Tick(10f);

    Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 10f));
    Assert.That(projectile.IsFinished, Is.True);
}
```

Configure target defense as `2f`, and add a test where the target dies before arrival; it must not lose additional health.

Required PlayMode test: create a configured player with the Task 2 components and a red ground target initially outside the player’s range. Issue `UnitCommand.Attack(target.gameObject)`, repeatedly call resolver/attack/projectile tick with `0.05f` until the target dies, and assert that the player first moved, then stopped in range, spawned visible projectile objects, and target `IsDead` becomes true.

Extend the existing `PrototypeArenaContainsControllablePlayerAndCameraCenteringTarget` test to require `CombatUnit`, `CombatCommandResolver`, and `BasicAttackController` on `Player_Exusiai`; add a scene test that finds `TrainingDummy_Red`, verifies its red ground `CombatUnit`, collider, and `Targetable` layer.

- [ ] **Step 2: Run the focused test set and observe RED**

Run `ProjectileTests|BasicCombatPlayModeTests|PlayerMovementPlayModeTests`; expected failure because `Projectile`, scene training target, and projectile spawning do not exist, or a recorded LicenseClient pre-compilation block.

- [ ] **Step 3: Implement deterministic projectile and integrate it**

`Projectile.Initialize` stores attacker, target, non-negative damage and positive speed (minimum `0.01f`), starts at attacker position, and sets a small visible sphere renderer. `Tick` moves in XZ toward the target's current position using `Vector3.MoveTowards`; on arrival, it applies `target.TakePhysicalDamage(Mathf.Max(1f, damage - target.Defense))` only if `TargetRules.IsLegal(attacker, target)` is still true, then marks `IsFinished` and destroys its object in runtime. If either unit is invalid/dead before arrival, it finishes without damage.

`BasicAttackController` subscribes its own attack request path in `Awake` and creates a childless `GameObject("Projectile")` with `Projectile` at the attacker position, initializing damage with owner `AttackPower` and speed `16f`. Keep `AttackRequested` public for Task 2 tests.

Extend `PrototypeSceneBuilder` to create a reusable yellow projectile material, add combat components/configuration to player (`Blue`, ground, max health `100`, attack `12`, defense `2`, range `6`, interval `0.5`, attacks ground and air), and create `TrainingDummy_Red` at `(20, 1, 0)` as a red cube in `Targetable` layer (`Red`, ground, max health `40`, attack `0`, defense `2`, range `0`, interval `0`, no attack capabilities). Do not add combat behavior to towers.

- [ ] **Step 4: Run full GREEN and manual gate**

Run full EditMode and PlayMode suites in the clean checkout. Then use `Arknights Frontline → Build Prototype Arena`, enter Play Mode, and verify: direct right-click or Q+left target pursuit; visible projectile hits; training target dies and cannot be attacked again; Q+left ground/wall/empty does not move to the click position. If Unity’s licensing client blocks batch tests, retain the exact log and perform only the manual gate after editor startup is available.

- [ ] **Step 5: Commit only Task 3 files and necessary metadata**

```bash
git add Assets/Game/Scripts/Combat/Projectile.cs Assets/Game/Scripts/Combat/Projectile.cs.meta Assets/Game/Scripts/Combat/BasicAttackController.cs Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/EditMode/ProjectileTests.cs Assets/Tests/EditMode/ProjectileTests.cs.meta Assets/Tests/PlayMode/BasicCombatPlayModeTests.cs Assets/Tests/PlayMode/BasicCombatPlayModeTests.cs.meta Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs
git commit -m "feat: add basic projectiles and training combat scene"
```

## Plan self-review

- Spec coverage: Task 1 supplies all unit, target and death rules; Task 2 maps both Stage 2 attack intentions into chase/attack or no action; Task 3 supplies deterministic projectiles, physical damage and the approved training target.
- Scope: no task introduces tower combat, minions, computer behavior, skills, multi-hit attacks, slowing, respawn, UI or data-driven combat abstractions.
- Type consistency: every Task 2/3 dependency (`CombatUnit`, `TargetRules`, `TargetSelector`, `CombatCommandResolver`, `BasicAttackController`, `Projectile`) is created by the task that precedes its first consumer.
- Placeholder scan: no TBD/TODO or unspecified behavior is present; LicenseClient handling is an explicit environment contingency, not an implementation requirement.
