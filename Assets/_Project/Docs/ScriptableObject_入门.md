# ScriptableObject 入门（Grimhand）

## 它是什么？

**ScriptableObject（简称 SO）** 是 Unity 里的一种**数据文件**，不是场景里的物体，也不是 C# 脚本本身。

可以把它理解成：

> **Excel 表里的一行配置**，但放在 Project 里，能在 Inspector 里直接改，战斗代码运行时读取。

| 对比 | 普通 C# 代码 | ScriptableObject |
|------|-------------|------------------|
| 改数值 | 改代码 → 重新编译 | 在 Inspector 点选 `.asset` 改字段 |
| 策划/迭代 | 程序员改 | 设计师也能改（不用碰逻辑） |
| 位置 | `Scripts/` | `Assets/_Project/Data/` 下的 `.asset` |

Grimhand 里三类 SO：

```
CardDefinitionSO      → 一张卡叫什么、费多少、什么效果
CharacterDefinitionSO → 一个角色 + 他的 10 张牌组
BattleSetupSO         → 一场战斗：有哪些角色、能量上限等
```

战斗内核（`BattleEngine`）**不读 SO**，只读 `BattleConfig`。  
`BattleSetupSO.ToBattleConfig()` 负责把 SO **翻译成**战斗能用的数据。

---

## 为什么点菜单「好像没发生任何事」？

菜单：**Grimhand → Content → Generate Demo ScriptableObjects**

它**不会**改场景、**不会**自动 Play，而是：

1. 在 **Project 窗口** 创建/更新 `.asset` 文件  
2. 弹出 **对话框** 告诉你完成了（新版已加）  
3. 自动 **高亮** `BattleSetup_Demo.asset`  
4. 若场景里有 `BattleDemo`，会 **自动绑定** Battle Setup  

### 请按这个顺序检查

1. 看 **Console** 是否有红色报错（有报错则生成失败）  
2. 看 **Project** → `Assets/_Project/Data/` 是否出现文件夹和 `.asset`  
3. 看是否弹出 **「Demo 数据已生成」** 对话框  
4. 打开 **BattleSandbox** 场景 → 选中 **BattleDemo** → Inspector 里 **Battle Setup** 是否有引用  

若 Battle Setup 为空：

- 再点一次 **Generate Demo ScriptableObjects**，或  
- 菜单 **Grimhand → Content → Assign Demo Battle Setup to Scene**

---

## 推荐工作流（第一次）

```
1. Grimhand → Content → Generate Demo ScriptableObjects
2. Grimhand → Setup Battle Sandbox Scene   （或打开已有 BattleSandbox）
3. 确认 BattleDemo 上 Battle Setup = BattleSetup_Demo.asset
4. Play
```

日志应显示：`战斗开始 — 3v3 (SO, 种子 xxxxx)`

若显示 `(代码, ...)` 说明 **没绑 SO**，仍在用代码内置数据（现在也是 3v3，但改卡要去改 C#）。

---

## 在 Project 里长什么样？

```
Assets/_Project/Data/
├── Cards/
│   ├── Card_k_strike.asset      ← 点选后在 Inspector 改「重击」
│   ├── Card_r_slow.asset
│   └── ...
├── Characters/
│   ├── Character_Knight.asset   ← Deck 列表拖入卡牌 SO
│   └── Character_Goblin_Archer.asset
└── Setups/
    └── BattleSetup_Demo.asset   ← Combatants 列表拖入 6 个角色 SO
```

**`.asset` 文件图标**像一张小表格；点选后 **Inspector** 显示所有字段。

---

## 当前 Demo 战斗（3v3）

| 我方 | 槽位 |
|------|------|
| 骑士 | 前排 |
| 法师 | 中排 |
| 游侠 | 后排 |

| 敌方 | 槽位 | 特点 |
|------|------|------|
| 哥布林蛮兵 | 前排 | 撕咬、猛扑 |
| 哥布林萨满 | 中排 | 邪咒（毒）、虚弱（减速） |
| 哥布林弓手 | 后排 | 箭矢、瞄准 |

可验证：**选不同敌人**、**缚足打后排弓手**、**毒云指定萨满或蛮兵** 等。

---

## 和「写死在代码里」的关系

| 方式 | 何时用 |
|------|--------|
| **SO（推荐）** | 正式内容、Demo、以后给策划改 |
| **DemoBattleFactory** | 单元测试、没绑 SO 时的后备 |

目标：**以后只改 SO，不改工厂。**

更详细的改卡步骤见 [CardSO_Guide.md](./CardSO_Guide.md)。
