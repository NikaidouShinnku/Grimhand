# Grimhand 开发交接总结

**日期：** 2026-05-27  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**当前阶段：** 战斗内核 + SO 内容管线 + IMGUI Demo **已较完整**；**远征三场连战 Demo 可玩**

---

## 一、今日完成事项

### 1. 卡牌关键词与站位攻击（可扩展 SO）

| 能力 | 说明 |
|------|------|
| **Keywords** | 卡牌 SO / 模板支持关键词列表；`KeywordCatalog.cs` 集中维护含义 |
| **悬停 Tooltip** | 手牌悬停显示关键词说明 |
| **Reach** | `FrontAndMiddle`（默认近战）、`Any`（全排）、`BackOnly` |
| **SplashBehindTarget** | 命中主目标后溅射后方单位 |
| **BackRowPowerPercent** | 打后排时威力衰减 |

**示例卡（Content 菜单生成）：**

- **贯射** `r_pierce`：前/中排 + 80% 后方溅射  
- **远射** `r_far_shot`：全排，后排 70% 威力  
- **狙击** `r_snipe`：全排，无后排衰减  
- **重击/魔弹**：默认前/中排 + `melee` 关键词  

文档：`Assets/_Project/Docs/CardSO_Guide.md`

---

### 2. 选牌顺序显示（测试用）

- 手牌已选：`#n` = 全局选牌先后；`#n[m/t]` = 同角色内第 m 张 / 共 t 张  
- 规划阶段显示 **「已选顺序」** 列表面板  
- 逻辑：`PlanningDraft.GetGlobalPlayOrder` / `TryGetOwnerPlayOrder`

---

### 3. 远征系统（三场连战 Demo）

**新程序集：** `Grimhand.Expedition`（纯 C#，无 Unity 依赖）

| 组件 | 路径 |
|------|------|
| 远征引擎 | `Scripts/Expedition/ExpeditionEngine.cs` |
| 跨场 HP / 组 BattleConfig | `Scripts/Expedition/ExpeditionBattleConfigBuilder.cs` |
| SO 配置 | `Scripts/Content/ExpeditionSetupSO.cs` |
| Demo UI | `Scripts/Presentation/BattleDemoController.cs` |

**流程：**

1. 绑定 **Expedition Setup** → Play 进入远征  
2. 第 1 场战斗 → 胜利 → **三选一路线**（目前均为普通战斗，遭遇可复用）  
3. 第 2 场 → 再选路线 → 第 3 场  
4. 三场全胜 → 远征完成；任一场战败 → 远征失败  

**规则：**

- **血量跨场不恢复**（杀戮尖塔式）；每场敌人满血、牌堆重新洗牌  
- 仅绑定 **Battle Setup**、不绑 Expedition Setup → 仍为单场 3v3  

**菜单：**

- `Grimhand → Content → Generate Demo ScriptableObjects`（生成 `ExpeditionSetup_Demo.asset` 并尝试自动绑定）  
- `Grimhand → Content → Assign Demo Expedition Setup to Scene`

---

### 4. 今日修复的 Bug

| 问题 | 原因 | 修复 |
|------|------|------|
| 打赢后不出路线选择 | 胜利时仍在 `SpeedResolve`，未切到 `BattleEnd` | `BattleEngine.ResolveTurn` 胜负确定时 `SetPhase(BattleEnd)` |
| 选路线 `ArgumentOutOfRangeException` | IMGUI 同帧内点击后清空 `PendingRoutes`，for 循环仍用旧 count | 路线列表快照 + 点击后 `return` |
| 远征下一场死亡污染失效 | `OnCharacterDied` 在卡牌实例创建**之前**调用 | 全部卡牌创建后再对 `Hp≤0` 角色统一污染 |
| 污染牌视觉上仍「亮」 | UI 未变暗 | 污染牌 `[污]` + 灰色背景 |

**战斗内核扩展：** `CombatantConfig.StartHp` — 远征继承上场 HP。

---

## 二、程序集结构（当前）

| 程序集 | 职责 |
|--------|------|
| Grimhand.Core | RNG 等 |
| Grimhand.Battle | 战斗内核（无 Unity） |
| Grimhand.Expedition | 远征流程、路线、跨场 HP |
| Grimhand.Content | ScriptableObject 定义 |
| Grimhand.Presentation | IMGUI Demo |
| Grimhand.Battle.Tests | 单元测试 |

---

## 三、如何跑 Demo

1. Unity 打开项目，Console 无红字  
2. `Grimhand → Content → Generate Demo ScriptableObjects`  
3. 打开 `Assets/_Project/Scenes/BattleSandbox.unity`  
4. 选中 **BattleDemo**，确认 **Battle Setup** + **Expedition Setup** 已绑定  
5. Play  

**测试：** Window → Test Runner → EditMode → `Grimhand.Battle.Tests`

---

## 四、核心设计备忘

- **能量：** 上限 8，首回合满，每回合 +3；Planning 选牌即时扣费，取消返还  
- **手牌：** 上限 8，每回合抽 5；EndOfTurn 全弃  
- **死亡污染：** 死者牌 `IsUsable=false`，仍可抽占手牌位  
- **选目标：** 攻击/减益需手动选敌（含仅 1 敌时）；自身防/治疗、按槽位（如缚足）除外  
- **弹反：** 出牌武装；下次受击减伤 + 按威力反射（走站位倍率）  
- **结算顺序：** 速度轮询；同角色多张按选牌队列 `[1/2]→[2/2]` 消耗  

---

## 五、建议的下一步（优先级）

### A. 内容向（需你设计，可并行）

- 在 Excel/Notion 定首包卡牌表（约 10 张/角色 × 3 人）  
- 填 Keywords、Reach、数值后批量写入 SO  
- 新关键词在 `KeywordCatalog.cs` 注册  

### B. 远征扩展

- [ ] 路线节点类型分化：`Elite` / `Event` / `Shop`（枚举已有，逻辑未接）  
- [ ] 多场不同遭遇 SO（目前复用同一 BattleSetup）  
- [ ] 胜利奖励（选卡、回血等）— 目前无  
- [ ] 地图/节点可视化（现 IMGUI 三卡片）  

### C. 战斗/UI  polish

- [ ] 规划阶段 **速度模拟结算预览**（可选）  
- [ ] uGUI / UI Toolkit 替代 IMGUI  
- [ ] `KeywordCatalog` / `StatusCatalog` 改 SO 驱动（卡牌量大时）  

### D. 质量

- [ ] 补测试 / CI；`DeathPollutionTests.StartWithZeroHp_PollutesOwnerCardsOnInit` 已加  
- [ ] Golden Test：固定种子整局日志比对  

---

## 六、关键文件索引

```
Assets/_Project/
  Docs/
    CardSO_Guide.md
    ScriptableObject_入门.md
    SessionHandoff_2026-05-27.md    ← 本文
  Scripts/
    Battle/BattleEngine.cs
    Battle/Planning/PlanningDraft.cs
    Battle/Rules/KeywordCatalog.cs
    Battle/Rules/TargetReachRules.cs
    Expedition/ExpeditionEngine.cs
    Content/ExpeditionSetupSO.cs
    Content/Editor/GrimhandContentMenu.cs
    Presentation/BattleDemoController.cs
  Data/Setups/
    BattleSetup_Demo.asset
    ExpeditionSetup_Demo.asset
  Scenes/BattleSandbox.unity
  Tests/Battle/
```

---

## 七、给下次 AI 助手的一句话

> **Grimhand 战斗内核与 SO 管线已就绪，远征三场连战 Demo 可玩（HP 继承、路线三选一、死亡污染跨场生效）。下一步优先：用户批量设计卡牌内容，和/或 远征节点类型分化 + 正式 UI。**

---

*本文件由 2026-05-27 开发会话整理，供后续接力使用。*
