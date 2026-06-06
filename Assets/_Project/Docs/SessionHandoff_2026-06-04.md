# Grimhand 开发交接总结

**日期：** 2026-06-04  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**当前阶段：** 远征 **事件 / 祭坛 / 奖励拾取 / 消耗品 / 战斗背包** 已串联可玩；**商人框架已通、内容未做完**

**上一篇：** [SessionHandoff_2026-06-03.md](SessionHandoff_2026-06-03.md)（v2 数值首版、护甲 UI、随机种子）

**策划参考：** [expedition_map_design.json](expedition_map_design.json)（商人仍为「占位设计」段）

---

## 一、本会话完成事项

### 1. 祭坛选项文案 + 统一奖励拾取

| 项 | 说明 |
|----|------|
| 祭坛文案 | `ExpeditionShrineCatalog.cs` 四类祭坛 A/B/C 选项补全 **Label + Description** |
| 拾取阶段 | 新增 `ExpeditionPhase.RewardPickup`；金币 / 遗物 / 卡牌 / 消耗品均走 **领取 or 放弃** |
| 数据模型 | `ExpeditionRewardPickup` + `ExpeditionRewardPickupFactory` |
| 接入点 | 事件 / 祭坛 / 宝箱 / 战斗胜利 → `ExpeditionEngine.TryEnterRewardPickupPhase` |
| UI | `ExpeditionPostBattleOverlayView` 战斗后奖励；路线阶段奖励弹层 |

**原则：** 不再静默修改 RunState；所有「获得物」玩家可见、可拒绝。

---

### 2. 消耗品系统（8 种，端到端）

| 层 | 路径 / 说明 |
|----|-------------|
| 定义 | `Scripts/Battle/Consumables/` — `ConsumableIds`、`ConsumableDefinition`、`ConsumableDatabase`、`ConsumableRules` |
| 远征栏位 | `Expedition/ConsumableInventory.cs` — **5 栏、不堆叠**；满则 `PendingConsumableOfferId` |
| 战斗 | 规划阶段使用；`BattleState.ConsumableUsedThisBattle` 每场限 1 次；含选目标流程 |
| 视觉 | `ConsumableVisualCatalogSO` + Editor `ConsumableArtBinder`（菜单 **Bind Consumable Art**） |
| 测试 | `Tests/Battle/ConsumableInventoryTests.cs` |

**8 种消耗品（对齐策划）：** 小/大治疗药水、烟雾弹、泉水瓶、镜之碎片、古卷残页、力量药剂、铁壁药剂等（效果在 `ConsumableRules`）。

**获取途径：**

| 来源 | 说明 |
|------|------|
| 随机事件 | 如魔法泉水给泉水瓶、镜之幻影给镜之碎片等 |
| 商人 | 小/大治疗药水、烟雾弹（见下文商人节） |
| 战斗胜利 | 独立 roll，概率与遗物相同（`RelicDropChancePercent`） |
| 宝箱 | 独立 roll，概率与宝箱遗物相同（`TreasureRelicChancePercent`） |

满栏时：`ConsumableReplaceOverlayView` 让玩家选替换槽位或放弃。

---

### 3. 战斗背包 UI 重构

**入口：** 战斗中打开背包 — `BattleInventoryPanelView.cs`

| 区域 | 实现 |
|------|------|
| 窗口 | 约 1080×780，标题栏可拖动（`UiPanelDragHandle`） |
| 顶部 | 金币（远征 Run.Gold） |
| 角色 | 3 名玩家立绘卡片（`GetPortraitReference` 优先）；悬停 tooltip 显示等级 / 经验 / HP / 攻防速 |
| 遗物 | 图标 + 悬停说明 |
| 卡牌 | 真实 `CardView` 预览；手牌 / 抽牌堆 / 弃牌堆分组；**Grid 布局、一行 5 张、可纵向滚动** |
| 右侧 | 5 消耗品栏；战斗中可点击使用（规划阶段、未用过本场消耗品时） |
| Tooltip | `InventoryTooltipView` — 遗物 / 消耗品 / 角色共用 |

**绑定提醒：** `BattleScreenController` 需绑定 `consumableVisualCatalog`；首次或图标缺失时运行 `Grimhand → Content → Bind Consumable Art`。

**浮层层级：** 背包挂载 `CombatantTooltipLayer`，排序高于手牌 HUD。

---

### 4. 商人（流浪商人）— 框架可玩，内容未完成

| 项 | 状态 |
|----|------|
| 地图生成 | `ExpeditionMapGenerator` — 第 3–7 层保底至少 1 次商人 |
| 阶段 | 选商人节点 → `ExpeditionPhase.ShopVisit` |
| UI | `ExpeditionNodeInteractOverlayView.RefreshShop()` — 文字按钮弹层 |
| 治疗（25 金，全队 25% HP） | ✅ |
| 小/大治疗药水、烟雾弹 | ✅（含满栏替换） |
| 删牌（20 金） | ⚠️ **占位** — 扣金有，消息「删牌服务完成（占位）」，**未真正删牌** |
| 购随机卡牌 / 购遗物 / 升级 / 限购刷新 | ❌ 策划占位，未实现 |

---

## 二、遇到的问题与解决方案（费时的）

### 问题 1：背包「角色」区只有标题、无立绘

**现象：** 「角色」标题下一片空白。

**原因：** `Refresh()` → `ClearDynamic()` 会销毁 `_dynamicObjects` 内所有对象；而 `_characterRow` 在 `CreateHorizontalRow` 时被误加入该列表，**第一次刷新就把布局容器删了**，后续 `RefreshCharacters` 往已销毁节点上挂子物体。

**解决：** 持久布局行（`_characterRow`、`_relicRow`）**不再**加入 `_dynamicObjects`；`ClearDynamic` 只对行做 `ClearChildren`，只清动态内容（角色卡、遗物槽、卡牌 grid 等）。

---

### 问题 2：背包内卡牌极小且挤在一行

**现象：** 抽牌堆 20+ 张全挤一行，文字不可读。

**原因：** `AddCardGroup` 使用 `HorizontalLayoutGroup` 单行排列，且 `CardScale = 0.52`。

**解决：** 改为 `GridLayoutGroup`（`FixedColumnCount` + 换行）+ `ScrollRect` 纵向滚动；按反馈逐步调到 **scale 0.88、一行 5 张、UpperCenter 居中**。

---

### 问题 3：鼠标悬停角色时 tooltip 疯狂闪烁

**现象：** 指针放在立绘上时 tooltip 反复出现 / 消失。

**原因：** Tooltip 弹出位置盖住角色卡片；Tooltip 的 `Image` 默认 **raycastTarget = true**，抢走鼠标 → 触发 `PointerExit` → `Hide` → 再 `PointerEnter`，形成死循环。

**解决：**

1. Tooltip 背景 `raycastTarget = false` + `CanvasGroup.blocksRaycasts = false`
2. Tooltip 改显示在卡片 **右侧**（`GetWorldCorners` 算位置），不挡立绘
3. 隐藏加 ~40ms 防抖；立绘 `Image.raycastTarget = false`，由卡片背景统一接 hover

---

### 问题 4：卡牌偏左、左侧被裁切

**现象：** Grid 内卡牌贴左，左缘显示不全。

**原因：** `CardView.ApplyHandPresentationScale` 会把 `RectTransform` anchor 重置为 **左中对齐**；若在缩放后再不设回居中，视觉会向左溢出单元格。

**解决：** **先** `ApplyHandPresentationScale`，**再** 强制 anchor / pivot 居中；卡牌区增加左 padding；Grid `childAlignment = UpperCenter`。

---

### 问题 5：奖励 / 消耗品流程分散、边界不一致

**现象：** 金币静默入账、满包时行为不统一、战斗后遗物与消耗品逻辑分叉。

**解决：** 统一 `ExpeditionRewardPickup` + `RewardPickup` 阶段；消耗品满栏走 `PendingConsumableOfferId` + `ConsumableReplaceOverlayView`；事件 / 宝箱 / 战斗 / 商人购买共用同一套 claim / skip / replace 逻辑。

---

## 三、关键文件索引

```
Assets/_Project/
  Docs/
    SessionHandoff_2026-06-04.md     ← 本文
    expedition_map_design.json       ← 商人完整设计仍为占位
  Scripts/
    Battle/Consumables/
      ConsumableIds.cs
      ConsumableDatabase.cs
      ConsumableRules.cs
    Expedition/
      ConsumableInventory.cs
      ExpeditionEngine.cs
      ExpeditionRewardRoller.cs
      Model/
        ExpeditionRewardPickup.cs
        ExpeditionPhase.cs
      Events/
        ExpeditionEventResolver.cs   ← ResolveShopChoice、事件给消耗品
        ExpeditionShrineCatalog.cs
        ExpeditionRewardPickupFactory.cs
    Presentation/Battle/
      BattleInventoryPanelView.cs    ← 战斗背包
      InventoryTooltipView.cs
      ConsumableReplaceOverlayView.cs
      ExpeditionPostBattleOverlayView.cs
      ExpeditionNodeInteractOverlayView.cs  ← 商人 / 事件 / 祭坛弹层
      BattleSession.cs
    Content/
      ConsumableVisualCatalogSO.cs
      Editor/ConsumableArtBinder.cs
  Tests/Battle/
    ConsumableInventoryTests.cs
Assets/The Grimhands Asset/consumables/   ← 消耗品 PNG
```

---

## 四、如何验证本会话功能

1. 打开 `Assets/_Project/Scenes/BattleSandbox.unity` → **Play**，进入远征
2. **Bind 美术（若图标为空）：** `Grimhand → Content → Bind Consumable Art`
3. **背包：** 战斗中打开背包 → 应见 3 角色立绘、遗物、放大卡牌（5 列）、右侧 5 消耗品栏；悬停角色不闪烁
4. **奖励拾取：** 宝箱 / 战斗胜利 / 部分事件 → 应出现领取 / 放弃；满消耗品栏应弹替换
5. **商人：** 路线选「流浪商人」→ 可治疗 / 买药水；删牌仅扣金占位
6. **消耗品战斗使用：** 规划阶段点右侧栏位 → 如需目标则点角色 → 本场限 1 次

---

## 五、已知问题 / 技术债

1. **商人删牌未实装** — `BuyRemoveCard` 仅为占位文案  
2. **商人缺购卡 / 购遗物 / 升级 / 库存刷新 / 限购** — 见 `expedition_map_design.json`「商人(占位)」  
3. **金币不足反馈粗糙** — 统一返回「你没有购买任何东西」，未区分原因  
4. **消耗品掉落池** — 战斗 / 宝箱目前从全 8 种随机；策划若需「力量 / 铁壁仅商店」需单独池  
5. **`ConsumableVisualCatalog_Demo.asset`** — 需 Bind 后 UI 才有正确图标  
6. **商人无单元测试** — 商店 choice 索引与金币边界未覆盖  
7. **SessionHandoff_2026-05-27** 中「Shop 逻辑未接」已过时 — 框架已接，**内容**仍占位  

---

## 六、下次建议优先级

### P0 — 商人收尾（用户明确可明天做）

- [ ] **删牌真实流程**：选牌 UI + 从 Run 牌堆永久移除（诅咒 / 污染牌价值高）
- [ ] **购随机卡牌**：3 张选一，按品质定价（白 15 / 绿 30 / 蓝 60 等，对齐策划表）
- [ ] 购随机遗物（2 选 1，50–80 金）— 可选同会话
- [ ] 金币不足 / 已购 / 栏位满 的明确提示文案

### P1 — 消耗品 & 背包

- [ ] 专用商人 UI（展示库存、价格、已购状态）
- [ ] 消耗品掉落池按来源拆分（事件 / 宝箱 / 战斗 / 商店）
- [ ] 背包卡牌悬停详情（与手牌一致的关键词 / 数值说明）
- [ ] 力量 / 铁壁药剂等仅商店或特定事件掉落

### P2 — 远征 & 工程

- [ ] 商人 & 商店 choice 单元测试  
- [ ] 路线节点：`Elite` 等分化（旧 handoff 待办）  
- [ ] 经济数值实测（一次远征 200–400 金、价格平衡）  
- [ ] git 提交（本会话未要求 commit）

---

## 七、架构速查（本会话新增，勿破坏）

| 模块 | 作用 |
|------|------|
| `ExpeditionRewardPickup` | 统一奖励载荷（金 / 遗物 / 卡 / 消耗品） |
| `ExpeditionPhase.RewardPickup` | 非战斗领取阶段；claim / skip 后回路线或战斗 |
| `ConsumableInventory` | 5 槽远征背包；`TryAdd` / `ReplaceAt` / 满栏 offer |
| `ConsumableRules` | 战斗内效果执行（与 `EffectActionSpec` 对齐） |
| `BattleInventoryPanelView` | 战斗内只读背包 + 消耗品使用入口 |
| `InventoryTooltipView` | 背包内 hover；**必须** blocksRaycasts=false 防闪烁 |
| `PendingConsumableOfferId` | 满栏时暂存 offer，替换 UI 消费 |

**持久 UI 行 vs 动态内容：** `_characterRow` / `_relicRow` 在 `EnsureBuilt` 创建，**不可**进 `_dynamicObjects`。

---

## 八、相关对话

本会话 Cursor 记录（祭坛 / 消耗品 / 背包 / 商人状态）：agent transcript `1ac6b155-6409-4327-8c65-4e34f0ebbcbb`。

---

*文档由 2026-06-04 会话整理，便于下次从「商人删牌 + 购卡」与「消耗品 / 背包 polish」继续。*
