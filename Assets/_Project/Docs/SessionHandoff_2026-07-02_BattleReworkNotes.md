# Grimhand 开发交接 — 战斗/卡牌机制审计与补丁（待大改）

**日期：** 2026-07-02  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**状态：** ⚠️ **用户决定暂停继续 patch，计划重新设计战斗逻辑。** 本文档仅记录本会话成果、权威设计意图与已知问题，供大改时对照。  
**相关旧文档：** [SessionHandoff_2026-06-10_RespondCombat.md](SessionHandoff_2026-06-10_RespondCombat.md)

---

## 一、用户结论（2026-07-02）

- 当前代码层对「卡牌机制」的理解与实现**不可靠**，补丁式修改容易越改越乱。
- **停止继续改战斗逻辑**，后续以**重新设计**为主。
- 本文档 = 今天做了什么 + 设计规则原文 + 哪里还没搞对。

---

## 二、用户明确的卡牌/演出规则（权威，大改时必须保留）

### 2.1 应对成功 → 防御姿势，不是受击

- 成功应对攻击时，目标角色应表现为 **防御（defend）姿势**。
- **不应**再播放受击（hit）立绘；可以保留伤害数字、格挡吸收、闪白等反馈。
- 即使仍有部分伤害穿过减伤，视觉上仍优先「防御反应」，而非「被打脸」。

### 2.2 动画过后再更新 UI（尤其状态图标）

- 逻辑可以一次性算完，但**表现层**必须跟动画进度对齐。
- 典型问题：选了加伤害/上状态的牌，**状态脚底图标立刻出现** —— 错误；应在对应 `StatusApplied` **动画播完后**才显示。
- HP / 护甲已有 `PresentationSnapshot` 部分支持；**脚底状态图标**在本会话中尝试单独快照，但整体验证不充分。

### 2.3 应对卡文案标点 = 触发条件（核心）

| 标点 | 语义 | 数据层对应（本会话约定） |
|------|------|--------------------------|
| **逗号 `，`** | 必须**应对成功**后才触发 | `ReactionConditionType.LastActionAttackOnSelf` 等 **Condition ≠ None**（应对段） |
| **句号 `。`** | 本回合**即使没应对到**也会触发 | `Condition = None`（无条件段） |
| **「若应对失败…」** | 仅未匹配/未成功时触发 | 本会话新增 `RespondArmFailed`（见下） |

**反例（用户明确投诉）：**

- **叹息之墙 `l_wall_of_sighs`**：描述为「随机一名我方角色获得虚化」，曾被配成 `FrontAlly` + 需手选 + 第二段 `Condition: 0`（失败也虚化）—— 属于**乱改目标/乱改条件**。

---

## 三、本会话 Earlier 已完成（非「应对」专题，但同周累积）

| 类别 | 内容 | 关键位置 |
|------|------|----------|
| 远征队人数 | 强制 3 人，与军营一致 | `ExpeditionPartyRules`、营地/战斗模板裁剪 |
| 能量语义 | **获得**可超上限；**回复**封顶 | `EnergyRules.GainTemporary` / `Restore` |
| 巫妖立绘 | hit/defend 子图绑错 | `CharacterVisualCatalog_Demo.asset`、bootstrap 修复 |
| 虚化禁牌 | 规划阶段多张牌锁不住 | `CardLockRules`、`PlanningDraft`、`BattleEngine` |

---

## 四、本会话「应对/演出/卡牌 asset」改动清单

> **注意：** 以下改动已进仓库工作区，**未经用户满意验收**；大改时可整段 revert 或作参考。

### 4.1 代码

| 文件 | 改动意图 |
|------|----------|
| `BattlePortraitDirector.cs` | 应对成功时走防御姿势 + 闪白/数字，避免 Hit 立绘 |
| `CombatantPortraitView.cs` | 新增 `ShowHpDamageNumber` |
| `PresentationSnapshot.cs` | 新增 `_footStatuses`、`FootStatusEntry`、`ApplyFootStatusApplied` |
| `CombatantFootStatusIconsView.cs` | 支持从快照条目刷新，而非只读 live `Statuses` |
| `CombatantSlotView.cs` | 演出期间用脚底状态快照 |
| `EffectEnums.cs` | 新增 `EffectTarget.RandomAlly`、`ReactionConditionType.RespondArmFailed` |
| `TargetRules.cs` | `PickRandomAlly` |
| `RespondEffectExecutor.cs` | 随机友方传 `rng`；`LockSelfCards` 进应对执行分支 |
| `EffectActionExecutor.cs` | `ExecuteFailedRespondActions` |
| `BattleEngine.cs` | 应对失败时调 `ExecuteFailedRespondActions` |
| `CardDescriptionCatalog.cs` | `l_wall_of_sighs` →「随机一名我方角色」 |

### 4.2 卡牌 Asset（本会话动过的）

| CardId | 本会话修改 | 设计意图 |
|--------|------------|----------|
| `l_wall_of_sighs` | Action2：`Target=RandomAlly(15)`，`Condition=1` | 逗号段：随机友方虚化，仅应对成功 |
| `m_grudge_guard` | 中毒 `Condition=RespondArmFailed(4)` | 「若应对失败则中毒」，不能成功也中毒 |
| `m_golem_unmovable` | 补 `defense_up×5` Condition 0 | 句号段：+5 DEF 无论是否应对 |
| `m_final_guard` | 第三段禁回能 `Condition=1` | 逗号段，非句号段 |
| `m_spider_wrap` | 补 `LockSelfCards`→攻击者 Condition 1 | 逗号段：锁攻击者（实现可能仍不完整） |

### 4.3 编译修复

- `RespondEffectExecutor.ResolveRespondTarget` 漏传 `BattleRng rng` → 已补参数（CS0103）。

---

## 五、卡牌审计摘要（本会话 grep/对照，非全量自动化）

### 5.1 带 `respond_attack` 的玩家卡

| CardId | 描述要点 | 本会话判断 |
|--------|----------|------------|
| `l_wall_of_sighs` | 80%减伤 + 随机友方虚化 | **原 asset 错误**，已改（待重做系统后复核） |
| `v_poison_scale` | 50%减伤 + 攻击者中毒 | 两 action 均 Condition 1，目标 `LastActionActor` ✓ |
| `v_queen_prevention` | 60%减伤 | 单 action Condition 1 ✓ |
| `v_tongue_sense` | 应对状态 | `respond_status`，非 attack 专题 |

### 5.2 带 `parry` 的应对攻击卡（节选）

| CardId | 句号段 | 本会话发现 |
|--------|--------|------------|
| `m_golem_unmovable` | +5 DEF | asset **缺第二 action**，已补 |
| `m_grudge_guard` | 失败中毒 | asset 中毒曾绑 Condition 1（**成功也毒**），已改 Failed |
| `m_final_guard` | +8 护甲 | GainBlock Condition 0 ✓；禁回能曾 Condition 0 **错误** |
| `m_spider_wrap` | 锁攻击者 | asset **缺第二 action**；`LockSelfCards` 是否等于「无法使用攻击牌」**未验证** |

### 5.3 生成器 vs Asset 漂移

- `MonsterContentGenerator.Dungeon.cs` / `Abyss.cs` 里部分卡定义**比实际 .asset 完整**（如 `GolemUnmovable`、`SpiderWrap` 生成器有两段，asset 曾只有一段）。
- **教训：** 改卡应同时改 Generator + asset，或废弃 Generator 单源，否则必 drift。

---

## 六、当前架构的结构性问题（大改时应优先解决）

### 6.1 应对机制表达力不足

- **逗号/句号/失败** 靠 `Condition` 枚举硬编码，缺少与文案绑定的声明式层。
- `RespondEffectExecutor` 只特殊处理部分 `EffectActionType`（减伤登记、弹反、ApplyStatus…），很多 action 在应对路径**根本进不去**。
- `damage_reduction` 用 **ApplyStatus** vs **GainBlockFromLastDamagePercent** 混用 → `HadRespondDefense`、演出、减伤登记行为**不一致**。

### 6.2 演出快照与逻辑快照不同步

- `RecordPresentationCheckpoints` 在**整段结算完成后**批量录制，checkpoint 可能是**最终态**，不是「事件 i 时的态」。
- HP/护甲有增量 `ApplySnapshotAfter*`；状态图标本会话加了 `_footStatuses`，但 **StatusRemoved / StatusExpired / 回合末 tick** 未完整覆盖。
- `ApplyEventDisplayCheckpoint` 与 foot statuses 两套逻辑，长期会打架。

### 6.3 目标系统

- `FrontAlly` / `ManualSelected` / `RandomAlly` 语义分散在 `CardRules`、`TargetRules`、`RespondEffectExecutor`。
- 应对卡不应出现「手选 FrontAlly」类配置，但 **CardRules 只扫 Condition 0 的 action** 决定是否弹目标 —— 若条件/action 配错，UI 与结算都会怪。

### 6.4 失败应对 / 成功应对的分支

- 调度：`RespondResolutionPlanner` → `ApplyConditionalEffects true/false`。
- 成功：`RespondEffectExecutor` + 可能 `ExecuteUnconditionalActions`。
- 失败：仅 `ExecuteUnconditionalActions`（句号段）+ 本会话加的 `ExecuteFailedRespondActions`。
- **「若应对失败」** 不是句号规则，需要第三类分支 —— 用 `RespondArmFailed` 是补丁，不是干净设计。

### 6.5 锁定 / 禁攻击牌

- `LockSelfCards` 锁**全部出牌**，不是「仅攻击牌」；蛛网包裹文案与实现可能不符。
- 锁定回合递减 `CardLockRules.ProcessTurnStart` 挂载范围需全链路核对。

---

## 七、大改建议方向（记录用户意图，非实施承诺）

1. **卡牌效果 DSL**  
   - 从描述解析或 SO 显式字段：`OnRespondSuccess[]` / `OnPlayAlways[]` / `OnRespondFailed[]`。  
   - 不再靠 `Condition` 整数 + 标点隐式约定。

2. **统一 Effect 管道**  
   - 应对 / 正常出牌 / 被动反应 **同一套 Executor**，仅 Trigger 不同。  
   - 减伤统一走 `RespondMitigationLayer` 或统一 Status，二选一。

3. **Presentation 事件溯源**  
   - 每个 `BattleEvent` 携带 **display delta**（HP/block/status 变化），演出按 event 应用 delta，而非读 live state 或事后 checkpoint。

4. **内容单源**  
   - Excel / Generator / `.asset` 三源合一；审计脚本对照「描述标点 ↔ action 列表」。

5. **测试**  
   - 每张应对卡至少一条：成功触发 / 失败不触发逗号段 / 句号段仍触发 / 目标类型（Random vs Manual）。

---

## 八、关键文件索引（战斗重做入口）

```
Assets/_Project/Scripts/Battle/
  BattleEngine.cs                 # 结算主循环、应对调度入口
  Reactions/
    RespondRules.cs
    RespondResolutionPlanner.cs
    RespondTriggerMatcher.cs
    RespondEffectExecutor.cs
  Effects/
    EffectActionExecutor.cs
    TargetRules.cs
    DamageRules.cs
  Rules/
    CardRules.cs                  # 规划阶段是否手选目标
    CardLockRules.cs

Assets/_Project/Scripts/Presentation/Battle/
  BattlePortraitDirector.cs       # 立绘/受击/防御演出
  PresentationSnapshot.cs
  CombatantFootStatusIconsView.cs
  CombatantSlotView.cs

Assets/_Project/Scripts/Content/
  CardDescriptionCatalog.cs         # 中文描述权威（与 asset 应对齐）

Assets/_Project/Data/Cards/         # CardDefinitionSO assets
```

---

## 九、未做 / 未验证（别当成已完成）

- [ ] 全卡库自动审计脚本（描述标点 ↔ asset actions）
- [ ] 叹息之墙、终焉守护等**进战斗实测**（用户未确认满意）
- [ ] 应对成功 + 部分伤害时的演出是否在所有减伤路径上一致（Status 减伤 vs Mitigation 层）
- [ ] 状态图标：移除/过期/回合开始 tick 的快照同步
- [ ] `m_spider_wrap` 锁攻击 vs 锁全部出牌
- [ ] 重新设计后是否保留 `RespondArmFailed` 枚举

---

## 十、Git 备注

本会话涉及大量 **未 commit** 的卡牌 asset 与脚本（见当时 `git status`）。大改前建议：

```bash
git status
git diff --stat
```

如需保留今天补丁作分支：`git checkout -b wip/2026-07-02-battle-audit`  
如需放弃战斗 patch、只留文档：自行 selective revert 第四节文件列表。

---

*文档结束 — 战斗逻辑以用户重新设计为准，本文不替代设计 spec。*
