# Stage 5 Exusiai Skills Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有单线原型中实现能天使 Rank III 的 W 扫射、E 冲锋、R 过载、多段攻击转移、减速、输入仲裁和技能 HUD，并保持阶段 1–4 的移动、攻击、兵线、防御塔和死亡规则不回退。

**Architecture:** 采用“方案 1.5”：`ExusiaiSkillController` 保存能天使专属组合规则，冷却、属性修正、攻击序列、冲刺和状态效果各自由小型可复用组件承担。`BasicAttackController` 继续决定何时开始一轮普攻，`AttackSequenceExecutor` 决定这一轮如何逐发生成投射物；`PlayerCommandController` 通过窄接口把有冲突的输入优先交给技能状态机。

**Tech Stack:** Unity 6.3 LTS（6000.3.15f1）、C# 9 花括号命名空间、Unity Input System 1.17.0、Unity UI、URP、NUnit、Unity Test Framework、macOS。

## Global Constraints

- 权威规格是 `docs/superpowers/specs/2026-09-20-stage-5-exusiai-skills-design.md`；总设计 `docs/superpowers/specs/2026-08-25-3v3-single-lane-graybox-design.md` 作为阶段间约束。
- 按用户要求直接在 `dev` 分支开发，不创建额外 worktree；每个任务提交前只暂存任务列出的文件。
- 保留工作区中用户持有的 Packages、ProjectSettings、日志、`docs/operator-profile/`、`docs/总纲.md` 和其他无关未提交文件。
- 允许确定性构建器覆盖并提交 `Assets/Game/Scenes/PrototypeArena.unity`；除此之外不得手工编辑场景 YAML。
- 玩家基础值保持：生命 1000、基础攻击力 50、基础移速 5、攻击范围 6、基础攻击间隔 0.50 秒。
- W：3 次完整普攻后就绪；普通状态 3 发、R 中 5 发；每发倍率 1.45；最后一发附加实际目标已损生命 8%。
- E：普通状态 4 发、R 中 5 发；每发倍率 1.25；减速 30% 持续 2 秒；释放后 0.25 秒接收一次右键冲刺指令；最大距离 7、速度 14。
- R：初次等待 10 秒、持续 10 秒、完整冷却 30 秒；面板攻击力乘 1.10、普通移速乘 1.08、攻击间隔加算 -0.22 秒。
- 面板攻击力修正与技能伤害倍率分乘区；伤害公式为 `max(1, 当前面板攻击力 × 技能倍率 + 额外原始伤害 - 防御)`，内部保留两位小数。
- 连射弹次间隔固定 0.05 秒；上一轮未结束时不得开启下一轮。
- 现有“首次锁定立即攻击”和“存活目标离开射程即中断手动攻击，重新入射程不恢复”规则必须保留。
- 不实现技能等级、天赋、战利品、E 刷新、R 延长、电脑干员、撤退、重新部署、统计或通用技能图编辑器。
- 所有新增 C# 文件使用花括号命名空间；运行时代码放在 `Assets/Game/Scripts`，测试放在 `Assets/Tests`。
- 每个功能按 TDD 执行：先添加聚焦失败测试，记录 RED，再写最小实现，最后运行聚焦测试和完整 EditMode 回归。
- 当前已知基线是 EditMode 89/89 通过；完整 PlayMode 曾存在 27/35 的旧场景/环境失败。阶段 5 新增和受影响的 PlayMode 测试必须全部通过，且不得增加基线外失败；未取得证据时不得宣称完整 PlayMode 全绿。
- 若 Unity Licensing Client 在编译前失败，保留准确日志并把该次运行标为环境阻塞，不得把“未执行”写成“通过”。

---

## File Structure

- `Assets/Game/Scripts/Combat/UnitStatModifiers.cs`：按来源保存攻击力、移速和攻击间隔修正。
- `Assets/Game/Scripts/Combat/TimedStatModifierController.cs`：计时并刷新 E 的移动减速。
- `Assets/Game/Scripts/Combat/PhysicalDamagePayload.cs`：描述投射物命中时的伤害倍率、已损生命附伤和减速。
- `Assets/Game/Scripts/Combat/AttackSequencePlan.cs`：一轮多段攻击的不可变快照。
- `Assets/Game/Scripts/Combat/AttackSequenceExecutor.cs`：逐发执行、目标转移、射程中断和序列完成事件。
- `Assets/Game/Scripts/Skills/SkillCountdown.cs`：无 Unity 生命周期依赖的冷却/持续时间倒计时。
- `Assets/Game/Scripts/Skills/DashPathResolver.cs`：把右键位置截断到 7 米、场地边界和障碍物前。
- `Assets/Game/Scripts/Skills/SkillDashController.cs`：以固定速度 14 执行不可被普通指令打断的直线冲刺。
- `Assets/Game/Scripts/Skills/IPlayerSkillInputHandler.cs`：玩家命令层与技能状态机之间的窄输入仲裁接口。
- `Assets/Game/Scripts/Skills/ExusiaiSkillController.cs`：W/E/R 状态、组合、冷却、快照和生命周期。
- `Assets/Game/Scripts/Skills/ExusiaiSkillIndicator.cs`：攻击点、锁定目标、冲刺范围、路径和落点几何提示。
- `Assets/Game/Scripts/Skills/SkillHudPresenter.cs`：底部 W/E/R 技能槽与文本刷新。
- `Assets/Game/Scripts/Combat/CombatUnit.cs`：保留基础值并通过属性修正层暴露有效攻击力/间隔。
- `Assets/Game/Scripts/Movement/UnitMotor.cs`：通过属性修正层使用有效普通移速。
- `Assets/Game/Scripts/Combat/Projectile.cs`：在命中时按 `PhysicalDamagePayload` 结算伤害和减速。
- `Assets/Game/Scripts/Combat/TargetSelector.cs`：新增“自身射程内、距离点击点最近”的目标选择。
- `Assets/Game/Scripts/Combat/BasicAttackController.cs`：可选接入多段执行器与普攻计划提供器。
- `Assets/Game/Scripts/Commands/PlayerCommandController.cs`：把 E/R 与冲突鼠标输入路由给技能处理器。
- `Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs`：E 瞄准时隐藏 A 键攻击移动范围提示。
- `Assets/Game/Scripts/Arena/MatchOutcomeController.cs`：比赛结束时停止技能、攻击序列、冲刺和减速计时。
- `Assets/Game/Editor/PrototypeSceneBuilder.cs`：确定性创建所有阶段 5 组件、HUD 和引用。
- `Assets/Game/Scenes/PrototypeArena.unity`：由构建器重建的可人工验收场景。

---

### Task 1: Add reusable countdowns and source-keyed stat modifiers

**Files:**
- Create: `Assets/Game/Scripts/Skills/SkillCountdown.cs`
- Create: `Assets/Game/Scripts/Combat/UnitStatModifiers.cs`
- Modify: `Assets/Game/Scripts/Combat/CombatUnit.cs`
- Modify: `Assets/Game/Scripts/Movement/UnitMotor.cs`
- Create: `Assets/Tests/EditMode/SkillCountdownTests.cs`
- Create: `Assets/Tests/EditMode/UnitStatModifiersTests.cs`
- Modify: `Assets/Tests/EditMode/CombatUnitTests.cs`

**Interfaces:**
- Consumes: existing configured base values in `CombatUnit` and `UnitMotor`.
- Produces: `SkillCountdown.Reset(float)`, `Start(float)`, `Tick(float)`, `Remaining`, `IsReady`; `UnitStatModifiers.SetAttackPowerMultiplier(string, float)`, `SetMovementSpeedMultiplier(string, float)`, `SetAttackIntervalOffset(string, float)`, `RemoveSource(string)`, `Clear()`, `ApplyAttackPower(float)`, `ApplyMovementSpeed(float)`, `ApplyAttackInterval(float)`; `CombatUnit.BaseAttackPower`, `BaseAttackInterval`; `UnitMotor.BaseMovementSpeed`, `MovementSpeed`.

- [ ] **Step 1: Write the failing timer and modifier tests**

Create `SkillCountdownTests` with these exact boundaries:

```csharp
[Test]
public void CountdownClampsAtZeroAndReportsReady()
{
    SkillCountdown timer = new SkillCountdown();
    timer.Reset(10f);
    timer.Tick(9.9f);
    Assert.That(timer.IsReady, Is.False);
    timer.Tick(0.1f);
    Assert.That(timer.Remaining, Is.EqualTo(0f));
    Assert.That(timer.IsReady, Is.True);
}
```

Create `UnitStatModifiersTests` proving keyed replacement, removal and the approved formulas:

```csharp
[Test]
public void RAndSlowUseSeparateApprovedOperations()
{
    UnitStatModifiers modifiers = CreateModifiers();
    modifiers.SetAttackPowerMultiplier("Exusiai.R", 1.10f);
    modifiers.SetMovementSpeedMultiplier("Exusiai.R", 1.08f);
    modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
    modifiers.SetAttackIntervalOffset("Exusiai.R", -0.22f);

    Assert.That(modifiers.ApplyAttackPower(50f), Is.EqualTo(55f).Within(0.001f));
    Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.78f).Within(0.001f));
    Assert.That(modifiers.ApplyAttackInterval(0.5f), Is.EqualTo(0.28f).Within(0.001f));
}

[Test]
public void ReusingSourceReplacesInsteadOfStacking()
{
    UnitStatModifiers modifiers = CreateModifiers();
    modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
    modifiers.SetMovementSpeedMultiplier("Exusiai.E.Slow", 0.70f);
    Assert.That(modifiers.ApplyMovementSpeed(5f), Is.EqualTo(3.5f).Within(0.001f));
}
```

Extend `CombatUnitTests` to add `UnitStatModifiers`, configure attack 50/interval 0.5, apply R, and assert `BaseAttackPower == 50`, `AttackPower == 55`, `BaseAttackInterval == 0.5`, `AttackInterval == 0.28`.

- [ ] **Step 2: Run focused EditMode RED**

Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testFilter "ArknightsFrontline.Tests.EditMode.SkillCountdownTests|ArknightsFrontline.Tests.EditMode.UnitStatModifiersTests|ArknightsFrontline.Tests.EditMode.CombatUnitTests" -testResults TestResults/stage5-task1-red.xml -logFile TestResults/stage5-task1-red.log -quit
```

Expected: compilation fails because `SkillCountdown`, `UnitStatModifiers`, and the base/effective properties do not exist.

- [ ] **Step 3: Implement countdown and source-keyed modifiers**

Implement `SkillCountdown` as a plain sealed class; negative inputs and negative tick deltas clamp to zero:

```csharp
public sealed class SkillCountdown
{
    public float Remaining { get; private set; }
    public bool IsReady => Remaining <= 0f;

    public void Reset(float delay) => Remaining = Mathf.Max(0f, delay);
    public void Start(float duration) => Remaining = Mathf.Max(0f, duration);
    public void Tick(float deltaTime) => Remaining = Mathf.Max(0f, Remaining - Mathf.Max(0f, deltaTime));
}
```

Implement `UnitStatModifiers` with three `Dictionary<string, float>` collections. Setters reject null/empty source with `ArgumentException`, clamp multipliers to at least zero, and replace an existing key. Apply methods must be:

```csharp
public float ApplyAttackPower(float baseValue) => Mathf.Max(0f, baseValue * Product(attackPowerMultipliers));
public float ApplyMovementSpeed(float baseValue) => Mathf.Max(0f, baseValue * Product(movementSpeedMultipliers));
public float ApplyAttackInterval(float baseValue) => Mathf.Max(0.05f, baseValue + Sum(attackIntervalOffsets));
```

`RemoveSource` removes the key from all three dictionaries; `Clear` empties all three.

In `CombatUnit`, rename serialized storage only conceptually—not in YAML—to keep fields `attackPower` and `attackInterval`; add lazy `GetComponent<UnitStatModifiers>()` lookup and expose:

```csharp
public float BaseAttackPower => attackPower;
public float AttackPower => Modifiers == null ? attackPower : Modifiers.ApplyAttackPower(attackPower);
public float BaseAttackInterval => attackInterval;
public float AttackInterval => Modifiers == null ? attackInterval : Modifiers.ApplyAttackInterval(attackInterval);
```

In `UnitMotor`, expose `BaseMovementSpeed`, compute `MovementSpeed` through the same sibling component, and replace `movementSpeed * deltaTime` inside `Tick` with `MovementSpeed * deltaTime`.

- [ ] **Step 4: Run focused and full EditMode GREEN**

Run the Task 1 filter, then run all EditMode tests. Expected: Task 1 suites pass and the existing 89-test baseline has no regression.

- [ ] **Step 5: Commit Task 1**

```bash
git add Assets/Game/Scripts/Skills.meta Assets/Game/Scripts/Skills/SkillCountdown.cs Assets/Game/Scripts/Skills/SkillCountdown.cs.meta Assets/Game/Scripts/Combat/UnitStatModifiers.cs Assets/Game/Scripts/Combat/UnitStatModifiers.cs.meta Assets/Game/Scripts/Combat/CombatUnit.cs Assets/Game/Scripts/Movement/UnitMotor.cs Assets/Tests/EditMode/SkillCountdownTests.cs Assets/Tests/EditMode/SkillCountdownTests.cs.meta Assets/Tests/EditMode/UnitStatModifiersTests.cs Assets/Tests/EditMode/UnitStatModifiersTests.cs.meta Assets/Tests/EditMode/CombatUnitTests.cs
git commit -m "feat: add reusable skill timers and stat modifiers"
```

---

### Task 2: Add point-relative targeting, damage payloads, and refresh-only slows

**Files:**
- Create: `Assets/Game/Scripts/Combat/PhysicalDamagePayload.cs`
- Create: `Assets/Game/Scripts/Combat/TimedStatModifierController.cs`
- Modify: `Assets/Game/Scripts/Combat/Projectile.cs`
- Modify: `Assets/Game/Scripts/Combat/TargetSelector.cs`
- Modify: `Assets/Tests/EditMode/ProjectileTests.cs`
- Modify: `Assets/Tests/EditMode/TargetSelectorTests.cs`
- Create: `Assets/Tests/EditMode/TimedStatModifierControllerTests.cs`

**Interfaces:**
- Consumes: Task 1 `UnitStatModifiers`; existing `CombatUnit`, `TargetRules`, projectile speed 16.
- Produces: immutable `PhysicalDamagePayload`; new `Projectile.Initialize(CombatUnit, CombatUnit, PhysicalDamagePayload, float)` while retaining the old overload; `TimedStatModifierController.ApplyMovementSlow(string, float, float)`, `Tick(float)`, `StopForMatch()`; `TargetSelector.FindNearestInRangeFromPoint(CombatUnit, Vector3)`.

- [ ] **Step 1: Write failing target, damage, and slow tests**

Add a target selector test where the attacker stands at zero, enemies are at `(2,0,0)` and `(4,0,0)`, and the click point is `(5,0,0)`; `FindNearestInRangeFromPoint` must return the enemy at 4 while both remain inside the attacker's range.

Add these projectile assertions with target defense 2:

```csharp
[Test]
public void PayloadUsesPanelAttackThenSkillMultiplierAndDefenseOnce()
{
    Projectile projectile = CreateProjectile();
    PhysicalDamagePayload payload = new PhysicalDamagePayload(55f, 1.45f, 0f, 1f, 0f);
    projectile.Initialize(attacker, target, payload, 16f);
    projectile.Tick(10f);
    Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth - 77.75f).Within(0.001f));
}

[Test]
public void LastShotReadsActualTargetsMissingHealthAtImpact()
{
    target.TakePhysicalDamage(200f);
    PhysicalDamagePayload payload = new PhysicalDamagePayload(50f, 1.45f, 0.08f, 1f, 0f);
    projectile.Initialize(attacker, target, payload, 16f);
    projectile.Tick(10f);
    Assert.That(target.CurrentHealth, Is.EqualTo(711.5f).Within(0.001f));
}
```

The second target starts at 1000 health and defense 2: raw skill damage is 72.5, missing-health addition is 16, final damage is 86.5.

Create `TimedStatModifierControllerTests` that applies source `Exusiai.E.Slow` twice, ticks 1.5 seconds between applications, proves speed remains `5 × 0.70` instead of stacking, then ticks 2 seconds and proves speed returns to 5.

- [ ] **Step 2: Run focused EditMode RED**

Run the three affected test classes. Expected: missing payload, point selector, and timed modifier APIs.

- [ ] **Step 3: Implement damage payload and impact effects**

Define the complete immutable payload contract:

```csharp
public readonly struct PhysicalDamagePayload
{
    public PhysicalDamagePayload(
        float attackPower,
        float damageMultiplier,
        float missingHealthRatio,
        float movementSlowMultiplier,
        float slowDuration)
    {
        AttackPower = Mathf.Max(0f, attackPower);
        DamageMultiplier = Mathf.Max(0f, damageMultiplier);
        MissingHealthRatio = Mathf.Max(0f, missingHealthRatio);
        MovementSlowMultiplier = Mathf.Clamp01(movementSlowMultiplier);
        SlowDuration = Mathf.Max(0f, slowDuration);
    }

    public float AttackPower { get; }
    public float DamageMultiplier { get; }
    public float MissingHealthRatio { get; }
    public float MovementSlowMultiplier { get; }
    public float SlowDuration { get; }
}
```

Keep `Projectile.Initialize(attacker, target, float damage, float speed)` by wrapping the raw damage as attack power with multiplier 1. Add the payload overload. On legal arrival calculate:

```csharp
float missingHealth = Mathf.Max(0f, target.MaxHealth - target.CurrentHealth);
float rawDamage = payload.AttackPower * payload.DamageMultiplier
    + missingHealth * payload.MissingHealthRatio;
float resolvedDamage = Mathf.Round(Mathf.Max(1f, rawDamage - target.Defense) * 100f) / 100f;
target.TakePhysicalDamage(resolvedDamage);
```

Only if the target survives and `SlowDuration > 0` and `MovementSlowMultiplier < 1`, get or add `TimedStatModifierController` and apply source `Exusiai.E.Slow`.

`TimedStatModifierController` gets or adds `UnitStatModifiers` in `Awake`. Store one remaining duration per source; repeated application replaces multiplier and resets the full duration. `Tick` decrements durations, removes expired sources from both its dictionary and `UnitStatModifiers`; `StopForMatch` removes all owned sources and disables further ticking.

Implement `FindNearestInRangeFromPoint` by retaining the existing owner-range legality filter but minimizing XZ distance from candidate to the supplied point. Iterate `FindObjectsSortMode.InstanceID` so equal-distance selection stays deterministic.

- [ ] **Step 4: Run focused and full EditMode GREEN**

Expected: existing raw-damage projectile tests still pass through the compatibility overload; new multiplier, missing-health, slow refresh, and clicked-point selection tests pass.

- [ ] **Step 5: Commit Task 2**

```bash
git add Assets/Game/Scripts/Combat/PhysicalDamagePayload.cs Assets/Game/Scripts/Combat/PhysicalDamagePayload.cs.meta Assets/Game/Scripts/Combat/TimedStatModifierController.cs Assets/Game/Scripts/Combat/TimedStatModifierController.cs.meta Assets/Game/Scripts/Combat/Projectile.cs Assets/Game/Scripts/Combat/TargetSelector.cs Assets/Tests/EditMode/ProjectileTests.cs Assets/Tests/EditMode/TargetSelectorTests.cs Assets/Tests/EditMode/TimedStatModifierControllerTests.cs Assets/Tests/EditMode/TimedStatModifierControllerTests.cs.meta
git commit -m "feat: add skill damage payloads and timed slows"
```

---

### Task 3: Execute deterministic non-overlapping attack sequences

**Files:**
- Create: `Assets/Game/Scripts/Combat/AttackSequencePlan.cs`
- Create: `Assets/Game/Scripts/Combat/AttackSequenceExecutor.cs`
- Modify: `Assets/Game/Scripts/Combat/BasicAttackController.cs`
- Create: `Assets/Tests/EditMode/AttackSequenceExecutorTests.cs`
- Modify: `Assets/Tests/EditMode/BasicAttackControllerTests.cs`

**Interfaces:**
- Consumes: Task 2 payload and target selector; existing `BasicAttackController.AttackRequested` contract.
- Produces: `AttackSequenceKind`, immutable `AttackSequencePlan`, `AttackSequenceExecutor.Configure(CombatUnit)`, `TryStart(AttackSequencePlan, CombatUnit)`, `Cancel()`, `Tick(float)`, `IsRunning`, `event Action<CombatUnit, PhysicalDamagePayload> ShotRequested`, `event Action<AttackSequencePlan, bool> SequenceFinished`; `BasicAttackController.Configure(CombatUnit, AttackSequenceExecutor)`, `SetPlanProvider(Func<AttackSequencePlan>)`.

- [ ] **Step 1: Write failing sequence tests**

Construct an executor with its automatic runtime projectile spawning disabled in EditMode and observe `ShotRequested`. Required tests:

```csharp
[Test]
public void FiveShotPlanFiresImmediatelyThenEveryPointZeroFiveSeconds()
{
    int shots = 0;
    executor.ShotRequested += (_, _) => shots++;
    Assert.That(executor.TryStart(CreatePlan(5), target), Is.True);
    Assert.That(shots, Is.EqualTo(1));
    executor.Tick(0.049f);
    Assert.That(shots, Is.EqualTo(1));
    executor.Tick(0.001f);
    Assert.That(shots, Is.EqualTo(2));
    executor.Tick(0.15f);
    Assert.That(shots, Is.EqualTo(5));
    Assert.That(executor.IsRunning, Is.False);
}
```

Also test:

- `TryStart` returns false while a sequence is running;
- a dead current target causes the next unspawned shot to select the nearest legal target in owner range;
- a living target leaving range interrupts a normal/W/R plan and emits `SequenceFinished(plan, false)`;
- an E plan with `IgnoreRangeAfterStart == true` continues against a living selected target outside range;
- if an E target dies, the next shot retargets from the owner's current position;
- the last shot alone carries `LastShotMissingHealthRatio`;
- `Cancel()` on a running sequence emits exactly one `SequenceFinished(plan, false)` and then clears state.

Extend `BasicAttackControllerTests`: configure an executor and a one-shot plan provider, start the first request, assert a second attack cannot start while the executor reports running, complete it, then assert the next attack starts only when the current effective interval elapses.

- [ ] **Step 2: Run focused EditMode RED**

Expected: sequence types and the new basic-attack overload do not exist.

- [ ] **Step 3: Implement the immutable plan and executor**

Define `AttackSequencePlan` with these exact constructor fields and public properties:

```csharp
public AttackSequencePlan(
    AttackSequenceKind kind,
    int shotCount,
    float shotInterval,
    float attackPower,
    float damageMultiplier,
    float lastShotMissingHealthRatio,
    float movementSlowMultiplier,
    float slowDuration,
    bool countsAsBasicAttack,
    bool ignoreRangeAfterStart)
```

Clamp shot count to at least 1 and interval to at least zero. `AttackSequenceKind` values are `Basic`, `Sweep`, `Charge`, `Overload`, and `OverloadSweep`.

`AttackSequenceExecutor.TryStart` validates owner, plan and initial legal target, emits shot zero immediately, then schedules remaining shots at 0.05-second boundaries. Before every later shot:

1. If the current target is legal and either inside range or `IgnoreRangeAfterStart`, keep it.
2. If it is alive/legal but outside range and range is not ignored, interrupt without retargeting.
3. If it died or became illegal, use `TargetSelector.FindNearestInRange(owner)`.
4. If no replacement exists, interrupt.

Build a fresh `PhysicalDamagePayload` per shot; set missing-health ratio only when `shotsFired == ShotCount - 1`. Fire `ShotRequested(target, payload)` for tests, and in PlayMode create `GameObject("Projectile")`, add `Projectile`, initialize at speed 16. When all planned shots are generated, emit `SequenceFinished(plan, true)` and clear state. Every interruption path, including explicit `Cancel`, emits `SequenceFinished(plan, false)` exactly once.

Modify `BasicAttackController` without breaking minions/towers:

- retain legacy single-projectile behavior when no executor is configured;
- with an executor, obtain the plan from `Func<AttackSequencePlan>` or use a one-shot Basic plan with `owner.AttackPower`;
- do not request a new attack while `executor.IsRunning`;
- use effective `owner.AttackInterval` each tick;
- when its active sequence finishes with `completed == false`, clear the current attack target so `CombatCommandResolver` destroys the engaged manual command on its next tick;
- preserve `AttackRequested` so existing tests and controllers continue to observe attack starts.

- [ ] **Step 4: Run sequence, existing combat, and full EditMode GREEN**

Run `AttackSequenceExecutorTests`, `BasicAttackControllerTests`, `CombatCommandResolverTests`, `LaneMinionControllerTests`, and `TowerCombatControllerTests`, followed by all EditMode. Expected: legacy attackers still create one projectile per attack; only configured player sequences use the executor.

- [ ] **Step 5: Commit Task 3**

```bash
git add Assets/Game/Scripts/Combat/AttackSequencePlan.cs Assets/Game/Scripts/Combat/AttackSequencePlan.cs.meta Assets/Game/Scripts/Combat/AttackSequenceExecutor.cs Assets/Game/Scripts/Combat/AttackSequenceExecutor.cs.meta Assets/Game/Scripts/Combat/BasicAttackController.cs Assets/Tests/EditMode/AttackSequenceExecutorTests.cs Assets/Tests/EditMode/AttackSequenceExecutorTests.cs.meta Assets/Tests/EditMode/BasicAttackControllerTests.cs
git commit -m "feat: execute deterministic multishot attacks"
```

---

### Task 4: Resolve and execute fixed-speed dash movement

**Files:**
- Create: `Assets/Game/Scripts/Skills/DashPathResolver.cs`
- Create: `Assets/Game/Scripts/Skills/SkillDashController.cs`
- Create: `Assets/Tests/EditMode/DashPathResolverTests.cs`
- Create: `Assets/Tests/EditMode/SkillDashControllerTests.cs`

**Interfaces:**
- Consumes: `ArenaLayout`, `UnitMotor`, Unity physics obstacle mask.
- Produces: `DashPathResolver.TryResolve(Vector3, Vector3, float, ArenaLayout, int, float, out Vector3)`; `SkillDashController.Configure(UnitMotor, ArenaLayout, int)`, `TryStart(Vector3)`, `Preview(Vector3, out Vector3)`, `Tick(float)`, `Cancel()`, `IsDashing`, `Destination`, `DashCompleted`.

- [ ] **Step 1: Write failing endpoint and motion tests**

Required endpoint cases:

```csharp
[Test]
public void ClickBeyondSevenIsClampedAlongClickDirection()
{
    bool valid = DashPathResolver.TryResolve(
        Vector3.zero, new Vector3(20f, 0f, 0f), 7f,
        ArenaLayout.CreateDefault(), 0, 0.25f, out Vector3 endpoint);
    Assert.That(valid, Is.True);
    Assert.That(endpoint, Is.EqualTo(new Vector3(7f, 0f, 0f)));
}
```

Also assert a 4-meter click stays 4 meters, a lane-boundary click shortens to `ArenaLayout.Clamp`, and an `Obstacle`-layer cube at x=3 shortens the endpoint to hit distance minus 0.25 clearance. An endpoint shorter than 0.05 must return false.

For `SkillDashController`, configure a motor whose ordinary speed is slowed to 1, start a 7-meter dash, call `Tick(0.25f)`, and assert horizontal travel is exactly 3.5 meters: dash uses speed 14, not ordinary speed. Verify `motor.IsMoving` is false at dash start and `Cancel` stops further movement.

- [ ] **Step 2: Run focused EditMode RED**

Expected: dash resolver and controller types are missing.

- [ ] **Step 3: Implement straight-line endpoint resolution and dash ownership**

`DashPathResolver.TryResolve` must:

1. Flatten click delta to XZ and reject a magnitude below 0.05.
2. Clamp desired distance to `maxDistance` and form the directional endpoint.
3. Apply `ArenaLayout.Clamp` and recompute travel along the original direction so clamping never bends the dash.
4. If `obstacleMask != 0`, perform a raycast from `start + Vector3.up * 0.5f`; shorten to `max(0, hit.distance - clearance)`.
5. Return false when final horizontal distance is below 0.05.

`SkillDashController` stores speed 14 and maximum distance 7 as constants. `TryStart` uses the resolver, calls `motor.Stop()`, stores endpoint and takes movement ownership. `Tick` uses `Vector3.MoveTowards` in XZ with `14 × deltaTime`; reaching destination clears `IsDashing` and invokes `DashCompleted`. `Preview` calls the same resolver without changing state. `Cancel` clears state and leaves the ordinary motor stopped.

- [ ] **Step 4: Run focused and full EditMode GREEN**

Expected: all endpoint and fixed-speed assertions pass; existing `UnitMotor` movement tests remain green.

- [ ] **Step 5: Commit Task 4**

```bash
git add Assets/Game/Scripts/Skills/DashPathResolver.cs Assets/Game/Scripts/Skills/DashPathResolver.cs.meta Assets/Game/Scripts/Skills/SkillDashController.cs Assets/Game/Scripts/Skills/SkillDashController.cs.meta Assets/Tests/EditMode/DashPathResolverTests.cs Assets/Tests/EditMode/DashPathResolverTests.cs.meta Assets/Tests/EditMode/SkillDashControllerTests.cs Assets/Tests/EditMode/SkillDashControllerTests.cs.meta
git commit -m "feat: add bounded skill dash movement"
```

---

### Task 5: Implement the unified Exusiai W/E/R state machine

**Files:**
- Create: `Assets/Game/Scripts/Skills/ExusiaiSkillController.cs`
- Create: `Assets/Tests/EditMode/ExusiaiSkillControllerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4 timers, modifiers, sequence executor, target selector, command controller, and dash controller.
- Produces: `ExusiaiSkillController.Configure(CombatUnit, PlayerCommandController, BasicAttackController, AttackSequenceExecutor, UnitStatModifiers, SkillDashController)`, `Tick(float)`, `BeginChargeTargeting()`, `TryConfirmCharge(Vector3, CombatUnit)`, `TryConsumeDashMove(Vector3)`, `CancelChargeTargeting()`, `TryActivateOverload()`, `ResetForDeployment()`, `StopForMatch()`, `Snapshot`; public state `IsSelectingChargeTarget`, `IsDashWindowOpen`, `IsOverloadActive`, `SelectedChargeTarget`, `BlocksNormalCommands`.

- [ ] **Step 1: Write failing controller tests for initial state and W**

Build one fixture with a player owner, commands, modifiers, executor, attack controller and dash. Required assertions:

```csharp
[Test]
public void DeploymentStartsWAtZeroEReadyAndRAtTenSeconds()
{
    controller.ResetForDeployment();
    Assert.That(controller.Snapshot.SweepProgress, Is.EqualTo(0));
    Assert.That(controller.Snapshot.IsChargeReady, Is.True);
    Assert.That(controller.Snapshot.OverloadCooldown, Is.EqualTo(10f));
}

[Test]
public void FourthBasicAttackConsumesReadyWAndCreatesThreeShotPlan()
{
    CompleteBasicAttack();
    CompleteBasicAttack();
    CompleteBasicAttack();
    AttackSequencePlan plan = RequestNextBasicPlan();
    Assert.That(plan.Kind, Is.EqualTo(AttackSequenceKind.Sweep));
    Assert.That(plan.ShotCount, Is.EqualTo(3));
    Assert.That(plan.DamageMultiplier, Is.EqualTo(1.45f));
    Assert.That(plan.LastShotMissingHealthRatio, Is.EqualTo(0.08f));
}
```

Add cases proving an interrupted sequence does not add W progress, consumed W is not refunded, a completed W attack begins the next cycle at `1/3`, and E plans never alter W progress.

- [ ] **Step 2: Add failing R and combination tests**

Test the 10-second first wait, immediate activation afterward, exact 10-second duration, 30-second cooldown from activation, modifier values, and cleanup. Assert these plan snapshots:

```csharp
AssertPlan(normalR, AttackSequenceKind.Overload, 5, 55f, 1f);
AssertPlan(rSweep, AttackSequenceKind.OverloadSweep, 5, 55f, 1.45f);
Assert.That(rSweep.LastShotMissingHealthRatio, Is.EqualTo(0.08f));
```

Activate R after an E attack plan has started and assert the existing E plan remains 4 shots at attack power 50. Activate R before E confirmation and assert the E plan is 5 shots at attack power 55 and multiplier 1.25.

- [ ] **Step 3: Add failing E targeting and 0.25-second window tests**

Cover all approved transitions:

- `BeginChargeTargeting` succeeds only while E is ready and owner alive;
- a point beyond attack range returns false without cooldown;
- a directly supplied legal in-range target wins over point-nearest selection;
- a ground point chooses the legal owner-range target nearest that point;
- no target still consumes E and opens the window without a sequence;
- left confirmation starts 20-second cooldown immediately;
- entering E targeting does not cancel an existing Move/Attack command; canceling targeting preserves it, while successful left confirmation clears it;
- a right-click at 0.249 seconds starts dash; a click at or after 0.25 does not;
- first window click is consumed, later clicks are not;
- no click closes the window without moving;
- normal E plan is 4 shots, R E plan is 5 shots;
- R remains activatable while selecting E, during the 0.25-second window, and during the dash; it never changes an E plan already created;
- cancel before confirmation costs nothing; cancel after confirmation returns false;
- death and `StopForMatch` cancel sequence, window, dash, modifiers and timers.
- a controller missing any required sibling dependency logs one `MissingReferenceException` naming the GameObject and missing component, disables itself, and does not emit repeated frame errors.

- [ ] **Step 4: Run focused EditMode RED**

Expected: `ExusiaiSkillController` and snapshot API do not exist.

- [ ] **Step 5: Implement one controller with explicit constants and transitions**

Use named constants copied from the spec: W required attacks 3, W multiplier 1.45, missing ratio 0.08, E cooldown 20, window 0.25, E multiplier 1.25, slow multiplier 0.70, slow duration 2, R initial wait 10, cooldown 30, duration 10, attack multiplier 1.10, move multiplier 1.08, interval offset -0.22.

`Configure` must validate every dependency, unsubscribe/re-subscribe owner death and executor completion events, register `CreateNextBasicAttackPlan` through `BasicAttackController.SetPlanProvider`, then call `ResetForDeployment`. `Awake` attempts sibling auto-configuration only when every required component exists; otherwise it logs one `MissingReferenceException` containing `gameObject.name` and the missing component name, sets `enabled = false`, and returns before subscribing.

`CreateNextBasicAttackPlan` performs this exact decision:

```csharp
bool consumeSweep = sweepProgress >= 3;
int shots = IsOverloadActive ? 5 : consumeSweep ? 3 : 1;
float multiplier = consumeSweep ? 1.45f : 1f;
AttackSequenceKind kind = consumeSweep
    ? (IsOverloadActive ? AttackSequenceKind.OverloadSweep : AttackSequenceKind.Sweep)
    : (IsOverloadActive ? AttackSequenceKind.Overload : AttackSequenceKind.Basic);
if (consumeSweep) sweepProgress = 0;
return new AttackSequencePlan(
    kind, shots, 0.05f, owner.AttackPower, multiplier,
    consumeSweep ? 0.08f : 0f, 1f, 0f, true, false);
```

On `SequenceFinished(plan, completed)`, increment W once only when `completed && plan.CountsAsBasicAttack`, clamped to 3.

E confirmation first checks the clicked point's XZ distance against `owner.AttackRange`. It chooses a legal supplied direct target or calls `FindNearestInRangeFromPoint`; then cancels the current command and running normal sequence, snapshots an E plan if a target exists, starts the 20-second cooldown, and opens the 0.25-second dash window. `TryConsumeDashMove` accepts only the first click while remaining time is strictly greater than zero, closes the window, and calls `dash.TryStart` even if the attack target was null.

R activation starts cooldown and duration together and sets three modifiers under the single source `Exusiai.R`. Ending R removes that source. An E or basic plan records `owner.AttackPower` at plan creation, so later modifier changes cannot alter it.

Expose an immutable snapshot with W progress/ready, E phase/cooldown, R active/duration/cooldown. Do not expose writable timers.

- [ ] **Step 6: Run focused and full EditMode GREEN**

Expected: all W/E/R transitions and combinations pass; full EditMode remains green.

- [ ] **Step 7: Commit Task 5**

```bash
git add Assets/Game/Scripts/Skills/ExusiaiSkillController.cs Assets/Game/Scripts/Skills/ExusiaiSkillController.cs.meta Assets/Tests/EditMode/ExusiaiSkillControllerTests.cs Assets/Tests/EditMode/ExusiaiSkillControllerTests.cs.meta
git commit -m "feat: implement Exusiai skill state machine"
```

---

### Task 6: Arbitrate skill input against movement and attack commands

**Files:**
- Create: `Assets/Game/Scripts/Skills/IPlayerSkillInputHandler.cs`
- Modify: `Assets/Game/Scripts/Skills/ExusiaiSkillController.cs`
- Modify: `Assets/Game/Scripts/Commands/PlayerCommandController.cs`
- Modify: `Assets/Tests/EditMode/PlayerCommandControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: Task 5 controller transitions and existing `GameInputActions`.
- Produces: `IPlayerSkillInputHandler`; `PlayerCommandController.SetSkillInputHandler(IPlayerSkillInputHandler)`; Exusiai controller implements the interface.

- [ ] **Step 1: Define the failing routing tests with a recording handler**

The interface must use world data rather than Input System callback objects:

```csharp
public interface IPlayerSkillInputHandler
{
    bool BlocksAttackMove { get; }
    bool BlocksNormalCommands { get; }
    void HandleSkill2();
    void HandleSkill3();
    bool TryHandleConfirm(Vector3 worldPoint, GameObject hitObject);
    bool TryHandleMoveClick(Vector3 worldPoint);
    bool TryHandleCancel();
    void HandleStop();
}
```

Use a test double to assert:

- handled E confirm does not issue attack move;
- handled E cancel consumes the same-frame right click and does not issue Move;
- during the 0.25 window right click reaches `TryHandleMoveClick` and does not become ordinary Move;
- after the window the same click path issues ordinary Move;
- `BlocksAttackMove` prevents arming A;
- `BlocksNormalCommands` consumes move/attack clicks while dash is active;
- S always calls `HandleStop` and then issues Stop, but cannot cancel the separate dash controller;
- Skill2 and Skill3 actions call their respective handler methods.

- [ ] **Step 2: Run focused EditMode and input PlayMode RED**

Expected: missing handler interface and routing registration.

- [ ] **Step 3: Route input through the handler before legacy command logic**

Add `SetSkillInputHandler`. Subscribe/unsubscribe `input.Skill2.performed` and `input.Skill3.performed` beside existing actions.

Routing order must be:

1. `OnConfirm`: raycast once; if handler accepts, return; otherwise execute existing A-confirm logic.
2. Queued move click in `Update`: raycast once; if handler accepts, return; if normal commands are blocked, return; otherwise execute existing target/move handling.
3. `OnCancel`: ask handler first; when accepted, set `consumedCancelControl`/frame and clear matching queued move click exactly as existing A-cancel does.
4. `OnAttackMove`: return while handler blocks attack move; otherwise preserve current behavior.
5. `OnStop`: call handler, cancel A state, then issue Stop.
6. Skill2/Skill3: call the handler and never construct commands directly.

Make `ExusiaiSkillController` implement the interface:

- Skill2 toggles E targeting when ready; pressing E while targeting cancels.
- Skill3 tries R immediately.
- Confirm calls `TryConfirmCharge` with a direct `CombatUnit` only when the hit object is targetable/legal.
- Confirm returns true without issuing a normal command while the dash window is open or a dash is active.
- Move click calls `TryConsumeDashMove` during the window, consumes all clicks while dashing, otherwise returns false.
- Cancel only succeeds before E confirmation.
- `BlocksAttackMove` is true while selecting E, in the dash window, or dashing; `BlocksNormalCommands` is true only while dashing.

- [ ] **Step 4: Run focused and full command regressions GREEN**

Run `PlayerCommandControllerTests`, `PlayerCommandInputPlayModeTests`, `CombatCommandResolverTests`, and all EditMode. Expected: existing right-click/A behavior is unchanged when no handler is configured.

- [ ] **Step 5: Commit Task 6**

```bash
git add Assets/Game/Scripts/Skills/IPlayerSkillInputHandler.cs Assets/Game/Scripts/Skills/IPlayerSkillInputHandler.cs.meta Assets/Game/Scripts/Skills/ExusiaiSkillController.cs Assets/Game/Scripts/Commands/PlayerCommandController.cs Assets/Tests/EditMode/PlayerCommandControllerTests.cs Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs
git commit -m "feat: arbitrate Exusiai skill input"
```

---

### Task 7: Present the three-slot HUD and in-world skill indicators

**Files:**
- Create: `Assets/Game/Scripts/Skills/SkillHudPresenter.cs`
- Create: `Assets/Game/Scripts/Skills/ExusiaiSkillIndicator.cs`
- Modify: `Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs`
- Create: `Assets/Tests/EditMode/SkillHudPresenterTests.cs`
- Create: `Assets/Tests/EditMode/ExusiaiSkillIndicatorTests.cs`
- Modify: `Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs`

**Interfaces:**
- Consumes: Task 5 controller snapshot, command pointer hit, Task 4 dash preview.
- Produces: `SkillHudPresenter.Configure(ExusiaiSkillController)`, `Refresh()`, `WLabel`, `ELabel`, `RLabel`; `ExusiaiSkillIndicator.Configure(CombatUnit, ExusiaiSkillController, PlayerCommandController, SkillDashController)`, `Refresh()`, `Mode`, `DisplayedEndpoint`; `CommandFeedbackPresenter.ConfigureSkillController(ExusiaiSkillController)`.

- [ ] **Step 1: Write failing text-state and indicator-mode tests**

For the HUD, drive a controller snapshot and assert exact user-visible states:

```csharp
Assert.That(hud.WLabel, Is.EqualTo("W  2/3"));
Assert.That(hud.ELabel, Is.EqualTo("E  READY"));
Assert.That(hud.RLabel, Is.EqualTo("R  10.0"));
```

Then assert W ready becomes `W  READY`, E targeting becomes `E  SELECT TARGET`, dash window becomes `E  MOVE!`, E cooldown uses one decimal, and active R shows `R  ACTIVE 10.0` before falling back to cooldown.

For the indicator, assert modes `None`, `ChargeTargeting`, and `DashWindow`; attack targeting uses radius `owner.AttackRange`, dash window uses radius 7, a cursor beyond 7 produces a displayed endpoint exactly 7 meters away, and `OnDisable` hides every renderer.

- [ ] **Step 2: Run focused EditMode RED**

Expected: presenter and indicator types are absent.

- [ ] **Step 3: Build runtime UI without external art**

`SkillHudPresenter.Configure` receives an existing RectTransform root or creates `SkillHud` under the nearest Canvas. Create three 120×64 Image-backed slots anchored bottom-center with 8-pixel spacing. Use `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`, white text, blue ready border, gray cooldown overlay, and no imported texture assets.

`Refresh` reads one snapshot and updates labels exactly as tested. Expose labels as read-only strings so EditMode tests do not depend on rendered pixels. Disable interaction and raycast targets on all HUD graphics.

- [ ] **Step 4: Build line-renderer indicators and suppress A feedback**

`ExusiaiSkillIndicator` creates and owns:

- one 65-point loop LineRenderer for attack/dash range;
- one LineRenderer for the dash arrow;
- one 33-point loop for target or endpoint marker.

During E targeting, show attack radius at height 0.05, color blue when pointer point is inside range and red otherwise, and center the lock marker on `SelectedChargeTarget` when non-null. During the dash window, show radius 7, call `dash.Preview(pointerPoint, out endpoint)`, draw arrow to the actual endpoint, and use red only when no effective movement is possible. Right click/dash start hides the live preview; keep a fading path for at most 0.15 seconds.

Modify `CommandFeedbackPresenter.UpdateRangeRing` so its A ring is visible only when attack move is held and the skill controller is not selecting E, not in dash window, and not dashing. Preserve legal-target cursor behavior outside skill targeting.

- [ ] **Step 5: Run focused and full EditMode GREEN**

Expected: exact labels and modes pass; existing `PlayerCommandInputPlayModeTests` for `CommandFeedbackPresenter` still pass when no skill controller is configured.

- [ ] **Step 6: Commit Task 7**

```bash
git add Assets/Game/Scripts/Skills/SkillHudPresenter.cs Assets/Game/Scripts/Skills/SkillHudPresenter.cs.meta Assets/Game/Scripts/Skills/ExusiaiSkillIndicator.cs Assets/Game/Scripts/Skills/ExusiaiSkillIndicator.cs.meta Assets/Game/Scripts/Commands/CommandFeedbackPresenter.cs Assets/Tests/EditMode/SkillHudPresenterTests.cs Assets/Tests/EditMode/SkillHudPresenterTests.cs.meta Assets/Tests/EditMode/ExusiaiSkillIndicatorTests.cs Assets/Tests/EditMode/ExusiaiSkillIndicatorTests.cs.meta Assets/Tests/PlayMode/PlayerCommandInputPlayModeTests.cs
git commit -m "feat: present Exusiai skill HUD and targeting"
```

---

### Task 8: Wire match lifecycle, rebuild the arena, and verify Stage 5 end to end

**Files:**
- Modify: `Assets/Game/Scripts/Arena/MatchOutcomeController.cs`
- Modify: `Assets/Game/Editor/PrototypeSceneBuilder.cs`
- Modify: `Assets/Tests/EditMode/MatchOutcomeControllerTests.cs`
- Modify: `Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs`
- Create: `Assets/Tests/PlayMode/ExusiaiSkillsPlayModeTests.cs`
- Modify generated scene: `Assets/Game/Scenes/PrototypeArena.unity`

**Interfaces:**
- Consumes: every earlier task's final API and current scene builder.
- Produces: a fully configured saved scene, match-stop cleanup, and Stage 5 PlayMode acceptance coverage.

- [ ] **Step 1: Write failing scene and match-stop tests**

Extend `PrototypeArenaContainsRequiredRoots` to assert `Player_Exusiai` has:

```csharp
Assert.That(player.GetComponent<UnitStatModifiers>(), Is.Not.Null);
Assert.That(player.GetComponent<AttackSequenceExecutor>(), Is.Not.Null);
Assert.That(player.GetComponent<SkillDashController>(), Is.Not.Null);
Assert.That(player.GetComponent<ExusiaiSkillController>(), Is.Not.Null);
Assert.That(player.GetComponent<ExusiaiSkillIndicator>(), Is.Not.Null);
Assert.That(Object.FindFirstObjectByType<SkillHudPresenter>(), Is.Not.Null);
```

Keep and reassert health 1000, attack 50, interval 0.5, range 6 and scale `(1.6, 2, 1.6)`.

Add a match outcome test that activates R, applies an E slow, begins an attack sequence and dash, destroys a tower, calls settlement, and asserts: skill controller stopped, R modifier removed, executor not running, dash not running, and timed slow removed.

- [ ] **Step 2: Write failing PlayMode skill scenarios**

Create `ExusiaiSkillsPlayModeTests` with deterministic direct method calls rather than simulated keyboard timing. Required scenarios:

1. Three completed normal basic sequences make W ready; the next sequence spawns 3 visible projectiles 0.05 seconds apart.
2. After 10 seconds, R produces 5-shot basics, effective attack power 55, effective interval 0.28, and returns to base after 10 seconds.
3. R+W uses 5 shots with payload attack power 55, multiplier 1.45, and last-shot missing ratio 0.08.
4. Ground-point E chooses the enemy nearest that point; direct-target E takes priority; no-target E still opens the window.
5. E plus a right click during 0.25 seconds moves at speed 14 while remaining projectiles continue; a click beyond 7 ends exactly 7 meters from dash start.
6. E with no right click does not move.
7. A killed E target transfers remaining unspawned shots to the closest legal target; already spawned projectiles retain their original target.
8. A living normal/W/R target leaving range interrupts remaining shots and clears the manual command.

Track and destroy every runtime object in `[UnityTearDown]`; restore `Time.timeScale = 1`.

- [ ] **Step 3: Run focused tests and observe RED before scene wiring**

Run `MatchOutcomeControllerTests`, `ArenaSceneSmokeTests`, and `ExusiaiSkillsPlayModeTests`. Expected: scene component assertions and match cleanup fail before builder/lifecycle changes.

- [ ] **Step 4: Wire match shutdown**

In `MatchOutcomeController.StopCombatProducers`, after existing attack controllers:

```csharp
foreach (ExusiaiSkillController skills in Object.FindObjectsByType<ExusiaiSkillController>(FindObjectsSortMode.None))
{
    skills.StopForMatch();
}
foreach (AttackSequenceExecutor sequence in Object.FindObjectsByType<AttackSequenceExecutor>(FindObjectsSortMode.None))
{
    sequence.Cancel();
}
foreach (SkillDashController dash in Object.FindObjectsByType<SkillDashController>(FindObjectsSortMode.None))
{
    dash.Cancel();
}
foreach (TimedStatModifierController effects in Object.FindObjectsByType<TimedStatModifierController>(FindObjectsSortMode.None))
{
    effects.StopForMatch();
}
```

Keep the existing late same-frame draw settlement and projectile cancellation unchanged.

- [ ] **Step 5: Wire the deterministic scene builder**

Change `CreateUiRoots` to return the Canvas. Ensure an `Obstacle` layer beside Ground and Targetable. In `CreatePlayer`, add and configure components in dependency order:

```csharp
UnitStatModifiers modifiers = player.AddComponent<UnitStatModifiers>();
AttackSequenceExecutor sequence = player.AddComponent<AttackSequenceExecutor>();
sequence.Configure(combatUnit);
SkillDashController dash = player.AddComponent<SkillDashController>();
dash.Configure(motor, ArenaLayout.CreateDefault(), 1 << obstacleLayer);
attack.Configure(combatUnit, sequence);
ExusiaiSkillController skills = player.AddComponent<ExusiaiSkillController>();
skills.Configure(combatUnit, commands, attack, sequence, modifiers, dash);
commands.SetSkillInputHandler(skills);
CommandFeedbackPresenter feedback = player.GetComponent<CommandFeedbackPresenter>();
feedback.ConfigureSkillController(skills);
ExusiaiSkillIndicator indicator = player.AddComponent<ExusiaiSkillIndicator>();
indicator.Configure(combatUnit, skills, commands, dash);
```

Pass `obstacleLayer` into `CreatePlayer`. After Canvas creation, add a `SkillHud` child and `SkillHudPresenter`, then call `Configure(skills)`. Keep all existing player values and all tower/minion setup unchanged.

- [ ] **Step 6: Rebuild and save the scene**

Close the interactive Unity editor first. Run:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath "$PWD" -executeMethod ArknightsFrontline.Editor.PrototypeSceneBuilder.Build -logFile "$PWD/stage5-build-scene.log"
```

Expected: exit code 0, no C# compiler errors, and `PrototypeArena.unity` contains the Stage 5 components. If the Licensing Client is disconnected, open the project once interactively, wait for license/compilation, close it, and rerun the same builder command.

- [ ] **Step 7: Run complete automated verification**

Run full EditMode:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform EditMode -testResults TestResults/stage5-editmode.xml -logFile TestResults/stage5-editmode.log -quit
```

Run focused Stage 5 and affected PlayMode:

```bash
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics -projectPath "$PWD" -runTests -testPlatform PlayMode -testFilter "ArknightsFrontline.Tests.PlayMode.ExusiaiSkillsPlayModeTests|ArknightsFrontline.Tests.PlayMode.ArenaSceneSmokeTests|ArknightsFrontline.Tests.PlayMode.PlayerCommandInputPlayModeTests|ArknightsFrontline.Tests.PlayMode.BasicCombatPlayModeTests" -testResults TestResults/stage5-playmode.xml -logFile TestResults/stage5-playmode.log -quit
```

Expected: full EditMode passes; all filtered PlayMode tests pass. Then run full PlayMode once and compare any failures with the recorded pre-Stage-5 baseline; report exact remaining failures rather than hiding them.

- [ ] **Step 8: Perform the manual acceptance gate**

Open Unity 6000.3.15f1, run `Arknights Frontline → Build Prototype Arena`, enter Play Mode, and verify in this order:

1. W HUD advances `0/3 → 1/3 → 2/3 → READY`; next attack is three shots.
2. At 10 seconds R becomes ready; R shows active duration, produces five-shot attacks, and visibly increases ordinary move/attack pace.
3. Trigger W during R and confirm it remains five shots rather than 7 or 15.
4. Press E, click a legal enemy, and separately click ground near a different enemy; verify the approved target priority.
5. Release E and right-click within 0.25 seconds at 4 meters, beyond 7 meters, and toward arena boundary; verify specified, clamped, and shortened endpoints.
6. Release E without right-click; confirm shots occur but no dash.
7. Use E during R; confirm five shots and that W count does not change.
8. Kill a target during a volley; confirm only unspawned shots transfer.
9. Move a living normal target out of range; confirm the manual attack is destroyed and does not resume on re-entry.
10. End the match by destroying a tower; confirm cooldowns, projectiles, dash, indicators, and stat effects stop with no residual HUD animation.

- [ ] **Step 9: Check diff scope and commit Task 8**

```bash
git diff --check -- Assets/Game/Scripts/Arena/MatchOutcomeController.cs Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/EditMode/MatchOutcomeControllerTests.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Tests/PlayMode/ExusiaiSkillsPlayModeTests.cs Assets/Game/Scenes/PrototypeArena.unity
git add Assets/Game/Scripts/Arena/MatchOutcomeController.cs Assets/Game/Editor/PrototypeSceneBuilder.cs Assets/Tests/EditMode/MatchOutcomeControllerTests.cs Assets/Tests/PlayMode/ArenaSceneSmokeTests.cs Assets/Tests/PlayMode/ExusiaiSkillsPlayModeTests.cs Assets/Tests/PlayMode/ExusiaiSkillsPlayModeTests.cs.meta Assets/Game/Scenes/PrototypeArena.unity
git commit -m "feat: integrate Exusiai skills into prototype arena"
```

---

## Final Review Checklist

- [ ] Compare every section of `2026-09-20-stage-5-exusiai-skills-design.md` to a task and test above; no requirement is left only to manual interpretation.
- [ ] Run `rg -n 'NotImplementedException|throw new Exception' Assets/Game/Scripts Assets/Tests` and remove any unfinished Stage 5 stub.
- [ ] Run `git diff --check` and inspect `git status --short`; unrelated dirty files remain untouched and unstaged.
- [ ] Request a specification-conformance review of Tasks 1–8.
- [ ] Request a separate code-quality review after conformance passes.
- [ ] Re-run full EditMode and focused PlayMode after all review fixes.
- [ ] Record Unity version, exact test counts, focused PlayMode results, full PlayMode baseline comparison, scene build result, and manual acceptance result.
- [ ] Do not declare Stage 5 complete until automated evidence and the user's manual acceptance are both available.
