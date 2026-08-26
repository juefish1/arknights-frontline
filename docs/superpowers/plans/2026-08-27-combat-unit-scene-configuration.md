# Combat Unit Scene Configuration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让原型场景中的战斗单位在进入 Play Mode 后保留基础属性并以满生命开始，使输入命令可以正常驱动移动和攻击。

**Architecture:** `CombatUnit` 将基础配置保存为 Unity 可序列化字段，并在运行时首次启用时用最大生命初始化当前生命。保留 `Configure` 作为测试和运行时构建 API；它同步写入配置字段并重置生命。场景 PlayMode 测试直接验证玩家和训练假人的可用战斗状态。

**Tech Stack:** Unity 6000.3.15f1、C# 9、Unity Test Framework、NUnit。

## Global Constraints

- 不修改用户未提交的 `Assets/Game/Scenes/PrototypeArena.unity`、Packages 或 ProjectSettings。
- 保持 `CombatUnit.Configure(...)` 的现有签名和伤害规则不变。
- 测试应验证 Play Mode 的可观察行为：单位生命和阵营正确，而非检查序列化源文本。
- Unity 批处理测试只能在临时干净 checkout 运行；若 LicenseClient 505 在编译前阻断，记录该环境阻塞，不能宣称测试通过。

---

### Task 1: Persist combat configuration and prove scene startup state

**Files:**
- Modify: `Assets/Game/Scripts/Combat/CombatUnit.cs`
- Modify: `Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs`

**Interfaces:**
- Consumes: scene-builder configuration through `CombatUnit.Configure(TeamId, Altitude, float, float, float, float, float, bool, bool)`.
- Produces: scene-loaded `CombatUnit` instances with configured `Team`, `MaxHealth`, `CurrentHealth`, `AttackRange`, capability flags, and `IsDead == false`.

- [ ] **Step 1: Add a failing scene regression test**

Add the following assertions after loading `PrototypeArena`; they fail against the current code because auto-properties are not serialized and `CurrentHealth` starts at zero:

```csharp
CombatUnit playerUnit = player.GetComponent<CombatUnit>();
Assert.That(playerUnit.Team, Is.EqualTo(TeamId.Blue));
Assert.That(playerUnit.MaxHealth, Is.EqualTo(100f));
Assert.That(playerUnit.CurrentHealth, Is.EqualTo(100f));
Assert.That(playerUnit.IsDead, Is.False);

Assert.That(unit.MaxHealth, Is.EqualTo(40f));
Assert.That(unit.CurrentHealth, Is.EqualTo(40f));
Assert.That(unit.IsDead, Is.False);
```

- [ ] **Step 2: Run the focused PlayMode test and observe RED**

Run the installed Unity editor against `PlayerMovementPlayModeTests` in a temporary clean checkout. Expected: the new assertions fail with default health `0`; if LicenseClient fails before compilation, retain its exact 505 message as the blocked RED result.

- [ ] **Step 3: Serialize configuration and initialize health at runtime**

Replace unbacked auto-properties in `CombatUnit` with private `[SerializeField]` backing fields and read-only public accessors. Use this behavior:

```csharp
private void Awake()
{
    CurrentHealth = MaxHealth;
}

public void Configure(...)
{
    team = configuredTeam;
    maxHealth = Mathf.Max(1f, configuredMaxHealth);
    currentHealth = maxHealth;
    // retain the existing normalisation for defense, range, and interval.
}
```

The serialized fields make the editor-built scene persist configuration; `Awake` resets scene instances to full health every Play Mode entry. Do not serialize transient attack-timer state or alter death event semantics.

- [ ] **Step 4: Run focused verification and inspect the change**

Run the focused PlayMode test in the clean checkout, then run `git diff --check`. Expected: the regression passes, or the LicenseClient 505 is recorded before compilation; whitespace check exits successfully.

- [ ] **Step 5: Commit the scoped repair**

```bash
git add Assets/Game/Scripts/Combat/CombatUnit.cs Assets/Tests/PlayMode/PlayerMovementPlayModeTests.cs
git commit -m "fix: persist combat unit scene configuration"
```

## Plan self-review

- Coverage: the only failure source—scene configuration loss and zero runtime health—is addressed by serialization plus runtime initialization, and tested using actual loaded scene objects.
- Scope: no scene file, input binding, command behavior, combat timing, or unrelated settings are changed.
- Type consistency: existing public `CombatUnit` accessors and `Configure` signature remain available to all Stage 3 callers and tests.
