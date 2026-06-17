# Grimhand 开发交接总结 — 数值对齐 / 演出展示快照 / 血条跟随

**日期：** 2026-06-15  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**内容来源：** `Grimhand实际内容总览表.xlsx`（经验、遗物成长、怪物数值、层数缩放、事件文案）

**上一篇：** [SessionHandoff_2026-06-14.md](SessionHandoff_2026-06-14.md)（海渊层、天赋、手牌预览、立绘缩放）

---

## 一、本会话完成事项（概览）

| 类别 | 状态 | 说明 |
|------|------|------|
| **打怪经验** | ✅ | 按区域 + 战斗类型随机 XP；事件战额外奖励 |
| **遗物每 20 层成长** | ✅ | 获得时按层数立即成长；跨层自动补成长；进化链转移层数 |
| **层数怪物缩放（削弱）** | ✅ | HP +5%/层、ATK +3%/层、DEF +2%/层 |
| **小怪 / Boss 数值** | ✅ | 对照 Excel 更新 Generator 与 Boss Builder |
| **事件流程 + 文案** | ✅ | 选选项 → 描述页 → 确定 → 效果；概率事件只显示命中句 |
| **背包等级显示** | ✅ | 角色名旁 `战士Lv2` |
| **战斗演出展示快照** | ✅ | ATK/DEF/速度/状态/血怒/特性随动画逐步更新，不提前显示整回合结果 |
| **敌我血条水平对齐** | ✅ | 以玩家中间位为基准，敌人立绘整体上移 |
| **出牌归位动画** | ✅ | 不再闪现；0 伤害也有受击反馈 |
| **敌人出牌时血条跟随** | ✅ | 第三版：完整世界坐标偏移，X/Y 同步跟随立绘 |

---

## 二、数值与远征系统（对照策划表）

### 2.1 打怪经验 — `CombatXpRules.cs`

按**区域 + 战斗类型**随机发放：

| 区域 | 普通 | 精英 | Boss |
|------|------|------|------|
| 洞穴 | 8–10 | 14–20 | 25 |
| 地牢 | 13–17 | 23–27 | 40 |
| 海渊 | 18–24 | 28–36 | 55 |

**事件战额外：** 镜中挑战 +5、复仇战 +8、熔炉石傀儡 +10  
**古老神殿「虔诚祈祷」：** 全队 +5 经验

### 2.2 遗物每 20 层成长 — `RelicGrowthRules.cs`

- `ExpeditionRunState.RelicGrowthTiers` 记录每件遗物已成长次数
- **获得时：** `floor / 20` 立即应用（例：50 层获得 → 2 次）
- **跨层时：** `CompleteCurrentNode` 后 `SyncFloorGrowth` 补成长（例：10 层获得 → 20 层 +1）
- **进化链**（翡翠/熔炉等）：`TransferGrowthTiers` 转移层数
- **铁壁战甲** 基础 DEF 修正：1 → **2**（文档 DEF+2）
- **奇迹之叶** 成长：复活 HP 比例 +10%/层（默认 20%）

### 2.3 层数怪物缩放（削弱）— `EnemyFloorScaling.cs`

| 属性 | 每层加成（相对 1 层） |
|------|------------------------|
| HP | +5% |
| ATK | +3% |
| DEF | +2% |

仅作用于敌方；带 90%–110% 随机浮动。  
（原曲线 15/10/5 已替换）

### 2.4 小怪 / Boss 数值更新

已更新 `MonsterContentGenerator` / Boss Builder，主要变更：

| 单位 | 变更 |
|------|------|
| 幽灵 | HP 18→20 |
| 幽灵精英 | ATK 11→10 |
| 骷髅王 | HP 400→350 |
| 石傀儡 | HP 85→80 |
| 踏潮守卫 | ATK 14→13 |
| 水母海巫 | HP 90→80 |
| 人鱼战士 | 110/15/6 → 100/14/6 |
| 深渊怪物 | 115/12/8 → 95/12/7 |
| 腐蚀蟹 | 105/8/12 → 100/8/10 |
| 鬼灵海盗船长 | 150/18 → 130/16 |

**Editor：** 改完后需执行 `Grimhand → Content → Generate Demo ScriptableObjects` 同步 Character SO。

### 2.5 事件流程

**新阶段：** `ExpeditionPhase.EventAftermath`

```
选选项 → 显示 AfterChoiceText → 点「确定」 → 触发实际效果
```

- `ExpeditionEventCatalog` 每项选项增加 `AfterChoiceText`
- **古老熔炉** 新增选项 C「探索熔炉」（40% 石傀儡战 / 30% 遗物 / 30% 无）
- **神秘旅者** 选项 A 文案改为「随机卡牌奖励」（去掉「蓝优先，否则白」）

### 2.6 概率事件文案（Bug 修复）

**问题：** 魔法泉水等概率事件，确认页把 60%/25%/15% 三条描述全部堆在一起。

**修复：** `ExpeditionEventAftermathText.cs`

1. 选选项时 **先掷骰一次**（0–99）
2. 确认页 **只显示命中那一句**
3. 点「确定」用 **同一 roll** 结算，避免前后不一致

适用：魔法泉水 · 饮用；赌徒骰子 · 小赌/大赌；古老熔炉 · 探索熔炉

### 2.7 背包等级显示

**需求：** 名字旁标注等级，如 `战士Lv2`

**改动：** `BattleInventoryPanelView.cs` — 角色卡片名、卡牌分组标题、悬停标题均带 `Lv.N`

### 2.8 测试

- `CombatXpRulesTests.cs`（新增）
- `RelicGrowthRulesTests.cs`（新增）
- `EnemyFloorScalingTests.cs`（更新削弱曲线）
- `ExpeditionEngineTests.cs`（XP 范围）

---

## 三、战斗 UI — 演出展示准确性

### 3.1 背景问题

整回合在玩家点「出牌」后 **逻辑已全部算完**，但演出还在播动画。  
若 UI 直接读 `CombatantState`（live 状态），会出现：

- 巨魔「战争怒吼」还没播完，悬停已显示涨过的 ATK
- 受击叠的血怒层数提前出现
- 多张牌的效果 **一次性跳到最后**

**逻辑层本身是对的**（`ResolveStep` 才 `RefreshDerivedStats`），问题在 **表现层读数时机**。

### 3.2 方案：事件检查点 + 展示快照

#### 核心类型

| 类型 | 文件 | 作用 |
|------|------|------|
| `CombatantDisplayStats` | `PresentationSnapshot.cs` | Attack/Defense/Speed/BloodRage/Status/TraitFootnote |
| `_eventCheckpoints` | `PresentationSnapshot.cs` | 按战斗事件索引缓存各单位展示属性 |
| `PresentationCheckpointRecorder` | `BattleEngine.cs` | 每条影响展示的事件后录制检查点 |
| `BattlePresentationCheckpointKinds` | `Battle/Events/` | 定义哪些 `BattleEventKind` 需要录制 |
| `CombatantDisplayHelper` | 新建 | 框内/悬停统一读快照 |
| `MinionTraitDisplayFormatter` | 新建 | 敌人特性脚注（血怒、石像鬼、鼠群狂怒等） |

#### 录制时机（`BattleEngine`）

在 `ResolveStep` / `ResolveRespondStep` 产生事件后，若 `BattlePresentationCheckpointKinds.ShouldRecord(kind)` 为 true，则回调录制当前 `BattleState` 快照。

#### 应用时机（`BattlePortraitDirector`）

动画段落结束后调用 `ApplyEventDisplayCheckpoint(e)`，将 UI 切到 **该事件对应的检查点**，而非整回合最终态。

**典型同步点：**

| 事件 | UI 更新内容 |
|------|-------------|
| `StatusApplied` | 状态、ATK/DEF 等（动画结束后） |
| `DamageApplied` | HP、血怒层数（受击动画后） |
| `BlockGained` | 护甲 |
| `HealApplied` / `CharacterRevived` | HP、存活 |
| `PortraitIdleRestored` / 出牌归位 | 出牌者属性（含血怒消耗） |

#### 敌人框内血怒

有层数时显示：`血怒×N  下一张攻击+{N×15}%`（橙红色，`bodyText` 区域）

#### 规划阶段

无 `PresentationLocked` 时仍读 **实时** 数据，不受影响。

---

## 四、Bug 修复详细过程

### Bug A：敌我血条不在同一水平线

**现象：** 玩家与敌人 Stage 分离，血条 Y 不一致。

**尝试与结论：**

| 轮次 | 做法 | 结果 |
|------|------|------|
| 1 | 槽内固定本地 Y `UnifiedFootStatusAnchorY` | 与立绘脚部分离，距离过远 |
| 2 | 改回跟随立绘脚底 + 2px | idle 时对齐，但无法统一六人高度 |
| 3 | 以玩家中间位 **世界坐标 Y** 为基准，敌人 **整体平移**（立绘+血条+特性） | ✅ idle 齐平 |

**最终机制（`EnsureFixedEnemyHpBarLayout`）：**

1. 先 `AlignStatusBelowPortrait()` 保持立绘与血条自然相对关系
2. 量 `playerHpBarWorldY - footRoot.position.y`
3. `portraitRoot.position += (0, deltaWorld, 0)` **只抬立绘**（或连同 foot 一起，视版本）
4. 血条固定在 `_unifiedHpBarWorldY`
5. `_enemyLayoutLocked = true`，禁止 Refresh 重置 anchor

---

### Bug B：演出期间框内数值「跳到最后」

**现象：** 巨魔先攻后防，防御牌动画未播完，框内已显示 DEF 提升。

**根因：** `SyncCombatantFromLive()` 读的是整回合结算后的 live 状态。

**修复：** 见第三节「事件检查点」—— 改为 `ApplyEventCheckpoint(eventIndex)` 按事件逐步推进展示。

**附带：** 新增 `MinionTraitDisplayFormatter`，被动/特性（石像鬼姿态、首击闪避等）同样随检查点出现，不提前。

---

### Bug C：出牌后角色 **闪现归位**（第二问题，已解决）

**现象：** 加护甲、0 伤害攻击后，敌人/玩家瞬间回槽位，无 tween。

**根因链：**

1. `EndCardPlay` 在 `!IsAwayFromHome` 时直接 `RestoreHomePosition()` 瞬移
2. `MoveToCenter` 开头或结尾把 `_isAnimating` 过早置 false
3. `EndCardPlay` 后全量 `Refresh()` → `ApplyEnemyPortraitLift()` 把立绘 **打回原位**
4. `ForceSettleHome()` 在中央也强制 snap

**修复（`CombatantPortraitView` + `BattlePortraitDirector`）：**

| 改动 | 说明 |
|------|------|
| `ReturnHome()` 始终 tween | 距离 > 0.01 就插值，不 snap |
| `MoveToCenter` 去掉开头 snap | 保持 `_awayFromHome` 直到归位完成 |
| `RecaptureHomePosition()` | 布局锁定后更新 home 世界坐标 |
| `ForceSettleHome` | 仅在家附近才结算，避免把中央立绘瞬移回去 |
| `EndCardPlay` | 一律 `ReturnHome()` + `SyncCombatantSlotLayout()`，**不用**全量 Refresh 改布局 |
| `PlayHitReaction(0)` | 0 伤害（非格挡）也播放受击闪烁/姿势 |

**用户确认：** 「第二个问题解决了，也就是动画正常了。」

---

### Bug D：敌人到中央出牌时 **血条不跟随**（第三问题，本会话最终修复）

**现象：** 立绘沿 X 移到战场中央，血条钉在槽位不动。

**三轮修复过程：**

#### 第一版（失败）

- 思路：idle 时锁血条世界 Y，动画期间 **不跟** 立绘（避免乱跳）
- 问题：用户明确要求「血条要跟着敌人走，保持相对位置」

#### 第二版（失败）

- 思路：`LateUpdate` + `ApplyEnemyFootDuringAnimation()`，血条 **只跟 Y**
- 根因：`MoveToCenter` **只改 X**，Y 保持 home 水平线 → 只跟 Y 等于血条 **X 完全不动**
- 另：`Refresh` / `ApplyEnemyPortraitLift` 反复改 anchor，对齐结果被覆盖

#### 第三版（成功）✅

**文件：** `CombatantSlotView.cs`

1. **锁定布局时** 记录完整世界偏移：
   ```csharp
   _footWorldOffsetFromPortraitFoot = footRoot.position - GetPortraitFootWorldPosition();
   ```
2. **动画期间**（`LateUpdate` + `SyncEnemyLayoutAfterPresentation`）：
   ```csharp
   targetWorld = GetPortraitFootWorldPosition() + _footWorldOffsetFromPortraitFoot;
   // 转为 slot 本地 anchoredPosition（与玩家侧 AlignStatusBelowPortrait 一致）
   ```
3. **idle 归位后** 同样用偏移公式，保证与锁定时的相对关系一致

**要点：**

- 必须 **XYZ 全跟随**（至少 X+Y），不能只跟 Y
- 用 `anchoredPosition` 换算而非裸设 `position`，避免 Canvas 布局在 LateUpdate 之后盖回
- `_enemyLayoutLocked` 后禁止 `ApplyStatusAnchorLayout` 重置敌人 anchor

**用户确认：** 「很好，今天先这样。」（血条跟随生效）

---

### Bug E：编译错误（会话中顺带修复）

| 错误 | 修复 |
|------|------|
| `RelicIds` 找不到 | `RelicGrowthRules.cs` 补 `using Grimhand.Expedition.Model` |
| `ApplyGoldRelicBonus` 误用 `run` | `ExpeditionRewardRoller.cs` 改为参数传入 `RelicGrowthTiers` |

---

## 五、机制确认（问答记录）

### 绿皮巨魔「重拳」`m_ogre_heavy_punch`

- 1 费攻击，目标前/中排，**同一目标连打 2 次**
- 每次伤害 = **ATK + 5**（独立结算护甲/减伤）
- UI 数字通常只显示 **单次** 威力，实际打两下

### 血怒机制（`MinionTraitCatalog.OgreBloodRage`）

- 每次受伤 +1 层，上限 5
- **仅攻击牌** 享受每层 +15% 伤害
- **任意攻击牌整牌结算完成后** 层数清零（重拳两击都吃到加成，牌结束才清）
- 防御/状态牌 **不消耗** 血怒

---

## 六、关键文件索引

```
Battle/
  BattleEngine.cs                          # PresentationCheckpointRecorder
  Events/BattlePresentationCheckpointKinds.cs

Expedition/
  CombatXpRules.cs                         # 区域 XP
  RelicGrowthRules.cs                      # 遗物 20 层成长
  EnemyFloorScaling.cs                     # 层数缩放（削弱）
  ExpeditionEngine.cs                      # EventAftermath 流程
  ExpeditionRunState.cs                    # RelicGrowthTiers
  Events/
    ExpeditionEventCatalog.cs              # AfterChoiceText
    ExpeditionEventAftermathText.cs        # 概率事件单句描述
    ExpeditionEventPlanner.cs

Presentation/Battle/
  PresentationSnapshot.cs                  # 展示快照 + 事件检查点
  CombatantDisplayHelper.cs
  MinionTraitDisplayFormatter.cs
  BattlePortraitDirector.cs                # ApplyEventDisplayCheckpoint、EndCardPlay
  CombatantPortraitView.cs                 # ReturnHome / MoveToCenter / 0伤受击
  CombatantSlotView.cs                     # 血条对齐 + ApplyFootFollowPortrait
  BattleScreenView.cs                      # EnsureEnemyHpBarAlignment、SyncCombatantSlotLayout
  BattleInventoryPanelView.cs              # 背包等级名
  ExpeditionNodeInteractOverlayView.cs     # EventAftermath UI

Tests/Battle/
  CombatXpRulesTests.cs
  RelicGrowthRulesTests.cs
  EnemyFloorScalingTests.cs

Content/Editor/
  MonsterContentGenerator.cs               # 小怪数值
```

---

## 七、测试建议

### 数值 / 远征

- [ ] 重新 Generate Demo SO 后，洞穴普通战 XP 在 8–10
- [ ] 10 层拿成长遗物 → 传送门跳 20 层 → 遗物数值是否 +1 档
- [ ] 魔法泉水：确认页 **只有一句** 命中描述
- [ ] 任意事件：选选项 → 描述 → 确定 → 效果顺序正确
- [ ] 背包角色名显示 `战士Lv2` 等

### 战斗演出

- [ ] 巨魔：战争怒吼动画 **结束前** 悬停 ATK 不变，播完后才升
- [ ] 巨魔：受击后血怒在 **受击动画结束** 才出现在框内
- [ ] 巨魔：攻击牌结束后血怒才清零
- [ ] 六人血条 idle 时 **同一水平线**（对齐玩家中间位）
- [ ] 敌人出牌到中央：血条 **横移跟随**，相对立绘位置不变
- [ ] 归位：**tween 滑回**，不闪现
- [ ] 0 伤害攻击：目标仍有 **受击反馈**

---

## 八、未提交 / 后续可选

- 本会话改动 **尚未统一 git commit**
- 战斗框内等级（`战士Lv2`）目前主要在 **背包**；槽位 `nameText` 仍可能只显示 `DisplayName`，若要在战场脚下也显示等级可另开
- 部分复杂特性仍为简化实现（换位加血、延迟召唤等）
- 全链路 1–60 层连续远征平衡需人工 Play 验证

---

## 九、给下次会话的提示

1. 先读本文 + [SessionHandoff_2026-06-14.md](SessionHandoff_2026-06-14.md)
2. 若改怪物数值：Excel → Generator → **Generate Demo ScriptableObjects**
3. **血条 / 立绘布局** 相关改动只动 `CombatantSlotView` 的锁定/跟随逻辑，避免再引入「每帧 AlignStatusBelowPortrait」或「Refresh 里 Reset anchor」
4. **演出 UI** 改展示时机时，优先扩展 `PresentationSnapshot` 检查点，不要直接读 live `CombatantState`

---

*文档版本：2026-06-15 · 供下次会话快速恢复上下文。*
