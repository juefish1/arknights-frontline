# 训练目标生命值调整设计

## 目标

将原型场景 `TrainingDummy_Red` 的最大生命值与初始生命值从 40 调整为 1000，便于持续观察攻击行为。

## 范围

- 修改 `PrototypeSceneBuilder` 中训练目标的 `CombatUnit.Configure` 参数。
- 同步场景 PlayMode 测试对训练目标最大生命值和初始生命值的预期。
- 不修改攻击力、防御力、攻击间隔、目标规则、投射物逻辑或当前未提交的 `.unity` 场景文件。

## 验收

- 通过“Build Prototype Arena”重建后的 `TrainingDummy_Red` 最大生命值为 1000，初始生命值为 1000。
- 既有场景测试使用 1000 作为预期值。
