# 尸体生命周期设计

## 1. 目标

为现有死亡表现增加按单位类别区分的尸体生命周期：

- 小兵尸体生成后保留 5 秒，然后销毁；
- 干员尸体保留至同一干员下次再部署前被显式清理；
- 防御塔尸体在当前场景中永久保留，仅随场景卸载清理。

本功能只管理尸体对象的存续时间，不实现阶段 6 的撤退、复活、再部署倒计时或重新生成干员流程。

## 2. 当前约束

`DeathCorpsePresenter` 在单位死亡时创建独立尸体对象，随后销毁原单位。因此，尸体的计时和后续清理不能依赖已经销毁的原单位组件。

项目已有 `UnitKind.Operator`、`UnitKind.Minion` 和 `UnitKind.Tower`，但尚未用于尸体表现。项目也尚无可供订阅的再部署生命周期事件。

## 3. 架构

新增 `CorpseLifetimeController`，挂载在每个由 `DeathCorpsePresenter` 创建的尸体对象上。该组件独立保存尸体类别、干员标识、经过时间和存续状态，因此原单位销毁后仍能完成生命周期管理。

`DeathCorpsePresenter` 的配置增加 `UnitKind`。创建尸体时，它把单位类别和干员标识传给 `CorpseLifetimeController`：

- `Minion`：启用 5 秒计时；
- `Operator`：登记到按 `ownerKey` 索引的干员尸体表，不自动计时；
- `Tower`：不计时、不登记，保持至场景卸载。

不建立场景级尸体管理器，也不把生命周期逻辑塞回已经随原单位销毁的 `DeathCorpsePresenter`。

## 4. 规则与数据流

### 4.1 小兵

小兵尸体的 5 秒从尸体对象创建时开始计算。空中小兵的 0.3 秒坠落包含在 5 秒内，而不是落地后重新开始计时。

`CorpseLifetimeController.Tick(float deltaTime)` 对负数时间按 0 处理；累计时间达到或超过 5 秒时销毁尸体。4.99 秒时尸体仍必须存在。消失为直接销毁，不增加淡出动画。

### 4.2 干员

干员尸体使用稳定的 `ownerKey` 登记。当前阶段以死亡单位的 `gameObject.name` 作为 key，例如 `Player_Exusiai` 和 `TrainingDummy_Red`。

`CorpseLifetimeController.ClearOperatorCorpse(string ownerKey)` 是未来再部署流程的接线入口：

- 在生成新干员前调用；
- 只清理完全匹配该 key 的现存干员尸体；
- key 不存在时无操作；
- 重复调用是安全的；
- 销毁尸体时必须同步移除登记，避免静态表残留失效引用。

如果同一 key 在旧尸体尚未清理时又生成尸体，新登记替换旧登记，并清理旧尸体，保证每个干员最多保留一个尸体。

### 4.3 防御塔

防御塔尸体不自动销毁。比赛结束不改变此规则；尸体只在场景卸载时由 Unity 清理。

### 4.4 比赛与场景生命周期

比赛结算不批量清理小兵、干员或防御塔尸体。已有正在计时的小兵尸体仍按生成后的 5 秒规则消失。

静态干员尸体登记不得跨场景持有已销毁对象；组件 `OnDestroy` 负责解除自身登记。

## 5. 接线

- `MinionWaveSpawner` 创建地面和空中小兵时，将尸体类别明确配置为 `UnitKind.Minion`。
- `PrototypeSceneBuilder` 创建玩家和训练敌方干员时，配置为 `UnitKind.Operator`。
- `PrototypeSceneBuilder` 创建双方防御塔时，配置为 `UnitKind.Tower`。
- `DeathCorpsePresenter` 的既有便捷重载保持兼容，默认类别为 `UnitKind.Operator`，避免现有测试辅助代码和未迁移调用点静默获得 5 秒清理行为。
- 重建并保存 `Assets/Game/Scenes/PrototypeArena.unity`，使保存场景携带明确的单位类别配置。

## 6. 测试策略

严格使用 TDD，先观察真实行为测试失败，再实现最小代码。

EditMode 测试覆盖：

- 小兵尸体累计 4.99 秒仍存在，累计到 5 秒时销毁；
- 负数 `deltaTime` 不推进计时；
- 干员尸体不会随时间自动消失；
- 正确 `ownerKey` 清理对应干员尸体，错误 key 不影响其他尸体，重复清理安全；
- 同一 key 只保留最新尸体；
- 防御塔尸体经过超过 5 秒仍存在；
- 空中小兵的计时从生成时开始，不等待落地。

PlayMode 测试覆盖：

- `MinionWaveSpawner` 生成的小兵死亡后，尸体按 5 秒规则消失；
- 保存的 `PrototypeArena` 中玩家、训练敌方干员和双方防御塔具有正确尸体类别；
- 防御塔死亡并完成比赛结算后，尸体仍保留；
- 现有尸体落地、材质、缩放、碰撞体移除和血条清理行为继续通过。

完成时运行尸体生命周期聚焦 EditMode、完整 EditMode、尸体/场景聚焦 PlayMode，并准确报告任何既有失败。人工验收需在 Unity Play Mode 中观察三类尸体行为；在人工验收实际执行前不得称其通过。

## 7. 非目标

- 不实现干员再部署状态机、倒计时、复活或重新生成；
- 不实现主动撤退；
- 不增加尸体淡出、溶解、透明度动画或对象池；
- 不改变尸体材质、尺寸、落点、碰撞层或空中坠落时长；
- 不改变比赛结算规则。
