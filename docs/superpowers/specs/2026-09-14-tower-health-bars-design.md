# 防御塔生命值与血条设计

## 目标

将蓝、红防御塔的基础生命值从 6000 调整为 500，并让两座防御塔始终显示头顶血条。

## 设计

- 在 `PrototypeSceneBuilder.CreateTower` 中继续复用现有 `CombatUnit`，将 `maxHealth` 改为 500、攻击力设为 20；防御、攻击范围、攻击间隔和攻击目标能力保持不变。
- 在塔根节点挂载现有 `HealthBarPresenter`。由于塔的可见模型位于 `BlueTowerVisual`/`RedTowerVisual` 子节点，血条定位逻辑读取子层级 Renderer 的合并边界，显示在塔顶。
- 通过原型场景 PlayMode 测试验证两座塔的 500 生命、血条组件和可见血条；重建场景后人工检查实际位置和受伤缩短。

## 范围

不新增塔专用血条组件；除攻击力调整为 20 外，不改变塔的防御、攻击范围、攻击间隔、攻击目标能力、胜负、碰撞或其他单位数值；重建场景时允许覆盖 `PrototypeArena.unity`。
