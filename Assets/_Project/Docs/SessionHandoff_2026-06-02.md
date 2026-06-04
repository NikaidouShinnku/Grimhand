# Grimhand 开发交接总结

**日期：** 2026-06-02  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**当前阶段：** 战斗内核 + 远征 **稳定**；**uGUI 战斗界面第二版可 Play**（大立绘对峙布局、手牌交互、角色悬停详情、速度序队列）

**上一篇：** [SessionHandoff_2026-06-01.md](SessionHandoff_2026-06-01.md)（uGUI 第一版、BattleSession、立绘管线）

---

## 一、本会话完成事项（战斗 UI polish + 数据对齐）

### 1. 战场布局（左右大立绘对峙）

| 区域 | 说明 |
|------|------|
| **PlayerStage / EnemyStage** | 左右各占 50%，立绘无大框；Front > Middle > Back 绘制顺序；立绘约 88% 缩放 |
| **底行** | 左：已选队列；中：手牌；右：敌方意图 |
| **上行** | 左：远征 / 回合 / 能量；右：确认出牌 / 空过 / 重开 |
| **运行时修补** | `BattleUiLayoutRuntimeFix.cs`（旧场景自动迁移；布局定稿后可逐步删除） |

---

### 2. 手牌与卡牌交互

| 功能 | 实现 |
|------|------|
| 悬停放大 | `CardScaleRoot` 顶锚缩放，避免卡名被裁切 |
| 牌面描述 | 中文：伤害 + 前中/全排/后排等；**不再显示** melee / far_shot 等 keyword |
| 选中高亮 | **仅已进入执行队列**的牌显示淡白遮罩（α≈0.26）；等待选目标只显示 **?** 角标 |
| 队列序号 | 角标 `#n` 按 **速度结算顺序**，非点选先后 |

**相关文件：** `CardView.cs`、`BattleUiFormatters.cs`、`HandPanelView.cs`、`BattleEngine.GetPlayerCardsInResolveOrder()`

---

### 3. 角色槽与悬停详情

| 功能 | 说明 |
|------|------|
| 脚下信息 | **仅 HP**（`UnitStatsRowView` 紧凑模式） |
| 悬停 | 立绘略放大 + 高亮；**右侧**（我方）/ **左侧**（敌方，朝战场中央）弹出详情框 |
| 详情内容 | 攻击 / 防御 / 速度 / 护甲 / 状态（中文） |
| 顶层显示 | `CombatantTooltipLayer`（Canvas sortOrder 250），避免被其他立绘遮挡 |
| 选目标 | 点击区域对齐 **立绘实际范围**；可选目标黄色描边（Outline） |

**相关文件：** `CombatantSlotView.cs`、`CombatantDetailPopupView.cs`、`CombatantTooltipLayer.cs`、`UiSpriteBounds.cs`

---

### 4. HUD 微调

- 能量水晶固定 **28×28**，独立 `EnergyRow`，与「回合」文字分行，避免重叠
- 远征模式：Title = 远征进度；Subtitle = 回合 · 阶段

---

### 5. 角色数据与显示名对齐

每个角色在数据上是 **一份 `CharacterDefinitionSO`**，运行时生成 **`CombatantState`**；UI 为 **6 个固定槽**（Team + Slot 绑定），不是每人一个独立 prefab。

**改显示名：** 编辑 `Assets/_Project/Data/Characters/Character_*.asset` 的 **DisplayName** 即可。

| CharacterId | DisplayName | 立绘 |
|-------------|-------------|------|
| char_knight | 战士 | warrior |
| char_mage | 法老 | pharaoh |
| char_ranger | 恶魔 | devil |
| char_goblin_brute | 哥布林蛮兵 | goblin |
| char_goblin_shaman | **骷髅萨满** | skeleton |
| char_goblin_archer | **怨灵弓手** | wraith |

内部 ID（`char_goblin_*`）未改，卡牌 / 代码引用不受影响。

---

## 二、如何跑 Demo

1. 打开 `Assets/_Project/Scenes/BattleSandbox.unity` → **Play**
2. 若 UI / 立绘异常：`Grimhand → Open Battle Test Scene`
3. 操作：点牌 → 需目标时点高亮敌人 → **确认出牌** / **空过**

---

## 三、程序结构速查

```
逻辑层     CharacterDefinitionSO → BattleSetup → CombatantState（HP/攻/速/状态/牌组归属）
立绘       CharacterVisualCatalogSO，按 CharacterDefinitionId 查 Sprite
卡牌       CardDefinitionSO + OwnerCharacterId；手牌 CardInstanceState
UI 槽位    CombatantSlotView × 6，FindCombatant(Team + FormationSlot)
会话       BattleSession（IMGUI / uGUI 共用）
```

---

## 四、已知限制 / 未做

- [ ] 操作按钮仍为 **纯色块 + 文字**（确认 / 空过 / 重开），无专用 Icon
- [ ] 战斗 **背景** 为纯色，未接场景图
- [ ] 立绘仅 idle；attack / hit / defend 帧未播
- [ ] 卡牌 CardArt 多为占位；CardIcon 未系统化
- [ ] `BattleUiLayoutRuntimeFix` 仍为过渡方案
- [ ] 遗物、Roguelike  meta 进度 **未开始**
- [ ] 怪物 **Design 文档 / 数值表** 尚未正式开写

---

## 五、下次建议优先级（用户规划）

### A. 美术 — UI Icon（优先）

制作并接入战斗常用 Icon，例如：

| Icon | 用途 |
|------|------|
| 确认出牌 | `PlanningActionsRight` 主按钮 |
| 空过 | Skip 按钮 |
| 重开远征 | Restart 按钮 |
| 能量水晶 | 已有 `ERG.png`，可统一风格 |
| HP / 攻 / 防 / 速 | 悬停详情框、可选恢复脚下多属性显示 |

**接入点：**

- `BattleUiIconCatalogSO`（`Assets/_Project/Data/`）
- `GrimhandUiVisualBootstrap.cs`（Editor 自动绑 Sprite）
- `BattleUISetup.cs` / `BattleScreenView` 按钮 `Image` + `Text`

建议目录：`Assets/The Grimhands Asset/UI/icons/`

---

### B. 美术 — 战斗背景

- 定稿分辨率与安全区（手牌区、立绘区不被挡）
- 接法：BattleCanvas 下 **`Background`** 全屏 Image，或 Camera 后景 Sprite
- 注意与 `PlayerStage` / `EnemyStage` 锚点协调（当前底边约 36%）

---

### C. 内容 — 数据设计（Design 阶段）

开始 **每个怪物 / 角色** 的 Design，建议先文档后 SO：

1. **模板字段：** 名称、站位、HP/攻/防/速、定位、牌组构成、关键词行为、美术 ID
2. **首包范围：** 沿用 demo 6 人扩写，或新怪替换 `char_goblin_*` 命名空间
3. **产出物：** Excel/Notion → 批量写入 `CharacterDefinitionSO` + `CardDefinitionSO`
4. **参考：** `CardSO_Guide.md`、`GrimhandContentMenu` 生成管线

**注意：** 新怪应用 **DisplayName 与立绘一致**；`CharacterId` 与 Catalog 条目一一对应。

---

### D. 后续（暂不排期）

- **遗物（Relic）** 系统设计与 SO 结构
- **Roguelike** 元素：地图节点分化、奖励、Meta 进度（远征已有雏形）
- 立绘 pose 动画、`KeywordCatalog` SO 化、移除 `BattleUiLayoutRuntimeFix`

---

## 六、关键文件索引

```
Assets/_Project/
  Docs/
    SessionHandoff_2026-06-02.md          ← 本文
    SessionHandoff_2026-06-01.md
    SessionHandoff_2026-05-27.md
    CardSO_Guide.md
  Scenes/BattleSandbox.unity
  Data/
    Characters/                           ← DisplayName、牌组、属性
    CharacterVisualCatalog_Demo.asset
    BattleUiIconCatalogSO（若已创建）
    Setups/BattleSetup_Demo.asset
  Scripts/Presentation/Battle/
    BattleSession.cs / BattleScreenView.cs
    CardView.cs / CombatantSlotView.cs
    BattleUiFormatters.cs / BattleUiLayoutRuntimeFix.cs
    CombatantTooltipLayer.cs
  Scripts/Battle/
    BattleEngine.cs                         ← PreviewResolutionSteps / 速度序
  Scripts/Editor/
    BattleUISetup.cs
    GrimhandBattleSceneBootstrap.cs
    GrimhandUiVisualBootstrap.cs
```

---

## 七、给下次 AI 助手的一句话

> **Grimhand uGUI 第二版已可 Play：大立绘布局、手牌中文描述、速度序队列、角色悬停详情、敌人显示名与骷髅/怨灵立绘已对齐。下一步优先：① 确认/空过等按钮 Icon + BattleUiIconCatalog；② 战斗背景图；③ 怪物/角色 Design 文档与 SO 数据批量设计。遗物与 Roguelike 深化暂缓。**

---

*本文件由 2026-06-02 开发会话整理，供后续接力使用。*
