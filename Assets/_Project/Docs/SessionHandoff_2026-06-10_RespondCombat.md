# Grimhand 开发交接总结 — 应对机制 / 目标随机 / 战斗演出

**日期：** 2026-05-27（本会话）  
**项目路径：** `c:\Users\Kelthuzad\Documents\GitHub\Grimhand\Grimhand`  
**Unity：** 6000.4.5f1 · URP · Input System  

**相关旧文档：** [SessionHandoff_2026-05-27.md](SessionHandoff_2026-05-27.md)（远征/关键词首版）、[SessionHandoff_2026-06-04.md](SessionHandoff_2026-06-04.md)（远征事件/消耗品）

---

## 一、本会话完成事项（概览）

| 类别 | 内容 |
|------|------|
| **敌方选目标** | 【前/中】【中/后】【前/中/后】在射程内**真随机**；规划阶段预掷目标；玩家手牌仍**手动选目标** |
| **应对机制** | 调度、减伤登记、弹反延迟结算、目标匹配与真实攻击目标绑定 |
| **战斗演出** | 敌方攻击 + 应对角色：原位 **defend 立绘 + blocking 特效同时出现** → 受击 → 敌人归位 → 弹反反击段 |
| **敌方意图 UI** | 意图列表从回合一开始就按 **SpeedResolver 速度顺序** 排列 |
| **数值/内容修复** | 减速每层 -1 SPD；剑刃风暴 5×随机敌；混沌之触诅咒卡；骨王/阿努比斯等（更早会话延续） |
| **单元测试** | `TargetReachRulesTests`（随机池）、`RespondRulesTests`（应对调度/匹配/弹反延迟） |

---

## 二、应对机制 — 完整设计说明（必读）

> **给下次实现「应对攻击 / 应对防御 / 应对状态」的硬性参考。**  
> 关键词 `respond_defense`、`respond_status` 已在 `KeywordCatalog` 有文案，**匹配逻辑尚未实现**；当前只有 **应对攻击（parry / LastActionAttackOnSelf）** 走通全链路。

### 2.1 概念分层

| 层 | 职责 | 关键文件 |
|----|------|----------|
| **识别** | 什么是「应对卡」 | `RespondRules.IsRespondCard` |
| **匹配** | 应对卡是否绑到某次**敌方出牌** | `RespondTriggerMatcher` + `ReactionRules.MeetsRespondCondition` |
| **调度** | 何时结算应对 vs 敌方攻击 | `RespondResolutionPlanner.BuildSchedule` |
| **逻辑执行** | 登记减伤 / 排队弹反（**不在此步造成伤害**） | `RespondEffectExecutor.Execute` |
| **伤害结算** | 敌方攻击时应用减伤；攻击段落后结算弹反 | `DamageRules.ApplyDamage` + `RespondEffectExecutor.ResolvePendingParriesForEnemyCard` |
| **演出** | defend + blocking、弹反反击立绘 | `BattlePortraitDirector` |

### 2.2 什么是「应对卡」

```csharp
// RespondRules.cs
IsRespondCard(card) =>
  card.Keywords.Contains("parry")   // 如「铁壁弹反」
  OR 任意 action.Condition != None  // 带条件的效果也算应对卡
```

- **应对卡仍是玩家正常出牌**：占能量、进 `PlayerPlan.PlayQueue`、按速度参与 `SpeedResolver`。
- **应对卡结算时不走 `PortraitPoseChanged`**（`ResolveRespondStep` 只有 `CardResolvedStarted/Ended`），视觉在**被绑定的敌方攻击段**里表现。

### 2.3 匹配规则（当前已实现：应对攻击）

**核心原则：必须匹配「该次敌方攻击的真实目标」是该角色，才触发应对。**

匹配入口：`RespondTriggerMatcher.RespondCardMatchesEnemyStep(state, respondOwner, respondCard, enemyStep)`

步骤：

1. `EnemyStepHasAttack` — 敌方这一步牌是否含 `DealDamage`。
2. `WouldEnemyStepAttackCombatant(state, enemyStep, respondOwner.Id)` — 用与结算相同的 `TargetRules.ResolveTarget` + 预掷的 `ResolutionTargets`，判断**该次攻击是否会打到这名角色**（含 AOE、射程、补位）。
3. 应对卡上带 `Condition != None` 的 action：逐个 `ReactionRules.MeetsRespondCondition`。
4. 若卡带 `parry` 且无显式 condition action， fallback 为 `LastActionAttackOnSelf`。

**当前唯一实现的 Condition：**

```csharp
ReactionConditionType.LastActionAttackOnSelf
  => WouldEnemyStepAttackCombatant(..., respondOwnerId)
```

**故意不触发的情况（测试已覆盖）：**

- 敌方【中/后】攻击，目标随机到中排法师 → **前排战士的应对不匹配**，静默消耗（`ApplyConditionalEffects = false`）。
- 敌方打 buff/加固等非攻击牌 → 不匹配，应对无效果无演出。

### 2.4 调度顺序（RespondResolutionPlanner）

输入：`SpeedResolver.BuildResolutionOrder` 得到的 **baseline**（纯速度轮询顺序）。

算法概要：

```
对每个 baseline 中的敌方攻击 step：
  收集所有「匹配该 step」且未被消费的应对 step
  按 PlayerPlan 选牌顺序排序
  依次插入 ScheduledResolution { RespondContext = 该敌方 step, ApplyConditionalEffects = true }
  再插入 ScheduledResolution { Step = 敌方 step 本身 }
  标记这些应对卡为已消费

对 baseline 中剩余的应对卡：
  若无任何敌方 step 匹配 → 插入 { ApplyConditionalEffects = false }（静默出牌，无条件效果）

其余 step 原样插入
```

**玩家确认的演出顺序（有应对 + 弹反）：**

1. 前序行动正常播完  
2. **应对逻辑步**（无立绘，只登记减伤/弹反队列）  
3. **敌方攻击段**：敌人 → 中央 → 攻击姿态 → **被攻击且已应对的角色原位 defend + blocking（同时）** → 受击 → 敌人归位  
4. **弹反段**（`ResolvePendingParriesForEnemyCard` 发出的事件）：应对者 → 中央 → 攻击姿态 → 敌人受击 → 应对者归位  

### 2.5 逻辑执行（RespondEffectExecutor）

#### 2.5.1 应对步 `Execute`（敌方攻击**之前**）

在 `ResolveRespondStep` 中，当 `entry.RespondContext` 有值且 `ApplyConditionalEffects == true`：

| Action 类型 | 行为 | 注意 |
|-------------|------|------|
| `GainBlockFromLastDamagePercent` | `RegisterMitigation(enemyCardId, defenderId, percent)` | **不立刻加 block UI**，是百分比减伤层 |
| `ReflectLastDamageToAttacker` | 写入 `PendingParryStrikes` | **不立刻 ApplyDamage** |

`EstimateIncomingPower` 用于估算反射伤害基数（与真实目标、Reach 调整一致）。

#### 2.5.2 敌方攻击伤害 `DamageRules.ApplyDamage`

顺序（与演出相关部分）：

1. Outgoing × 站位 incoming 倍率  
2. 扣 **Block** → `BlockedAmount`  
3. DEF 减伤 → `hpDamage`  
4. **`RespondEffectExecutor.ApplyMitigation(state, sourceCardInstanceId, recipient.Id, hpDamage)`**  
5. 写 `DamageApplied` 事件，字段包括：  
   - `RespondMitigatedAmount`  
   - **`HadRespondDefense`** = 该 enemyCardId + 该 targetId 在 `RespondMitigationByEnemyCard` 或 `PendingParryStrikes` 中有登记  

#### 2.5.3 弹反延迟结算

`BattleEngine.ResolveStep` 在敌方牌 `PortraitIdleRestored` **之前**的逻辑末尾：

```csharp
if (actor.Team == TeamSide.Enemy)
    RespondEffectExecutor.ResolvePendingParriesForEnemyCard(_state, card.InstanceId, _events, _rng);
```

发出事件链：

1. `PortraitPoseChanged`（应对者，Attack 姿态）  
2. `ParryTriggered`（演出层忽略，由 pose 段驱动）  
3. `DamageApplied`（应对者 → 攻击者）  
4. `PortraitIdleRestored`（应对者归位）  

**为什么延迟：** 若弹反在 `ResolveRespondStep` 就 `ApplyDamage`，`DamageApplied` 会排在敌方攻击动画之前，HP 数字先于演出更新。

### 2.6 战斗演出（BattlePortraitDirector）

#### 敌方打玩家且该角色有应对/格挡

`PlayDamageReactionOnly`：

```text
若 (BlockedAmount > 0 或 HadRespondDefense) 且目标是玩家：
  RunParallel:
    PlayInPlacePose(Defense)      // defend 立绘
    PlayBlockingEffect()          // blocking 特效
  // 两者同时开始、等两者都结束
→ 若有 hpDamage：PlayHitReaction
→ 若完全格挡/应对挡空：PlayBlockedReaction
```

**曾犯错误（勿再犯）：**

| 错误 | 后果 |
|------|------|
| defend 与 blocking **顺序**播放 | 用户看到先 pose 后特效 |
| 只判断 `RespondMitigatedAmount > 0` | 伤害被护甲/DEF 吃光时 mitigated=0，不播 defend |
| 弹反在应对步立刻 ApplyDamage | HP 早于动画 |
| 敌方攻击段用 Parallel 叠 overlay + reaction 导致节奏乱 | 已改为顺序：先完整 defend 段再受击 |

#### 应对步本身

- **没有** `PortraitPoseChanged` → 规划/结算时应对牌不会单独「出场」。  
- 视觉全部绑在：**受击 defend 段** + **弹反反击段**。

### 2.7 BattleState 应对相关字段

```csharp
// key = 敌方攻击牌的 CardInstanceId
Dictionary<int, List<RespondMitigationLayer>> RespondMitigationByEnemyCard;

List<PendingParryStrike> PendingParryStrikes;
// PendingParryStrike: TriggerEnemyCardInstanceId, DefenderId, AttackerId, Damage, RespondCardInstanceId

Dictionary<int, string> ResolutionTargets;  // 预掷/玩家选手的目标，匹配与结算共用
```

每回合 `ResolveTurn` 开头清空 `RespondMitigationByEnemyCard` 与 `PendingParryStrikes`。

### 2.8 标准应对攻击卡数据示例（铁壁弹反）

```yaml
Keywords: [parry]
Actions:
  - Type: GainBlockFromLastDamagePercent
    Target: Self
    Value: 50
    Condition: LastActionAttackOnSelf
  - Type: ReflectLastDamageToAttacker
    Target: LastActionActor
    Value: 100
    Condition: LastActionAttackOnSelf
```

- 第一张 action：登记 50% 减伤层（对**该次** enemyCardInstanceId）。  
- 第二张 action：按 `EstimateIncomingPower` 排队弹反伤害。  
- `ExecuteUnconditionalActions` 在应对步也会跑（若有无 condition 的 action）。

---

## 三、如何扩展「应对防御」「应对状态」（下次实现清单）

关键词含义（`KeywordCatalog.cs`）：

- **`respond_defense`**：当**选择的目标**（需定义：玩家选谁？还是自身？）使用**防御牌**时，应对生效。  
- **`respond_status`**：当目标使用**状态牌**时，应对生效。  

**当前未实现。** 只有 `LastActionAttackOnSelf` + `parry` 走 `RespondTriggerMatcher` 的敌方**攻击**路径。

### 3.1 推荐实现步骤（按顺序做，避免再踩坑）

#### Step A — 扩展 Condition 枚举

`EffectEnums.cs`：

```csharp
LastActionDefenseOnTarget   // 或 OnSelf / OnSelectedAlly — 与策划对齐
LastActionStatusOnTarget
```

#### Step B — 扩展 `ReactionRules.MeetsRespondCondition`

**不要**在 `MeetsCondition`（非应对路径）里写 true；应对专用逻辑只在 `MeetsRespondCondition`。

伪代码：

```csharp
LastActionDefenseOnTarget =>
    enemyStep 的 card.CardType == CardType.Defense
    && 目标解析命中 respondOwner（或「被监视的 ally」——需新规则）

LastActionStatusOnTarget =>
    enemyStep 的 card.CardType == CardType.Status
    && 同上目标匹配
```

#### Step C — 扩展 `RespondTriggerMatcher`

- 新增 `EnemyStepHasDefense` / `EnemyStepHasStatus`（或统一的 `EnemyStepMatchesCardType`）。  
- **`WouldEnemyStepXOnCombatant` 必须与真实结算一致**：  
  - 若防御/状态牌也有 Reach / 手动目标，复用 `TargetRules.ResolveTarget` + `ResolutionTargets`。  
  - 若未来是「监视一名队友」，需要在 `PlanningDraft` 存 monitor target，写入 `ResolutionTargets` 或新字典。

#### Step D — 扩展 `RespondResolutionPlanner`

今天 planner 只对 `EnemyStepHasAttack` 做「应对前置插入」。  
应对防御/状态应对**同一套插入模式**，只是匹配函数不同：

```csharp
if (actor.Team == TeamSide.Enemy && EnemyStepTriggersRespond(state, step, respondCard))
{
    // 与攻击相同：先所有 matching responds，再 enemy step
}
```

**一张敌方牌只应绑定一轮应对**（与攻击相同）；多种 respond 关键词按 PlayerPlan 顺序排队。

#### Step E — 条件效果 Action

- 若应对防御是「复制 block」「反制 debuff」等，在 `RespondEffectExecutor.ExecuteAction` 加 case。  
- **需要延迟到敌方牌结算后演出的效果** → 学弹反：登记 pending 结构，在 `ResolveStep` 敌方 `PortraitIdleRestored` 前/后统一 resolve（与策划确认顺序）。

#### Step F — 演出

- 防御/状态应对的**视觉锚点**可能不是 `DamageApplied`：  
  - 应对防御：可能在敌方 `GainBlock` 或 `PortraitPoseChanged(Defense)` 段给被监视角色加特效。  
  - 应对状态：在 `StatusApplied` 段。  
- 复用 `HadRespondDefense` 模式：在对应 `BattleEvent` 上挂 `HadRespondX` 标志，**在登记时判定**，不要只靠 amount>0。

#### Step G — 测试（必须）

复制 `RespondRulesTests.cs` 模式：

1. 匹配时：schedule 中 respond 紧挨 enemy step 之前  
2. 不匹配时：`ApplyConditionalEffects = false`，无 mitigated / 无 pending  
3. Reach 排除（如中后排打法师，前排 respond 不触发）  
4. 演出事件顺序（若测 event list）：defend+blocking 同段、弹反在 idle 后  

### 3.2 常见坑（本会话已踩）

| 坑 | 正确做法 |
|----|----------|
| 用「默认前排」代替随机目标 | 敌方自动目标：`PickRandomTargetForReach` + `PrerollEnemyAutoTargets` |
| 意图顺序按出牌选牌/费用排序 | `EnemyTurnPlanner.SortIntentsByResolutionSpeed` 用 `SpeedResolver` |
| 应对判定用固定前排 | `ResolveTarget(..., Reach, action)` + 预掷 targets |
| 玩家牌也被随机 | 只有 `EnemyPlan` 预掷；玩家 `PlanningDraft` 选手动目标 |
| 隐藏意图泄露目标 | 隐藏意图纯 `#N ? (敌人名)`，不显示 target note |
| `RespondMitigatedAmount==0` 不播 defend | 用 `HadRespondDefense` |

---

## 四、敌方自动选目标（随机 + 预掷）

### 4.1 规则

| Reach | 随机池 |
|-------|--------|
| `FrontAndMiddle` | 存活且有效位在前/中的敌人 |
| `MiddleAndBack` | 中/后 |
| `Any` | 全体合法目标 |
| 嘲讽 | 在射程内则**强制**嘲讽，不随机 |

实现：`TargetRules.CollectReachCandidates` → `PickRandomCandidate(rng)`。

### 4.2 目标锁定

- 第一次 `ResolveTarget` 掷出后写入 `ResolutionTargets[cardInstanceId]`，同一张牌多段 action 共用。  
- **规划阶段**：`BeginPlanning` → `PrerollEnemyAutoTargets(EnemyPlan)`，意图 UI 的 `→ 目标` 与结算一致。  
- **提交计划**：`CommitPlanInternal` 先备份敌方 targets，清表后写玩家 targets，再恢复敌方 targets。

### 4.3 玩家选手动目标

不变：`PlanningDraft.ShouldPromptForTarget` → 未选目标不能入队。

---

## 五、敌方意图按速度排序

`EnemyTurnPlanner.PrepareEnemyTurn` 生成 intents 后：

```csharp
SortIntentsByResolutionSpeed(state, result, rng.Copy());
// 用空 PlayerPlan + 当前 EnemyPlan 调用 SpeedResolver.BuildResolutionOrder
// 按 step 顺序重排 Intents 并更新 OrderIndex
```

UI：`BattleScreenView.RefreshActionTimeline` 在「仅敌方意图、玩家尚未选牌」时按 `state.EnemyIntents` 顺序显示 `#1 #2 #3`。

---

## 六、其他本会话相关修复

| 项 | 文件/说明 |
|----|-----------|
| 减速 | `StatusCatalog`：每层 **-1 SPD**（对齐策划表） |
| 剑刃风暴 | `Card_w_blade_storm.asset`：5× `RandomEnemy`；UI `TryDescribeRepeatedRandomHits` |
| 混沌之触 | `Card_curse_chaos_touch.asset`；远征事件污染，非怪物卡池 |
| 敌方意图目标 | 可见意图 `→ 角色名`；隐藏不泄露 |
| 编译 | 测试勿用 `Is.AnyOf`；用 `target.Id is "a" or "b"` |

---

## 七、关键文件索引

```
Assets/_Project/
  Docs/
    SessionHandoff_2026-05-27_RespondCombat.md   ← 本文
  Scripts/
    Battle/
      BattleEngine.cs                 # ResolveTurn / ResolveRespondStep / BeginPlanning / CommitPlanInternal
      AI/EnemyTurnPlanner.cs          # 意图 + 速度排序
      Effects/
        TargetRules.cs                # 随机目标、预掷、PredictIntentTarget
        DamageRules.cs                # HadRespondDefense、减伤、DamageApplied
        EffectActionExecutor.cs
      Reactions/
        RespondRules.cs               # IsRespondCard
        RespondResolutionPlanner.cs   # 调度
        RespondEffectExecutor.cs      # 登记减伤、延迟弹反
        RespondTriggerMatcher.cs      # 匹配真实攻击目标
        ReactionRules.cs              # MeetsRespondCondition（扩展点）
      Rules/
        SpeedResolver.cs              # 速度顺序
        CardReachFormatter.cs
      Events/BattleEvent.cs           # HadRespondDefense, RespondMitigatedAmount
    Presentation/Battle/
      BattlePortraitDirector.cs       # defend + blocking 并行、弹反段
      BattleScreenView.cs             # 意图面板
      BattleUiFormatters.cs           # 意图目标文案
  Tests/Battle/
    RespondRulesTests.cs
    TargetReachRulesTests.cs
```

---

## 八、测试与验证

```text
Window → Test Runner → EditMode → Grimhand.Battle.Tests
```

重点用例：

- `Respond_InterceptsBeforeFirstMatchingEnemyAttack`  
- `Respond_DoesNotMatchFrontRow_WhenEnemyOnlyHitsMiddleReach`  
- `AutoTarget_*_RandomWithinReachPool`  
- `PrerollEnemyAutoTargets_MatchesResolve`  

**Unity 内人工验证：**

1. 敌方【前/中】多次攻击，目标在战士/中排间随机  
2. 战士应对 + 敌人打法师 → 应对不触发  
3. 应对触发：defend 与 blocking **同时**出现，再受击，再弹反段  
4. 意图 `#1#2#3` 顺序与速度快照一致  

---

## 九、给下次 AI 助手的一句话

> **应对攻击已全链路打通：匹配真实目标 → 调度前置 → 登记减伤/排队弹反 → 敌方攻击段 defend+blocking 并行 → 攻击后弹反结算。扩展应对防御/状态时，复制 planner+matcher+executor 模式，新增 Condition 与 EnemyStep 类型判定，并单独设计 StatusApplied/Block 段的演出锚点；勿在应对步直接 ApplyDamage 除非不需要延迟演出。**

---

*本文件由 2026-05-27 应对/目标/演出会话整理，供后续接力使用。*
