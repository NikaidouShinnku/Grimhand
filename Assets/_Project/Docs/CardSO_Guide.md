# Grimhand 卡牌 ScriptableObject 使用指南

## 死亡污染说明

角色死亡后，其所有卡牌（抽牌堆 / 手牌 / 弃牌堆）会被标记为 **不可使用**，但：
- **仍会被抽到** 进入手牌
- **仍会在回合结束时被丢弃**
- **占用 8 张手牌上限**
- UI 显示 `[污染]`，无法点选出牌

这就是 GDD 中的「死亡螺旋」。

---

## 一、生成 Demo 数据（首次）

Unity 菜单：**Grimhand → Content → Generate Demo ScriptableObjects**

会在以下路径创建资源：
```
Assets/_Project/Data/
  Cards/          单张卡牌定义
  Characters/     角色 + 牌组引用
  Setups/         战斗配置 BattleSetup_Demo.asset
```

---

## 二、让 Demo 使用 ScriptableObject

1. 打开 `BattleSandbox` 场景
2. 选中 `BattleDemo` 物体
3. 在 **Battle Demo Controller** 组件中，将 **Battle Setup** 拖入：
   `Assets/_Project/Data/Setups/BattleSetup_Demo.asset`
4. Play

若留空，则仍使用代码内置的 `DemoBattleFactory`。

---

## 三、创建一张新卡

### 方法 A：复制现有卡牌

1. Project 窗口找到 `Assets/_Project/Data/Cards/`
2. 复制一张卡，如 `Card_k_strike.asset`
3. 重命名并修改 Inspector：
   - **Card Id** / **Display Name** / **Owner Character Id**
   - **Cost** / **Card Type**
   - **Actions** 列表（可添加多条效果）

### 方法 B：从零创建

1. Project 右键 → **Create → Grimhand → Card Definition**
2. 填写字段（同上）

### Actions 常用配置

| 想做 | Type | Target | 其他 |
|------|------|--------|------|
| 打伤害 | DealDamage | DefaultEnemy / ManualSelected / EnemyBackSlot | Value=基础伤害，勾选 Scale With Attack |
| 上毒 | ApplyStatus | DefaultEnemy | Status Id=`poison`，Stacks=10，Duration=-1 |
| 减速后排 | ApplyStatus | EnemyBackSlot | Status Id=`slow`，Duration=2 |
| 弹反 | GainBlockFromLastDamagePercent + ReflectLastDamageToAttacker | Self / LastActionActor | Condition=LastActionAttackOnSelf |

**Target 说明：**
- `DefaultEnemy`：默认前排敌人
- `ManualSelected`：玩家出牌时需点选目标（如狙击）
- `EnemyFrontSlot` / `EnemyMiddleSlot` / `EnemyBackSlot`：指定敌方槽位
- `AllyFrontSlot` 等：指定友方槽位

---

## 四、把新卡加入角色牌组

1. 打开 `Assets/_Project/Data/Characters/Character_Ranger.asset`（或其他角色）
2. 在 **Deck** 列表中添加你的 `CardDefinitionSO`
3. 每个角色仍建议保持 **10 张**（GDD 规则）

---

## 五、创建新角色 / 新战斗

**新角色：** Create → Grimhand → Character Definition  
填 Level、攻防、速度、槽位、Deck 列表。

**新战斗：** Create → Grimhand → Battle Setup  
添加多个 Character Definition 到 Combatants 列表，拖到 BattleDemo 的 Battle Setup 字段。

---

## 六、验证清单

- [ ] 毒云：敌人永久中毒，每回合开始掉层数 HP
- [ ] 缚足：后排敌人减速，速度显示下降
- [ ] 狙击：点牌后选目标，再确认出牌
- [ ] 弹反：敌人攻击骑士后出弹反
- [ ] 死亡污染：某角色死亡后，其牌仍入手但带 `[污染]`

---

## 七、状态 ID 参考（StatusCatalog）

| Status Id | 含义 |
|-----------|------|
| `poison` | 永久，回合开始伤害=层数 |
| `slow` | 2 回合，每层 -2 速度 |

新状态需在代码 `StatusCatalog.cs` 注册（后续可改为 SO 驱动）。
