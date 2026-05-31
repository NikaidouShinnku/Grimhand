# Grimhand 卡牌 ScriptableObject 使用指南

> **新手请先读：** [ScriptableObject_入门.md](./ScriptableObject_入门.md)（解释 SO 是什么、为什么点菜单后要在 Project 里找文件）

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

- 会弹出 **对话框** 并高亮 `BattleSetup_Demo.asset`
- 若场景有 BattleDemo，会 **自动绑定** Battle Setup

会在以下路径创建/更新资源：
```
Assets/_Project/Data/
  Cards/          单张卡牌定义
  Characters/     角色 + 牌组引用（含 3 个哥布林敌人）
  Setups/         BattleSetup_Demo.asset（3v3）
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

### 关键词（Keywords）

在卡牌 Inspector 的 **Keywords** 列表填入关键词 ID（字符串）。悬停手牌时 Demo 会显示含义。

| 关键词 ID | 显示名 | 含义 |
|-----------|--------|------|
| `block` | 护甲 | 优先吸收受到的生命伤害；回合结束时清零 |
| `melee` | 近战 | 只能指定敌方前排或中排单位 |
| `snipe` | 狙击 | 可指定任意敌方单位，包括后排 |
| `pierce` | 贯通 | 命中主目标后，对其后方槽位的敌人造成溅射伤害 |
| `far_shot` | 远射 | 可攻击后排，但对后排目标伤害降低 |
| `poison` / `slow` / `parry` / `slot` | （见 KeywordCatalog.cs） | 状态或机制说明 |

新关键词需在代码 `KeywordCatalog.cs` 注册（后续可改为 SO 驱动）。

### 站位攻击（Reach / Splash / Back Row）

在 **Actions** 每条效果上可配置：

| 字段 | 说明 | 典型值 |
|------|------|--------|
| **Reach** | 手动选敌时可点的站位 | `FrontAndMiddle`（默认，近战）、`Any`（狙击/远射）、`BackOnly` |
| **Splash Behind Target** | 命中主目标后是否溅射后方 | 贯射等勾选 |
| **Splash Power Percent** | 溅射伤害 = 主目标威力 × 百分比 | 贯射示例：80 |
| **Back Row Power Percent** | 主目标在后排时威力 × 百分比 | 远射示例：70（100=无衰减） |

**示例卡（Generate Demo 后可在 Cards 文件夹查看）：**

- **贯射** `r_pierce`：Reach=前中，Splash 80%，关键词 pierce + melee
- **远射** `r_far_shot`：Reach=全排，后排 70% 威力，关键词 far_shot
- **狙击** `r_snipe`：Reach=全排，无后排衰减，关键词 snipe
- **重击 / 魔弹**：默认 Reach=前中，关键词 melee

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
- [ ] 贯射：打中排萨满时，后排弓手受到约 80% 溅射伤害
- [ ] 远射：可选后排弓手，但对后排伤害低于打前排
- [ ] 重击/魔弹：无法选择后排弓手
- [ ] 悬停手牌：显示关键词 tooltip（如「近战」「贯通」）
- [ ] 弹反：敌人攻击骑士后出弹反
- [ ] 死亡污染：某角色死亡后，其牌仍入手但带 `[污染]`

---

## 七、状态 ID 参考（StatusCatalog）

| Status Id | 含义 |
|-----------|------|
| `poison` | 永久，回合开始伤害=层数 |
| `slow` | 2 回合，每层 -2 速度 |

新状态需在代码 `StatusCatalog.cs` 注册（后续可改为 SO 驱动）。

---

## 八、卡图 / 边框（Visual）

每张 **`CardDefinitionSO`** 可填：

| 字段 | 说明 |
|------|------|
| **Card Art** | 卡面主图 |
| **Card Frame** | 边框（留空则用 Catalog 默认） |
| **Card Icon** | 小图标（可选） |

或在 **`Card Visual Catalog`**（`Create → Grimhand → Card Visual Catalog`）里按 **Card Id** 批量配置；未绑图时 UI 显示灰色占位。

战斗 UI 菜单：**Grimhand → Setup Battle UI in Scene** 会自动创建 `CardVisualCatalog_Demo.asset` 并绑定到 `BattleScreenController`。
