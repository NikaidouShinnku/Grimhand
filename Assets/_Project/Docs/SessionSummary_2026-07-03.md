# Grimhand 开发日志 — 2026年7月3日

**项目路径：** `Assets/_Project/`  
**状态：** 战斗机制与 UI 多轮 patch 已落地；238/238 行为测通过。  
**关联文档：** [战斗逻辑及机制参考.docx](./战斗逻辑及机制参考.docx) §十四附 · [_card_fix_report_v09.md](./_card_fix_report_v09.md) · [_card_honest_review_v09.md](./_card_honest_review_v09.md)

---

## 一、今日目标与结论

用户要求逐项修复战斗/UI/卡牌机制问题，并同步写入策划参考文档。今日完成**战斗核心机制纠正**、**多张卡牌 asset 修复**、**UI 悬停与手牌费用**、**沙矛重塑机制三次迭代至正确版本**，以及**文档与测试**更新。

**测试现状：**

| 指标 | 结果 |
|------|------|
| 行为测试（238 张） | **238 / 238 通过** |
| strict 静态审计 | 215 / 238 OK（23 张为 Catalog/描述静态项，非行为失败） |

复跑：`Assets/_Project/Docs/_tools/run_card_tests.ps1`（需关闭 Unity Editor）

---

## 二、战斗核心机制（权威裁定）

### 2.1 无视 N% 护甲

- **错误理解：** 无视护甲 = 完全不扣真实 Block，或只扣 HP。
- **正确规则：** 按**有效护甲**折算格挡量，伤害**同时**分流到护甲与 HP。
- **示例：** 敌人 20/20 HP、10 护甲；50% 无视 + 10 伤 → 护甲剩 5，HP 15。
- **代码：** `DamageRules.ApplyDamage` + `CombatModifierRules.ComputeEffectiveBlock`

### 2.2 神圣灌注 `p_holy_infusion`

- **0 费**打出（移除 `x_cost`）。
- 规划队列中**紧接在神圣灌注后的下一张牌**费用 **+1**；能量不足则不可选。
- 结算仍靠 `holy_infusion_pending` 状态重复下一张牌。
- **代码：** `PlanningDraft.GetPlayCost`（public）、`HolyInfusionSurchargeApplies`

### 2.3 沙矛重塑 `p_sand_spear_reforge`（最终版）

- **卡牌数据：** 3 费 · 紫框（Epic）· 攻击牌 · 【消耗】
- **机制（远征级）：**
  1. 从远征开始，玩家每打出一张**消耗牌** → `SandSpearExhaustCardsPlayed + 1`（跨战斗保留）
  2. **打出沙矛重塑时**：按当前计数，重复「随机敌人 4 伤」这么多次
  3. 沙矛自身消耗结算在伤害之后，也会 +1 计数
- **错误迭代（已废弃）：** ① 每张消耗牌被动触发 4 伤；② 打出时挂永久状态 buff；③ 0 费蓝框状态牌
- **代码：** `PassiveCardMechanicsRules.OnSandSpearReforgePlayed` / `RecordExpeditionExhaustCardPlayed`  
- **持久化：** `RunModifierSnapshot.SandSpearExhaustCardsPlayed` ↔ `ExpeditionRunState.V09SandSpearExhaustCardsPlayed`

### 2.4 其他战斗修复

| 主题 | 要点 |
|------|------|
| 快速启动 | 击杀全敌后立即 `EvaluateOutcome`，不等回合结束 |
| 血族传承 | 只加 MaxHp，当前 Hp 不变（10/20 → 10/30），UI 即时刷新 |
| 血祭坛 | 献祭 -15% 自伤；叠 `SacrificeAttackStacks`；增伤反馈改 `StatusApplied`（不再误显示获护甲） |
| 瘟疫蔓延 | 中毒 tick 后 30% 向**相邻**敌人传染 `max(1,⌊层数/2⌋)`，继承剩余回合 |
| 死亡角色牌 | 污染/绑定死亡不可选；不 fallback 到其他存活同职业 |
| 浪潮冲锋 | 新增 `BonusIfActorFasterThanAllEnemiesFlat`；修复损坏 asset（12 伤 + 快于全敌 +8） |

---

## 三、UI 修复

### 3.1 角色悬停状态框

- 主信息框旁**独立状态框**（炉石式），含天神下凡、增伤、献祭增伤等。
- **位置：** 状态框作为主框**子节点**，紧贴右侧（间距 6px），随主框一起 `MountToFront`。
- **描述：** `BattleUiFormatters.FormatStatusTooltipDescriptions`  
  例：增伤 ×10 →「所有攻击牌伤害 +10%（每层 +1%）」+ 剩余回合
- **代码：** `CombatantDetailPopupView.cs`

### 3.2 手牌费用

- `HandPanelView` 使用 `draft.GetPlayCost(card)`，含神圣灌注 +1 surcharge。
- 修复 `PlanningDraft.GetPlayCost` 不可访问的编译错误。

### 3.3 其他 UI（前序轮次，今日文档化）

- 手牌选中不跳回滚动（`HandPanelView` 保留 scroll 位置）
- 护盾猛击等「消耗护甲」不再误显示为获护甲（`BlockGained` 移除分支）

---

## 四、主要改动文件

| 区域 | 文件 |
|------|------|
| 伤害/护甲 | `DamageRules.cs`, `CombatModifierRules.cs` |
| 卡牌被动 | `PassiveCardMechanicsRules.cs`, `RelicEffectRules.cs` |
| 规划/费用 | `PlanningDraft.cs`, `HandPanelView.cs` |
| 远征持久 | `RunModifierSnapshot.cs`, `ExpeditionRunState.cs`, `ExpeditionEngine.cs` |
| 条件加伤 | `CombatMechanicsRules.cs`, `EffectActionSpec.cs`, `Card_m_tide_charge.asset` |
| 悬停 UI | `CombatantDetailPopupView.cs`, `BattleUiFormatters.cs` |
| 卡牌 asset | `Card_p_sand_spear_reforge.asset`, `Card_p_holy_infusion.asset`, 等多张 |
| 测试 | `CardMechanicsV2Tests.cs` |
| 工具链 | `repair_and_sync_cards.py`, `gen_battle_reference_docx.py` |
| 文档 | 本文件、`战斗逻辑及机制参考.docx` §十四附、`_card_fix_report_v09.md` |

---

## 五、卡牌 asset 与工具链

- `repair_and_sync_cards.py`：沙矛不再误生成状态 action；移除 `CARD_TYPE_OVERRIDE` 把沙矛改成 0 费状态牌。
- 多卡 Actions/Keywords/Reach 与 xlsx 对齐（见 git 状态中 `Card_*.asset` 变更列表）。
- `gen_battle_reference_docx.py` §十四附 已写入今日各项裁定；运行脚本可刷新 docx。

---

## 六、单元测试新增

- `IgnoreDefPercent_SplitsDamageBetweenBlockAndHp`
- `BloodlineLegacy_IncreasesMaxHpWithoutHealing`
- `SandSpearReforge_DealsFourDamagePerExpeditionExhaustCount`
- `SandSpearReforge_IncrementsExhaustCounterForExpedition`
- `TideCharge_BonusWhenActorFasterThanAllEnemies`

---

## 七、已知遗留 / 下次继续

1. **strict pending 23 张**：多为 Catalog 文案或静态 check，行为已绿；可 `verify_card_strict.py --card <id>` 逐张清。
2. **PASSIVE_HANDLED 卡**：静态 OK ≠ 手测 OK；见 `_card_honest_review_v09.md`「本场战斗中」19 张等。
3. **手测建议：** 神圣灌注 +1 费选牌、沙矛先打几张消耗再打沙矛、悬停看增伤描述框、血祭坛献祭动画。
4. **CardPreviewRules**：`PreviewAoeDamagePerEnemy` 中 `actor` → `owner` 编译错误已修（2026-07-03 末）。

---

## 八、文档索引

| 文档 | 用途 |
|------|------|
| `SessionSummary_2026-07-03.md` | **本文 — 7月3日工作总结** |
| `战斗逻辑及机制参考.docx` | 策划+代码对照；§十四附 = 近期修复表 |
| `_card_fix_report_v09.md` | 238 张行为测报告 + 机制修复表 |
| `_card_honest_review_v09.md` | 静态 OK 与真实可玩性差距、高风险卡清单 |
| `CARD_BEHAVIOR_TEST.md` | 如何跑 batch 行为测 |

---

*记录人：开发会话 2026-07-03 · 下次接手请先读本文 + §十四附，再跑 `run_card_tests.ps1` 确认绿。*
