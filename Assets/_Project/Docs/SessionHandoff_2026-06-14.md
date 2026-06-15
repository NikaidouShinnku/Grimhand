# Grimhand 开发交接总结 — 天赋持久化 / 手牌预览 / 海渊层 / 立绘缩放

**日期：** 2026-06-14  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**内容来源：** `Grimhand实际内容总览表.xlsx`（海渊层小节 + 怪物组合表）

**相关旧文档：** [SessionHandoff_2026-06-10_BattleCampUI.md](SessionHandoff_2026-06-10_BattleCampUI.md)、[SessionHandoff_2026-06-10_RespondCombat.md](SessionHandoff_2026-06-10_RespondCombat.md)

---

## 一、本会话完成事项（概览）

| 类别 | 状态 | 说明 |
|------|------|------|
| **天赋整局持久化 + 战斗生效** | ✅ | 祭坛选择带入远征；`TalentBattleRules` 26 个天赋钩子 |
| **天赋 UI** | ✅ | 详情页关闭按钮改右上角 |
| **战斗立绘动画** | ✅ | 演出中不再闪回站位（Refresh 跳过 layout 重置） |
| **上回合明细滚动** | ✅ | ScrollRect + 垂直滚动条 |
| **手牌数值 / 悬停伤害预览** | ✅ | 仅底部手牌显示加成后数值；选目标悬停敌人时单体伤害变化 |
| **背包 / 商店 / 奖励牌面** | ✅ | 仍显示原始公式描述（`BuildCardStatsLinePreview`） |
| **敌方意图显示** | ✅ | 改用 `CardPreviewRules.DescribeIntentEffect` 显示算好伤害 |
| **海渊层（41–60 层）** | ✅ | 背景、路径、6 小怪、20 组遭遇、60 层 Boss、传送门测试入口 |
| **立绘缩放分级** | ✅ | 洞穴默认 / 地牢+精英略大 / 海渊更大 / Boss 最大 |
| **海渊贴图绑定** | ✅ | 多 Sprite 图集取最大子图（人鱼战士不再显示小鱼） |

---

## 二、远征区域结构（三层）

| 区域 | 层数 | 起始层（传送门） | 背景 | 路径 |
|------|------|------------------|------|------|
| 洞穴 | 1–20 | 1 | `cave_background` | `cave_path1`–`5` |
| 地牢 | 21–40 | 21 | `dungeon_background` | `dungeon_path1`–`3` |
| 海渊 | 41–60 | 41 | `underwaterruin_background` | `underwaterruin_path1`–`3` |

**核心规则：** `ExpeditionRegionRules.cs`

- `CaveLayerCount = 20`，`DungeonLayerCount = 40`，`FullLayerCount = 60`
- `DungeonStartLayer = 21`，`AbyssStartLayer = 41`
- `ApplyMapStartLayer`：按传送门选项设置 `ChapterLayerCount` / `MapStartLayer`

**Boss 分层：**

- 20 层：骷髅王 / 幽灵女王（原有）
- 40 层：石傀儡 ×3（`StoneGolemBossEncounterBuilder`）
- 60 层：鬼灵海盗船长 + 深渊怪物 ×2（`AbyssBossEncounterBuilder`）

---

## 三、海渊层内容

### 3.1 新增小怪（按 Excel「小怪设计 · 海渊层」）

| 显示名 | CharacterId | 贴图 | 默认站位 | HP/ATK/DEF/SPD |
|--------|-------------|------|----------|----------------|
| 踏潮守卫 | `char_seahorse_guard` | `seahorse_guard.png` | 中 | 100/14/7/8 |
| 水母海巫 | `char_jellyfish_caster` | `jellyfish_caster.png` | 后 | 90/11/4/6 |
| 人鱼战士 | `char_mermaid_warrior` | `mermaid_warrior.png`（`mermaid_warrior_1`） | 前 | 110/15/6/7 |
| 深渊怪物 | `char_abyss_creature` | `abyss_creature.png` | 中 | 115/12/8/4 |
| 腐蚀蟹 | `char_corrupted_crab` | `corrupted_crab.png` | 前 | 105/8/12/4 |
| 鬼灵海盗船长 | `char_phantom_captain` | `phantom_captain.png`（`phantom_captain_1`） | 中 | 150/18/7/7 |

卡牌与牌组由 **`MonsterContentGenerator.Abyss.cs`** 生成；特性钩子见 **`MinionTraitCatalog` / `MinionTraitRules`**（踏潮速度加攻、人鱼 0 费叠攻、深渊伤毒、腐蚀蟹受击毒、船长低血狂怒等；部分复杂机制如「终焉召唤」延迟自杀做了简化近似）。

### 3.2 怪物组合（20 组，`MonsterEncounterCatalog`）

- **普通 41–49：** 人鱼双、人鱼+踏潮、人鱼+锁链怨灵+幽灵、踏潮+水母、骷髅双+水母 等
- **普通 45–54：** 人鱼+精英幽灵+水母、人鱼三、踏潮双+水母、人鱼+踏潮+水母 等
- **普通 51–59：** 腐蚀蟹+深渊、腐蚀蟹+踏潮、人鱼+深渊 等
- **精英 41–44 / 45–49 / 45–54 / 51–59：** 含鬼灵海盗船长、深渊+水母等组合（与 Excel「怪物组合 · 海渊层」一致）

部分组合复用洞穴/地牢怪（骷髅、锁链怨灵、幽灵等）作为混搭。

### 3.3 美术与资源绑定

- **`ExpeditionArtBinder.cs` / `GrimhandUiVisualBootstrap.cs`**：`AbyssBackground`、`AbyssPathVariants`
- **`ExpeditionPathArt.cs`**：41 层起切换海渊背景与路径
- **`BattleUiIconCatalogSO`**：新增 `AbyssBackground`、`AbyssPathVariants` 字段
- **贴图绑定：** 洞穴/地牢仍用 `UpsertVisual`；海渊专用 **`UpsertVisualLargest`**（多 Sprite 图集取面积最大子图）

### 3.4 传送门测试

**`PortalOverlayView.cs`** 三个起始按钮：

- 洞穴（1 层）
- 地牢（21 层）
- **海渊（41 层）** ← 新增

---

## 四、战斗 UI — 手牌数值与悬停预览

### 4.1 设计原则

| 界面 | 牌面数值 | Tooltip |
|------|----------|---------|
| **战斗底部手牌** | 加成后数值（站位、遗物、天赋等） | 关键词 + 详情 |
| **背包（战斗内牌堆）** | 空（公式在 Tooltip） | `BuildCardStatsLinePreview` |
| **商店 / 战后奖励 / 图鉴** | 公式 / 原始描述 | 同上 |

### 4.2 悬停敌人预览（仅手牌 · 单体伤害）

1. 点击需选目标的手牌 → `AwaitingTargetCardId` 生效
2. 悬停合法敌人 → `CombatantSlotView` → `OnDamagePreviewEnter`
3. `HandPanelView.Refresh` → `BuildCardStatsLineForHand(state, draft, **card**, hoverTarget)`
4. **`ResolveDamagePreviewTarget`** 匹配当前待选牌 InstanceId 后调用 **`PreviewHpDamageAgainstTarget`**（含 DEF、护甲、站位 incoming 等；不含完整应对链）

**已修 Bug：** 曾用 `ResolveForDescription` 的 `descCard`（`InstanceId = 0`）算伤害，导致预览永远不更新；现改为战斗内真实 **`card`** 对象。

### 4.3 关键文件

| 文件 | 职责 |
|------|------|
| `BattleUiFormatters.cs` | `BuildCardStatsLineForHand`、`ResolveDamagePreviewTarget`、`DescribeEffectClause` |
| `HandPanelView.cs` | 手牌刷新与预览目标传入 |
| `BattleScreenView.cs` | 悬停回调、`RefreshHand` |
| `CardPreviewRules.cs` | `PreviewHpDamageAgainstTarget`、`DescribeIntentEffect` |
| `BattleInventoryPanelView.cs` | `useFormulas: true` 恢复公式模式 |

---

## 五、天赋系统（整局远征）

### 5.1 数据流

```
天赋祭坛选择 → ExpeditionTalentRunState → StartRun 写入 Run
→ BuildEncounter 合并 TalentBattleContext → 战后 SyncRunStateFromBattle
```

### 5.2 核心文件

| 文件 | 职责 |
|------|------|
| `Expedition/TalentDatabase.cs` | 天赋目录、合并到 `BattleConfig`、战后同步 |
| `Battle/Rules/TalentBattleRules.cs` | 26 个天赋战斗钩子 |
| `Expedition/Model/ExpeditionTalentRunState.cs` | 远征运行时天赋槽位 |
| `Battle/Model/TalentBattleContext.cs` | 战斗内天赋上下文 |
| `Presentation/Camp/TalentCampOverlayView.cs` | UI（详情页关闭按钮右上角） |
| `Tests/Battle/TalentPersistenceTests.cs` | 持久化测试 |

**程序集约束：** `Grimhand.Battle` 不可引用 `Grimhand.Expedition`；角色 ID 用 `RelicEffectRules` / 常量，不用 `TalentCatalog` 跨层引用。

---

## 六、立绘缩放（敌人）

**文件：** `CombatantSlotView.cs` + `MinionTraitCatalog.UsesElevatedPortraitScale` / `IsAbyssRegionCharacter`

| 档位 | 缩放 | 适用 |
|------|------|------|
| 普通洞穴怪 | **1.28** | 哥布林、史莱姆、骷髅兵、巨魔、蝙蝠等 |
| 略大 | **1.62** | 地牢全套 + `char_wraith` / `char_wraith_elite` / `char_skeleton_elite` |
| 海渊 | **2.15** | 6 种海渊小怪 |
| Boss | **2.35** | 骷髅王、幽灵女王等 |
| 玩家 | **2.28** | 我方三人 |

> **注意：** 曾误将全局敌人缩放改为 2.15，导致洞穴骷髅过大；已改回分级策略，**勿再改全局 `EnemyPortraitScale`**。

---

## 七、其他 UI / 战斗修复

| 项 | 说明 |
|----|------|
| `BattleTurnDetailPanelView` | Viewport + 垂直滚动条，从顶部开始 |
| `CombatantSlotView.Refresh` | `IsAwayFromHome \|\| IsAnimating` 时跳过 `ApplyStatusAnchorLayout`，避免动画闪回 |
| `AbyssBossEncounterBuilder` | 修复 `?.TryGetValue` + `out var` 未赋值编译错误（CS0165） |
| `MonsterEncounterBuilder` | 注册 `char_abyss_creature` 召唤模板（水母「终焉召唤」） |

---

## 八、Editor 菜单（内容更新后必跑）

1. **`Grimhand → Content → Generate Demo ScriptableObjects`**  
   生成/更新角色、卡牌、远征 `MonsterCharacters`（含海渊 6 怪）

2. **`Grimhand → Content → Bind Expedition Art`**（或 Refresh UI Visual Catalogs）  
   绑定海渊背景、路径、图标

3. **Play 验证路径**  
   营地 → 传送门 → 选「海渊（41 层）」→ 确认水下遗迹地图与遭遇

---

## 九、测试建议

- [ ] 洞穴 1 层：哥布林等体型仍为原尺寸（1.28）
- [ ] 地牢 21 层：鼠人/石像鬼等略大（1.62）；幽灵精英在洞穴遭遇中也略大
- [ ] 海渊 41 层：人鱼为美少女立绘（非小鱼）；整体大于地牢
- [ ] 选手牌单体攻击 → 悬停不同敌人，手牌伤害数字变化
- [ ] 打开背包看手牌/抽牌堆：仍为公式描述
- [ ] 天赋祭坛选天赋 → 整局战斗生效 → 战后仍保留
- [ ] 60 层 Boss：鬼灵海盗船长 + 两只深渊怪

---

## 十、未提交 / 后续可选

- 本会话改动**尚未统一 git commit**（含更早的应对机制、猫灵雕像、天赋、海渊等）
- 海渊部分卡牌/特性为简化实现（换位加血、延迟召唤、偷护甲等可后续补全）
- 手牌悬停 Tooltip 仍可与牌面计算数值分离（牌面数字、Tooltip 公式）— 未做，按需再加
- 全链路 1–60 层连续远征（不停在 20/40）若产品需要可另开任务

---

## 十一、关键文件索引

```
Expedition/
  ExpeditionRegionRules.cs      # 三层区域 20/40/60
  MonsterEncounterCatalog.cs    # +20 海渊遭遇
  AbyssBossEncounterBuilder.cs
  TalentDatabase.cs / ExpeditionEngine.cs（天赋接线）

Battle/Rules/
  MinionTraitCatalog.cs / MinionTraitRules.cs  # 海渊特性 + 立绘分级 ID
  TalentBattleRules.cs
  CardPreviewRules.cs

Content/Editor/
  MonsterContentGenerator.Abyss.cs
  MonsterContentGenerator.cs    # UpsertVisual / UpsertVisualLargest
  ExpeditionArtBinder.cs

Presentation/Battle/
  CombatantSlotView.cs          # 立绘缩放
  HandPanelView.cs / BattleScreenView.cs / BattleUiFormatters.cs
  BattleInventoryPanelView.cs
  ExpeditionPathArt.cs

Presentation/Camp/
  PortalOverlayView.cs          # 海渊 41 层按钮
  TalentCampOverlayView.cs

Data/
  CharacterVisualCatalog_Demo.asset  # +6 海渊视觉；人鱼/船长子图修正
```

---

*文档版本：2026-06-14 · 供下次会话快速恢复上下文。*
