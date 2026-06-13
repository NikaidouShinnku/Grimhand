# Grimhand 开发交接总结 — 营地系统 & 战斗 UI 改造

**日期：** 2026-06-10  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**Unity：** 6000.x · URP · uGUI 战斗场景  

**相关旧文档：** [SessionHandoff_2026-06-04.md](SessionHandoff_2026-06-04.md)（远征事件/消耗品/背包）、[SessionHandoff_2026-06-10_RespondCombat.md](SessionHandoff_2026-06-10_RespondCombat.md)（应对机制/演出）

---

## 一、本会话完成事项（概览）

| 类别 | 状态 | 说明 |
|------|------|------|
| **营地主流程（P0）** | ✅ 完成 | 进游戏先进营地；三建筑可点；军营配队；传送门进 Demo 远征 |
| **自定义编队/牌组接入远征** | ✅ 完成 | 3 角色 × 10 槽牌组写入 `PartyMemberSnapshot.CampDeckCardIds` |
| **战斗 UI — 顶部顺序条** | ✅ 完成 | 缩小版卡牌 + 卡名，按速度结算顺序从左到右排列 |
| **战斗 UI — 角色/血条/按钮** | ✅ 完成 | 六人脚线对齐、血条贴脚、确认/空过移至右下意图框上方 |
| **怪物卡面椭圆区** | ✅ 完成 | 非 Boss 怪物统一 `card_profile_monsters` 骷髅 profile |
| **商人营地** | ⏸ 占位 | 建筑可点，功能未做 |

---

## 二、营地系统

### 2.1 流程

```
启动 → 营地主界面（CampScreenView）
  ├─ champion_camp → 军营 overlay（选 3 人 + 每人 10 张牌）
  ├─ portal       → 传送门 overlay（确认编队 → 开始 Demo 远征）
  └─ merchant_camp → 占位（暂无商店逻辑）
```

开 overlay 时隐藏营地底图；关闭后回到营地。远征战斗中 `GameFlowController` 切换营地 ↔ 战斗视图。

### 2.2 核心脚本

| 文件 | 职责 |
|------|------|
| `Presentation/Camp/CampScreenView.cs` | 营地背景 + 三建筑贴图、Alpha 点击 |
| `Presentation/Camp/CampBuildingHoverView.cs` | 悬停放大 + 金边 |
| `Presentation/Camp/CampShapeImage.cs` | 贴图 Alpha Hit Test |
| `Presentation/Camp/ChampionCampOverlayView.cs` | 军营：3 角色 + 全卡池 + 10 槽配牌 |
| `Presentation/Camp/PortalOverlayView.cs` | 传送门确认 → 开战 |
| `Presentation/Camp/GameFlowController.cs` | 营地 / 战斗流程编排 |
| `Presentation/Camp/CampRosterBuilder.cs` | 从 Content 构建默认/测试编队 |
| `Expedition/CampRunPartyApplier.cs` | 营地牌组写入远征 `PartyMemberSnapshot` |

### 2.3 数据模型

- `CampRosterState` / `CampMemberLoadout`：3 成员，每人最多 10 张 `CardDefinitionId`
- `ExpeditionEngine.StartRun(CampRosterState)`：接受自定义编队
- `ExpeditionRunDeckCatalog`：读 `CampDeckCardIds` 作为本场玩家牌池来源

> **架构注记：** `CampRosterBuilder` 放在 `Presentation/Camp/`（非 Expedition），避免 `Grimhand.Content` ↔ `Grimhand.Expedition` 循环依赖。

### 2.4 美术绑定

| 资源 | 路径 |
|------|------|
| 营地背景 | `The Grimhands Asset/path and background/campsite_background.png` |
| 军营 / 商人 / 传送门 | `champion_camp.png`、`merchant_camp.png`、`portal.png` |

`BattleUiIconCatalogSO` 新增：`CampSiteBackground`、`ChampionCampBuilding`、`MerchantCampBuilding`、`PortalBuilding`。  
菜单 **`Grimhand → Content → Refresh UI Visual Catalog`** 刷新绑定。

建筑 PNG 的 `.meta` 已设 `isReadable: 1`（供 Alpha 点击检测）。

### 2.5 Editor / 入口

- `Editor/CampUISetup.cs` — 场景节点引导
- `Editor/GrimhandBattleSceneBootstrap.cs` — 集成营地 + 延迟启动战斗
- 菜单：**`Grimhand → Open Battle Test Scene`** → Play → 营地 → 军营 → 传送门 → 远征

### 2.6 已修 Bug（营地相关）

| 问题 | 处理 |
|------|------|
| `CampRosterBuilder` 找不到 Content 程序集 | 拆到 Presentation + `CampRunPartyApplier` |
| `offsetMin` 写在 Image/Text 上 | 改为 `rectTransform.offsetMin` |
| 建筑贴图 null（灰方块） | Catalog 绑定 + `ConfigureArt` |
| `PrepareSession` NRE（screenView null） | 自动 `FindAnyObjectByType<BattleScreenView>` + Editor 补全 SO |

---

## 三、战斗 UI 改造

参考策划标注图（角色下移、顶部蓝框顺序条、右下按钮 reposition）。

### 3.1 顶部行动顺序条

**新组件：** `BattleActionOrderBarView.cs`

| 行为 | 说明 |
|------|------|
| 布局 | 卡牌在条内**垂直居中**；卡名在卡牌**上方**，可超出条顶 |
| 内容 | 缩小版卡牌（0.44 倍），无描述，下方外部 Text 显示卡名（15pt 加粗 + 描边） |
| 顺序来源 | `BattleEngine.PreviewResolutionSteps()`；无玩家选牌时仅敌人；选牌后插入玩家牌 |
| 兜底 | 预览步骤为空时读 `EnemyIntents`（与右下文字框一致） |
| 保留 | 右下 **`【敌方意图】` 文字框**不删 |

**数据：** `ActionOrderVisualEntry` + `BattleUiFormatters.BuildActionOrderVisualEntries*`

### 3.2 角色与血条

| 调整 | 说明 |
|------|------|
| 战场下移 | `StageBottom` 0.27 → 0.19 |
| 脚线 | 六人共用 `UnifiedFeetLine`；玩家额外 `PlayerPortraitExtraDownPx = -96` 与敌人地面对齐 |
| 血条 | 恢复 `AlignStatusBelowPortrait()`，跟随立绘底部 + 2px，不再固定槽位 Y |

### 3.3 按钮位置

- **空过 / 出牌** 移至右下意图框**上方**（`CardRowBottom + IntentPanelHeight + 10px`）
- 手牌区域不受影响

### 3.4 顺序条 Bug 修复记录

| 问题 | 原因 | 修复 |
|------|------|------|
| 条在、卡全无 | Viewport 透明 `Mask` 裁切子物体 | 改 `RectMask2D` / 后移除纵向裁切 |
| 卡名把卡牌顶上去 | 名字参与 VerticalLayout 排版 | 名字绝对定位在卡牌上方，entry 高度仅卡牌高 |
| 预览无条目 | 与文字框数据源不一致 | 增加 `BuildActionOrderVisualEntriesFromEnemyIntents` 兜底 |

### 3.5 怪物卡面椭圆区

**规则（`CharacterVisualCatalogSO.GetCardPortrait`）：**

| 类型 | 椭圆贴图 |
|------|----------|
| 玩家 | 各自 `card_profile_*` 半身 |
| Boss（骷髅王 / 幽灵女王 / 易爆骷髅头） | 专属 profile |
| **普通怪物** | 统一 `MonsterCardProfilePortrait` → `card/card_profile_monsters.png` |

新增：`BossCharacterRules.cs`、`CharacterVisualCatalogSO.MonsterCardProfilePortrait`  
生成管线：`MonsterContentGenerator.UpdateVisualCatalog` 自动绑定。

---

## 四、关键布局常量（`BattleUiLayoutRuntimeFix.cs`）

```
StageBottom          = 0.19
StageTop             = 0.78
ActionOrderBarTop    = 56
ActionOrderBarHeight = 136
ActionOrderBarMiniCardScale = 0.44
PlanningActionsBottom = CardRowBottom + IntentPanelHeight + 10
```

---

## 五、如何验证

1. **`Grimhand → Content → Refresh UI Visual Catalog`**（贴图缺失时）
2. **`Grimhand → Open Battle Test Scene`** → Play
3. **营地：** 点军营配队 → 传送门 → 进战斗
4. **战斗 UI 检查：**
   - 顶部顺序条：先见敌人小卡，选牌后插入玩家卡
   - 六人脚线、血条贴脚
   - 右下按钮在意图框上方
   - 怪物卡椭圆为骷髅 profile；Boss 仍为专属图

---

## 六、关键文件索引

```
Assets/_Project/Scripts/Presentation/Camp/
  CampScreenView.cs
  ChampionCampOverlayView.cs
  PortalOverlayView.cs
  GameFlowController.cs
  CampRosterBuilder.cs
  CampBuildingHoverView.cs
  CampShapeImage.cs

Assets/_Project/Scripts/Presentation/Battle/
  BattleActionOrderBarView.cs      ← 新建：顶部顺序条
  BattleScreenView.cs              ← RefreshActionOrderBar 集成
  BattleUiLayoutRuntimeFix.cs      ← 布局常量
  BattleUiFormatters.cs            ← ActionOrderVisualEntry
  CombatantSlotView.cs             ← 脚线/血条
  CardView.cs                      ← SetOrderBarPresentation

Assets/_Project/Scripts/Content/
  CharacterVisualCatalogSO.cs      ← MonsterCardProfilePortrait
  BossCharacterRules.cs            ← 新建

Assets/_Project/Scripts/Expedition/
  CampRunPartyApplier.cs

Assets/The Grimhands Asset/
  path and background/             ← 营地美术
  card/card_profile_monsters.png   ← 怪物卡 profile
```

---

## 七、建议的下一步

### A. 营地 polish

- [ ] 三建筑位置/缩放微调（anchor、hitbox）
- [ ] **商人营地**正式 UI（目前占位）
- [ ] 营地 BGM / 点击音效

### B. 战斗 UI polish

- [ ] 顺序条：隐藏意图 `?` 卡视觉再打磨
- [ ] 顺序条条目过多时的滚动/分页体验
- [ ] 4K 下字体与间距回归测试

### C. 内容与远征

- [ ] 营地配队持久化（退出游戏保留）
- [ ] 多遭遇牌组校验（空槽、非法卡 ID）

---

## 八、给下次 AI 助手的一句话

> **2026-06-10：营地 P0 已通（配队 + 传送门进 Demo 远征），战斗 UI 加了顶部速度顺序条、角色/血条/按钮布局调整，怪物卡统一骷髅 profile。商人营地仍占位；下一步可 polish 布局像素或接商人/存档。**

---

*本文件由 2026-06-10 开发会话整理，供后续接力使用。*
