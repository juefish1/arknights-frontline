# EditMode 测试修复与防御塔残骸设计

日期：2026-09-18  
状态：已获用户口头确认，等待书面规格复核

## 目标

修复当前 EditMode Test Runner 中的 17 项失败，使测试重新能够可靠发现回归；同时让蓝、红双方防御塔被摧毁后留下与原塔底面积接近、保持阵营颜色的水平平面残骸。

本次工作不改变玩家已经人工验收通过的移动、手动攻击、攻击中断、小兵作战和防御塔胜负规则。

## 已定位的测试失败

当前 17 项 EditMode 失败均已从 Unity Test Runner 的持久化结果中定位：

- `CombatCommandResolverTests` 10 项：测试创建 `PlayerCommandController` 后没有执行其 `Awake` 初始化，导致 `Issue` 中的 `UnitMotor` 引用为空。
- `DeathCorpsePresenterTests` 3 项：`DeathCorpsePresenter` 在 EditMode 中调用 `Destroy`，Unity 要求使用 `DestroyImmediate`。
- `LaneMinionControllerTests` 1 项：测试中的 `BasicAttackController` 没有配置所属单位，第一次 `Tick` 会清空目标。
- `MinionWaveSpawnerTests` 1 项：测试使用 `Single` 按名称前缀查找蓝方地面小兵，但每波实际生成三个匹配对象。
- `ProjectileTests` 2 项：EditMode 不会自动执行弹丸的 `Awake`，测试读取可视组件前没有完成初始化。

除尸体销毁接口外，其余失败属于 EditMode 测试夹具没有完整模拟 Unity 运行时生命周期，不能据此推断已人工验证的玩法逻辑失效。

## 方案选择

采用扩展现有 `DeathCorpsePresenter` 的方案，不新增塔专用死亡组件，也不把原塔模型直接压扁。

原因：现有组件已经封装死亡订阅、阵营材质、落地点、活单位隐藏和残骸去碰撞逻辑。增加可配置残骸尺寸即可服务防御塔，同时保留活单位与独立残骸之间的清晰边界，便于以后只给残骸增加体积碰撞或生命周期管理。

## 防御塔残骸行为

### 场景配置

`PrototypeSceneBuilder` 为蓝、红两座防御塔配置现有 `DeathCorpsePresenter`，分别传入蓝方和红方共享材质、Ground 层以及塔专用残骸尺寸。

Unity 内置 Plane 的原始边长为 10。塔残骸采用 `(0.3, 1, 0.3)` 的局部缩放，因此实际水平面积约为 `3 × 3`，与当前塔底面积接近。干员和小兵继续使用现有 `(0.15, 1, 0.15)` 默认缩放，不改变其人工验收结果。

### 死亡流程

防御塔的 `CombatUnit.Died` 触发后：

1. `MatchOutcomeController` 仍按现有事件流程记录胜负，不修改结算规则。
2. `DeathCorpsePresenter` 在塔当前位置创建水平 Plane，并将其贴到地面上方 `0.01`，避免与地面闪烁。
3. 残骸使用原塔的阵营共享材质。
4. 残骸不包含 `CombatUnit`、`HealthBarPresenter`、攻击控制器或塔控制器，也不处于 `Targetable` 层。
5. Plane 自带碰撞体被禁用并移除；当前版本的残骸没有碰撞，但保留独立对象结构以便未来增加专用体积碰撞。
6. 原塔对象按 Unity 运行时生命周期销毁，因此原塔模型、血条、攻击能力、选中能力和活塔碰撞体一起消失。

本次不增加残骸消失时间、对象池、阻挡规则、动画或塔倒塌特效。

## EditMode 与 PlayMode 销毁策略

`DeathCorpsePresenter` 统一通过一个内部销毁入口处理 Unity 对象：

- PlayMode 使用 `Destroy`，保留当前“立即隐藏、帧末销毁”的运行时表现。
- EditMode 使用 `DestroyImmediate`，避免 Unity 抛出禁止在编辑模式使用 `Destroy` 的错误。

该策略同时用于原单位对象和 Plane 自带碰撞体。EditMode 测试按立即销毁语义断言；PlayMode 测试继续覆盖帧末销毁语义。

## 17 项测试的修复边界

测试夹具只补足 Unity 生命周期和明确选择条件，不通过放宽断言掩盖行为错误：

- `CombatCommandResolverTests` 的玩家创建帮助函数显式调用 `PlayerCommandController.Awake`，沿用现有 `PlayerCommandControllerTests` 已使用的初始化方式。
- `LaneMinionControllerTests` 在需要调用攻击计时器的用例中执行 `attack.Configure(minion)`。
- `MinionWaveSpawnerTests` 使用完整、唯一的小兵名称选择一个固定地面小兵，而不是用会命中三个对象的前缀配合 `Single`。
- `ProjectileTests` 的创建帮助函数显式执行弹丸可视初始化，再验证 Renderer 与共享黄色材质。
- `DeathCorpsePresenterTests` 更新为 EditMode 立即销毁语义，并继续验证同色 Plane、落地点、无活单位组件和无碰撞体。

若修复后出现新的失败，必须根据新的具体错误继续定位，不以忽略日志或删除断言作为通过手段。

## 新增与更新的验证

自动验证覆盖：

1. 默认干员和小兵残骸尺寸保持不变。
2. 可配置的塔残骸实际面积约为 `3 × 3`，材质与阵营一致。
3. 塔残骸没有 `CombatUnit`、血条、塔控制器、攻击控制器和碰撞体，且不在 `Targetable` 层。
4. 两座场景防御塔都挂载死亡残骸组件。
5. 任一防御塔死亡后，原塔和血条消失、残骸出现，同时现有胜负结果仍正确。
6. 全部 EditMode 测试通过；PlayMode 回归测试不因新残骸配置失败。

由于 Unity 授权服务和命令行测试在当前机器上曾出现连接问题，自动测试优先通过 Test Runner 执行。若命令行仍无法生成结果，需要记录环境阻塞，并由用户在编辑器中运行 EditMode 与 PlayMode；不能在没有结果证据时声称测试通过。

## 场景重建与人工验收

实现完成后允许覆盖 `Assets/Game/Scenes/PrototypeArena.unity`。用户执行 `Arknights Frontline → Build Prototype Arena` 重建场景，然后至少验证：

- 蓝、红塔头顶血条在存活时正常显示；
- 将任一塔生命值清空后，立体塔和血条消失；
- 原位置留下同阵营颜色、约 `3 × 3` 的贴地平面；
- 残骸不可选中、不可攻击且不阻挡当前移动；
- 摧毁红塔仍判定蓝方胜利，摧毁蓝塔仍判定红方胜利。

## 非目标

- 不修复或改写与上述 17 项失败无关的测试。
- 不调整防御塔生命值 500、攻击力 20、防御、射程或攻击间隔。
- 不改变干员、小兵和敌方干员的尸体表现。
- 不引入残骸碰撞、寻路阻挡、残骸交互、清理时间或美术资源。
- 不处理当前工作区中与本功能无关的 Package、ProjectSettings、日志和未跟踪文档变更。
