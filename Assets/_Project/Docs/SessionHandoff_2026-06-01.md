# Grimhand 开发交接总结

**日期：** 2026-06-01 
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**当前阶段：** 战斗内核 + 远征 Demo **稳定**；**uGUI 战斗界面第一版可 Play**，待美术 Icon 接入与 UI 重排

**上一篇：** [SessionHandoff_2026-05-27.md](SessionHandoff_2026-05-27.md)（关键词、远征、内核 Bug 修复）

---

## 一、今日完成事项（uGUI 战斗 UI）

### 1. 架构：逻辑与渲染分离

| 模块 | 路径 | 说明 |
|------|------|------|
| **BattleSession** | `Scripts/Presentation/Battle/BattleSession.cs` | 战斗 + 远征会话，**无渲染**；IMGUI 与 uGUI 共用 |
| **BattleScreenController** | `.../BattleScreenController.cs` | 场景入口，挂 **BattleDemo** 上 |
| **BattleScreenView** | `.../BattleScreenView.cs` | HUD / 战场 / 手牌 / 遮罩 总刷新 |
| **BattleUiFormatters** | `.../BattleUiFormatters.cs` | 文本格式化（与 Demo 共享） |

旧 **BattleDemoController**（IMGUI）仍保留，默认 **禁用**；可 Inspector 手动启用对照。

---

### 2. uGUI 组件

| 组件 | 职责 |
|------|------|
| **CardView** | 单张卡牌：卡图/框/费/名/选中描边/污染遮罩 |
| **HandPanelView** | 手牌 Scroll + 对象池 |
| **CombatantSlotView** | 战场槽：立绘 + HP/攻/速文字 + 选目标按钮 |
| **CardVisualResolver** | 从 `CardDefinitionSO` 或 `CardVisualCatalogSO` 解析卡图 |

**Prefab：** `Assets/_Project/Prefabs/UI/CardView.prefab`

---

### 3. 美术资源管线（已接好）

#### 角色立绘 — `CharacterVisualCatalogSO`

**资产：** `Assets/_Project/Data/CharacterVisualCatalog_Demo.asset`  
**Editor 填充：** `GrimhandBattleSceneBootstrap.EnsureCharacterVisualCatalog()`

| CharacterId | 显示名 | PNG 路径 |
|-------------|--------|----------|
| char_knight | 战士 | `The Grimhands Asset/warrior/warrior_idle_1024.png` |
| char_mage | 法老 | `The Grimhands Asset/pharoah/pharoah_idle_1024.png` |
| char_ranger | 恶魔 | `The Grimhands Asset/devil/devil_idle_1024.png` |
| char_goblin_brute | 哥布林蛮兵 | `monsters/goblin_idle_1024.png` |
| char_goblin_shaman | 哥布林萨满 | `monsters/skeleton_idle_1024.png` |
| char_goblin_archer | 哥布林弓手 | `monsters/wraith_idle_1024.png` |

同目录下还有 attack/hit/defend/defeat 动画帧，**尚未接入**（可接 `BattleEventKind.PortraitPoseChanged` 等）。

#### 卡牌美术 — `CardVisualCatalogSO` + `CardDefinitionSO`

- 每张卡 SO：`CardArt` / `CardFrame` / `CardIcon` 字段  
- 或统一目录：`Assets/_Project/Data/CardVisualCatalog_Demo.asset`  
- **当前卡图多为灰色占位**，不影响逻辑

---

### 4. Editor 菜单

| 菜单 | 作用 |
|------|------|
| **`Grimhand → Open Battle Test Scene`** | 一键：Demo SO + 立绘目录 + UI + 保存 `BattleSandbox.unity` |
| `Grimhand → Setup Battle UI in Scene` | 仅刷新当前场景 UI |
| `Grimhand → Content → Generate Demo ScriptableObjects` | 生成/更新卡牌与角色 SO |

**推荐日常：** 打开 `BattleSandbox.unity` → **Play**（不必每次点 Setup）。

---

### 5. 今日修复的 Bug

| 问题 | 原因 | 修复 |
|------|------|------|
| Play 报 Input System 错 | EventSystem 用 `StandaloneInputModule` | 改为 `InputSystemUIInputModule` |
| 战场/立绘全黑 | `PlayerRow` 等 RectTransform **负高度** | 新布局 API + `BattleUiLayoutRuntimeFix` Play 时自动修 |
| 无「确认出牌」按钮 | 按钮锚点在屏幕左外 | ActionBar 改 `HorizontalLayoutGroup` |
| 点牌无选中效果 | `Button.onClick` 未绑 / UI 未刷新 | `CardView` 绑 onClick + 点击后立即 `Refresh` |
| 立绘空白、无法选目标 | `CombatantSlotView` 阵营/槽位 **未序列化**，Play 后丢失 | `Configure()` 启动时调用 + `[SerializeField] team/formationSlot` + 从物体名推断 |
| 关键词 Tooltip 闪烁 | Tooltip 挡住卡牌触发 Enter/Exit 循环 | Tooltip `raycastTarget=false` + 显示在卡牌右上方 |
| 「手牌 x/x」被挡 | 与 Scroll 区重叠 | 手牌区顶部留 32px 给标题 |

---

## 二、如何跑 Demo（当前）

1. Unity 打开项目，Console 无红字  
2. 双击 **`Assets/_Project/Scenes/BattleSandbox.unity`**  
3. **▶ Play**  

若 UI 异常：菜单 **`Grimhand → Open Battle Test Scene`** 重建一次。

**操作：** 点手牌 → 攻击牌选敌人（槽位高亮）→ **确认出牌** / **空过**；悬停看关键词；三场远征胜利后三选一路线。

---

## 三、程序集（更新）

| 程序集 | 说明 |
|--------|------|
| Grimhand.Presentation | uGUI 战斗界面 + IMGUI Demo + **BattleSession** |
| Grimhand.Content | SO 定义 + **CharacterVisualCatalogSO** / **CardVisualCatalogSO** |
| Grimhand.Editor | BattleUISetup、GrimhandBattleSceneBootstrap |
| Grimhand.Content.Editor | GrimhandContentMenu（含 `GenerateDemoAssetsSilent`） |

---

## 四、明日计划（用户侧）+ 对接指引

### A. Icon 图标（卡牌 / 生命 / 攻击等）

**建议放置目录（任选，保持一致即可）：**

```
Assets/The Grimhands Asset/UI/          ← 推荐新建
  icons/
    icon_hp.png
    icon_attack.png
    icon_block.png
    icon_speed.png
    icon_energy.png
    icon_card_frame_attack.png
    ...
```

**代码接入点（下次改 UI 时动这些）：**

| 用途 | 文件 | 说明 |
|------|------|------|
| 卡牌小图标 | `CardDefinitionSO.CardIcon` / `CardView.iconImage` | 每张卡或 Catalog 统一配 |
| 卡牌框/背景 | `CardDefinitionSO.CardFrame` / `CardView.frameImage` | 按 CardType 分框 |
| 费用徽章 | `CardView` 里 `CostBadge` 子物体 | 可换成 Icon + 数字 |
| 单位属性行 | `CombatantSlotView.bodyText` | 现为纯文字 `HP/甲/攻/速`；可改为 **Icon + Text** 或独立 `StatRowView` |
| HUD 能量 | `BattleScreenView.titleText` | 现为「能量 8/8」文字；可拆 Icon |

**可选扩展（未做）：**

- 新建 `UiIconCatalogSO`（类似 CharacterVisualCatalog），按字符串 key 查 Sprite  
- 或在 `CharacterVisualCatalogSO` 旁加 `BattleUiIconCatalogSO`

**Unity 导入：** PNG → Texture Type **Sprite (2D and UI)**，与立绘相同。

---

### B. UI 排布重设计

**当前布局由 Editor 脚本生成，改排布主要动：**

```
Assets/_Project/Scripts/Editor/BattleUISetup.cs     ← BuildLayout() 里所有 Rect 位置/尺寸
Assets/_Project/Scripts/Presentation/Battle/BattleUiLayoutRuntimeFix.cs  ← Play 时补丁（重排后可能可删或简化）
```

**当前大致分区（1920×1080 参考）：**

```
┌─ HUD（回合/能量/队伍 HP）────────────────┐
├─ Battlefield（420px 高，敌上/我下，各 3 槽）─┤
├─ 敌方意图 / 已选队列 / 选目标提示 ────────┤
├─ 手牌 Scroll + 「手牌 n/m」─────────────┤
└─ 确认出牌 | 空过 | 重开远征 ──────────────┘
```

**重排建议流程：**

1. 在 Figma/纸上定新稿  
2. 改 `BattleUISetup.BuildLayout`（或拆成 Prefab 手动摆场景，弱化代码生成）  
3. 执行 **`Open Battle Test Scene`** 保存场景  
4. 若不再需要旧场景兼容，可删除或精简 `BattleUiLayoutRuntimeFix`  

**Canvas：** `BattleCanvas` → Screen Space Overlay，`CanvasScaler` 1920×1080，match 0.5。4K 下会缩放，锚点用 stretch + 固定 band 较稳。

---

## 五、已知限制 / 未做

- [ ] 卡牌 **CardArt** 未批量绑定（灰色占位）  
- [ ] 立绘仅 **idle**；attack/hit 等帧未播  
- [ ] 单位属性纯文字，**无 Icon**（明日美术目标）  
- [ ] UI 为代码生成第一版，**视觉 polish 少**（明日重排目标）  
- [ ] 速度模拟预览、路线地图可视化仍无  
- [ ] `BattleUiLayoutRuntimeFix` 为过渡方案，正式 Prefab 稳定后可移除  

---

## 六、关键文件索引

```
Assets/_Project/
  README.md                                    ← 快速开始（已改为 Open Battle Test Scene）
  Docs/
    SessionHandoff_2026-05-27.md
    SessionHandoff_2026-05-28.md                 ← 本文
    CardSO_Guide.md
  Scenes/BattleSandbox.unity                     ← 主测试场景
  Prefabs/UI/CardView.prefab
  Data/
    CharacterVisualCatalog_Demo.asset
    CardVisualCatalog_Demo.asset
    Setups/BattleSetup_Demo.asset
    Setups/ExpeditionSetup_Demo.asset
    Characters/                                  ← 战士/法老/恶魔 显示名已改
  Scripts/
    Presentation/Battle/                         ← uGUI 全套
    Editor/BattleUISetup.cs
    Editor/GrimhandBattleSceneBootstrap.cs
    Content/CharacterVisualCatalogSO.cs
    Content/CardVisualCatalogSO.cs
Assets/The Grimhands Asset/                      ← 角色立绘 PNG（用户资源）
```

---

## 七、给下次 AI 助手的一句话

> **Grimhand uGUI 战斗界面已可 Play（BattleSandbox + BattleSession），立绘与选目标、出牌流程已通。用户明日要加 Icon（费/HP/攻等）并重排 UI——优先改 `BattleUISetup.BuildLayout` / `CombatantSlotView` / `CardView`，必要时加 `UiIconCatalogSO`；卡牌图绑 `CardDefinitionSO` 或 `CardVisualCatalog_Demo`。旧 `BattleUiLayoutRuntimeFix` 可在布局定稿后删除。**

---

*本文件由 2026-05-28 开发会话整理，供后续接力使用。*
