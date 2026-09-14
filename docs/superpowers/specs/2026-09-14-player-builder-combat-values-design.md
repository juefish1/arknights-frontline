# 玩家干员构建数值调整设计

## 目标

将场景构建器创建的 `Player_Exusiai` 基础生命值设为 1000、攻击力设为 50。

## 范围

- 仅修改 `PrototypeSceneBuilder.CreatePlayer` 调用 `CombatUnit.Configure` 时的生命值与攻击力参数。
- 在原型场景冒烟测试中断言玩家单位的 `MaxHealth` 为 1000、`AttackPower` 为 50。
- 不改变防御、攻击范围、攻击间隔、移动速度、阵营、空地攻击能力或其他单位数值。
- 不重建或编辑当前已保存的 `PrototypeArena.unity`；数值会在下一次运行 `Arknights Frontline → Build Prototype Arena` 后写入场景。

## 验收

重新构建场景后，进入 Play Mode 的 `Player_Exusiai` 初始血量为 1000，普攻基础攻击力为 50；既有场景根组件测试继续通过。
