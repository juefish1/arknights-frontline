# Stage 4 final-fix report

日期：2026-08-27
基线：`4c9fd7a` (`fix: preserve configured camera and tower lifecycle`)

## 修复范围

本轮只处理 Stage 4 最终审查提出的两个 Important 与两个低风险 Minor，不修改玩家输入、场景、Packages、ProjectSettings 或日志。

1. `MatchOutcomeController` 改为两阶段结算：
   - 第一座塔死亡事件同步停止波次生成器、兵线/塔控制器和新的基础攻击；
   - 不在死亡事件中取消已发射投射物；
   - `LateUpdate` 在所有普通 `Update` 完成后汇总双方塔死亡标志，确定胜负或平局，再取消仍在飞行的投射物；
   - 结算待定或已经结束时拒绝重新 `Configure`，避免重开结果。
2. `MinionWaveSpawner` 的自动计时从 `Update` 移到 `LateUpdate`，并在任一塔死亡或已停止时拒绝生成。这样投射物在本帧 `Update` 内击毁塔后，恰好到达 25 秒边界的波次也不会提交。
3. `LaneMinionController` 在每次索敌前先检查 `BasicAttackController.CurrentTarget`；只要该目标仍合法且处于水平攻击范围内，就保持目标并继续原攻击节奏。只有目标失效或离开范围后才重新选最近目标。
4. `LaneMinionController` 与 `TowerCombatController` 增加明确的未配置保护，未完成 `Configure` 时 `Tick` 直接返回，不再解引用空 owner。
5. 保留 `MinionWaveSpawner` 的 `while` 追赶实现，并增加 75 秒一次补齐三波的明确回归。

## 新增回归覆盖

- `BasicCombatPlayModeTests.SameFrameLethalProjectilesResolveDrawAcrossOutcomeUpdateOrder`
  - 使用 `DefaultExecutionOrder(-1000)` 与 `DefaultExecutionOrder(1000)` 的测试驱动器，让两枚真实 `Projectile.Tick` 在同一 Unity 帧中分别位于旧的普通 outcome `Update` 两侧；
  - 断言双方塔均死亡且帧末结果为 `Draw`。
- `BasicCombatPlayModeTests.TowerDeathAtWaveBoundaryPreventsBoundaryWaveRegardlessOfUpdateOrder`
  - 在波次已接近 25 秒时，让补时发生在早期 `Update`、致命投射物发生在晚期 `Update`；
  - 断言塔死亡产生正确胜方且边界波次没有生成。
- `LaneMinionControllerTests.MinionKeepsCurrentTargetAndAttackCadenceWhenCloserEnemyEntersRange`
  - 当前目标仍合法且在范围内时加入更近敌人；
  - 同时断言目标不变、不会产生额外立即首击，并在剩余攻击间隔结束时才发出下一次攻击请求。
- `LaneMinionControllerTests.UnconfiguredMinionControllerTickIsIgnored`。
- `TowerCombatControllerTests.UnconfiguredTowerControllerTickIsIgnored`。
- `MinionWaveSpawnerTests.TickAtSeventyFiveSecondsCatchesUpThreeAdditionalWaves`。

## TDD / Unity 执行结果

先只写测试，再在一次性临时 clone `/private/tmp/stage4-final-red.0P3Nim/project` 中执行一次聚焦 PlayMode RED 尝试：

```text
/Applications/Unity/Unity-6000.3.15f1/Unity.app/Contents/MacOS/Unity
  -batchmode -nographics
  -runTests -testPlatform PlayMode
  -testFilter ArknightsFrontline.Tests.PlayMode.BasicCombatPlayModeTests
```

Unity 在测试发现/执行前再次被许可客户端阻断：

```text
[Licensing::Client] Error: HandshakeResponse reported an error:
    ResponseCode: 505
    ResponseStatus: Unsupported protocol version '1.18.1'.
[Licensing::Module] Error: Failed to handshake to channel: "LicenseClient-ynie"
```

按要求没有启动第二次 Unity 测试；确认已记录相同已知阻塞后终止该进程。因此不能宣称 EditMode 或 PlayMode 测试通过，也没有进行场景运行验收。

## 静态验证

使用 Unity 6000.3.15f1 自带的 Roslyn `csc.dll`、现有 Bee response files 和本机已有程序集引用进行了三次独立编译，均为退出码 0 且无编译器输出：

- 完整 `ArknightsFrontline.Runtime` 源集，显式补入 Stage 4 的 `MatchOutcomeController.cs`、`MinionWaveSpawner.cs` 与现有 Input System/Physics 引用；
- 完整 `ArknightsFrontline.EditModeTests` 源集，显式补入 Stage 4 outcome/spawner 测试与 NUnit/Input System 引用；
- 完整 `ArknightsFrontline.PlayModeTests` 源集，显式补入 `MinionLanePlayModeTests.cs` 与 NUnit/TestRunner/Input System/Physics 引用。

2026-08-28 恢复任务后再次执行上述三组静态编译及 staged diff 检查，结果仍为退出码 0、无编译器输出。

对本轮 8 个代码/测试文件执行的 scoped `git diff --check` 通过。仓库级 `git diff --check` 仍会报告用户已有的 `Assets/Game/Scenes/PrototypeArena.unity` 尾随空格；本轮没有编辑或整理该场景。

## 范围保护与遗留观察

- 未修改或暂存 `Assets/Game/Scenes/PrototypeArena.unity`、Packages、ProjectSettings、构建日志、用户文档及其他已有脏文件。
- 未建立对象池、尸体销毁/停用或材质生命周期架构。
- 最终审查指出的性能观察仍然成立：死亡小兵会累积，目标搜索集合持续增长，且 `renderer.material` 会为单位产生私有材质实例。这是当前 Stage 4 规格外的后续性能工作。
- Unity 许可恢复后仍需运行完整 EditMode、PlayMode 和实际帧生命周期验收；当前证据仅包含成功的静态编译与结构检查。
