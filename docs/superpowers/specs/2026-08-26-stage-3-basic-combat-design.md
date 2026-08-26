# 阶段 3：基础战斗设计

## 目标

在不实现兵线、防御塔、电脑、技能、多段攻击、减速、统计或 HUD 的前提下，让当前玩家胶囊可以对一个红方地面训练目标完成“发出攻击意图 → 判断目标 → 追击 → 普攻投射物 → 扣血 → 死亡”的最小战斗闭环。

## 范围

本阶段实现：

- 阵营、地面/空中分类、生命、攻击力、防御、攻击距离、攻击间隔和攻击目标能力；
- 合法目标判断；
- 阶段 2 指令的战斗解析、追击与停止；
- 基础普攻、可见投射物、物理伤害与死亡；
- 当前原型场景内的一个红方地面训练目标；
- 覆盖规则与场景路径的自动测试。

本阶段不实现：技能与任何多段攻击、范围伤害、减速、兵线、防御塔、电脑、重生、主动撤退、统计、HUD、数据表或通用效果框架。

## 战斗数据

`CombatUnit` 是挂在可战斗对象上的单一运行时状态组件，包含：

- `Team`：`Blue` 或 `Red`；
- `MovementClass`：`Ground` 或 `Air`；
- `MaxHealth`、`CurrentHealth`、`AttackPower`、`Defense`、`AttackRange`、`AttackInterval`；
- `CanAttackGround`、`CanAttackAir`；
- `IsDead`、`TakePhysicalDamage(float)` 和 `Died` 事件。

`CurrentHealth` 初始化为 `MaxHealth`，伤害结算后限制在 `[0, MaxHealth]`。生命归零时单位只死亡一次，之后不再接收伤害、不再是合法目标，并停止其 `UnitMotor`、玩家命令解析和基础攻击组件。死亡对象保留在场景中，作为灰盒人工观察结果；重生属于阶段 6。

## 目标规则

`TargetRules.IsLegal(attacker, candidate)` 只在以下条件全部满足时返回真：

1. 双方对象都存在，均未死亡；
2. 阵营不同；
3. 攻击者拥有攻击候选目标空中/地面类别的能力。

攻击距离使用 XZ 平面上的欧氏距离，包含边界。`TargetSelector.FindNearestInRange(attacker)` 从场景中的 `CombatUnit` 中筛选合法且在当前攻击距离内的候选，返回距离最小者；相等时按稳定实例顺序选择，避免测试与运行时结果抖动。

## 指令解析

`CombatCommandResolver` 读取 `PlayerCommandController.CurrentCommand`：

- `Attack(target)`：目标缺失或非法时清除战斗目标且不移动；目标合法且在范围内时停止移动并交给基础攻击；范围外时把 `UnitMotor` 目的地设为目标当前位置，在每帧刷新追击。首次进入攻击距离时立即停止并允许攻击。
- `AttackNearestInRange`：只调用 `TargetSelector.FindNearestInRange`；找到目标时停止移动并攻击，找不到时清除战斗目标且停止。它绝不创建到鼠标点击位置的移动。
- `Move` 或 `Stop`：清除战斗目标并停止攻击；`Move` 的实际移动仍由阶段 2 的 `PlayerCommandController` 处理。

解析器不修改 `CurrentCommand`；下一条阶段 2 命令自然取代前一条命令。目标在后续帧变为非法时，解析器停止攻击并清除其内部目标。

## 普攻、投射物与伤害

`BasicAttackController` 仅在当前目标合法且仍在攻击距离内时，按 `AttackInterval` 发起攻击。每次攻击创建一个可见几何投射物并固定该目标引用；投射物按每秒 16 个场景单位在 XZ 平面直线前进。

投射物抵达目标时再次检查合法性：合法则结算物理伤害 `max(1, AttackPower × 1 - Defense)`，非法或目标已死亡则销毁自身且不结算伤害。投射物不使用刚体碰撞作为命中依据。阶段 5 的技能投射物将复用这条合法性和伤害入口，但多段目标转移不属于本阶段。

## 场景与人工验收

确定性场景构建器在不改变现有地图几何的前提下：

- 给玩家 `Player_Exusiai` 添加 `CombatUnit`、`CombatCommandResolver` 与 `BasicAttackController`；
- 在中路红方一侧添加一个红色、`Targetable` 层、带碰撞体的 `TrainingDummy_Red`，并挂载 `CombatUnit`；
- 玩家默认可攻击地面与空中，训练目标为地面；训练目标可被右键直接攻击或 Q+左键指令攻击。

人工验收时，玩家可以右键或 Q+左键选择训练目标。距离过远时角色直线追击；进入范围后停止、持续发射可见投射物；目标生命归零后不能再被攻击。Q+左键地面、墙体或空白处只会尝试攻击当前范围内最近的合法目标，不移动到点击位置。

## 测试

编辑模式覆盖：

- 敌我与地面/空中目标合法性；
- 距离边界和范围内最近目标；
- 指定目标追击、入范围停止与非法目标无动作；
- 最近目标意图不产生目的地；
- 物理伤害公式、最低 1 点伤害与只死亡一次；
- 投射物对死亡/非法目标不结算伤害。

播放模式覆盖：

- 玩家攻击命令能够从追击进入投射物攻击并使训练目标死亡；
- 死亡训练目标不再作为可攻击目标。

## 后续阶段接口

阶段 4 的兵线和防御塔可复用 `CombatUnit`、`TargetRules`、`TargetSelector`、`BasicAttackController` 和投射物伤害入口。阶段 5 的技能可以提供不同伤害倍率或投射物连发请求，但不得改变本阶段的目标合法性与命中时复验原则。阶段 6 的重生恢复 `CombatUnit` 状态；本阶段不预先实现重生状态机。
