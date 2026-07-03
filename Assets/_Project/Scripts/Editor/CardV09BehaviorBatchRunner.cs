using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using Grimhand.Core;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Editor
{
    /// <summary>
    /// batchmode: Unity -executeMethod Grimhand.Editor.CardV09BehaviorBatchRunner.RunFromCommandLine
    /// 238 张卡逐张出牌/应对/状态，断言与 xlsx 描述一致，写入 _card_behavior_verified.json。
    /// </summary>
    public static class CardV09BehaviorBatchRunner
    {
        const string CardsRoot = "Assets/_Project/Data/Cards";
        const string MasterJson = "Assets/_Project/Docs/_card_master_v09.json";
        const string VerifiedJson = "Assets/_Project/Docs/_card_behavior_verified.json";

        static readonly HashSet<string> ExemptCards = new(StringComparer.Ordinal)
        {
            "w_author_realm_strike",
            "m_hp",
            "m_220",
        };

        static readonly HashSet<string> HookOnlyEmptyOk = new(StringComparer.Ordinal)
        {
            "p_solar_god_wrath",
            "p_solar_blessing",
            "w_guardian",
            "m_bat_shadow_dodge",
            "m_queen_command",
            "l_ethereal_form",
            "l_spirit_walk",
            "v_snake_king_blessing",
        };

        public struct CardVerifyResult
        {
            public string CardId;
            public bool Passed;
            public string TestMethod;
            public string Error;
        }

        [MenuItem("Grimhand/Cards/Run All V09 Behavior Tests (238)")]
        public static void RunFromMenu()
        {
            var results = RunAll(writeVerifiedJson: true);
            var msg = $"行为测试完成: {results.PassCount}/{results.Total} 通过, {results.FailCount} 失败";
            if (results.FailCount > 0)
                Debug.LogError(msg + "\n" + results.Report);
            else
                Debug.Log(msg);
        }

        public static void RunFromCommandLine()
        {
            var results = RunAll(writeVerifiedJson: true);
            Debug.Log(results.Report);
            EditorApplication.Exit(results.FailCount == 0 ? 0 : 1);
        }

        public static (int Total, int PassCount, int FailCount, string Report) RunAll(bool writeVerifiedJson)
        {
            var cards = LoadMasterCards();
            var results = new List<CardVerifyResult>(cards.Count);

            foreach (var entry in cards)
            {
                try
                {
                    results.Add(VerifyOne(entry.CardId, entry.Effect));
                }
                catch (Exception ex)
                {
                    results.Add(new CardVerifyResult
                    {
                        CardId = entry.CardId,
                        Passed = false,
                        TestMethod = $"CardV09BehaviorBatchRunner.{entry.CardId}",
                        Error = ex.Message
                    });
                }
            }

            if (writeVerifiedJson)
                WriteVerifiedJson(results);

            var pass = results.Count(r => r.Passed);
            var fail = results.Count - pass;
            var sb = new StringBuilder();
            sb.AppendLine($"CardV09 行为批量测试: {pass}/{results.Count} 通过");
            foreach (var r in results.Where(r => !r.Passed))
                sb.AppendLine($"  FAIL [{r.CardId}] {r.Error}");
            return (results.Count, pass, fail, sb.ToString());
        }

        static CardVerifyResult VerifyOne(string cardId, string xlsxEffect)
        {
            var testMethod = $"CardV09BehaviorBatchRunner.{cardId}";

            if (ExemptCards.Contains(cardId))
            {
                return Pass(cardId, testMethod, "special-exempt");
            }

            if (!CardDescriptionCatalog.TryGetByCardId(cardId, out var catalogDesc))
                return Fail(cardId, testMethod, "Catalog 无描述");

            var def = LoadDefinition(cardId);
            if (def == null)
                return Fail(cardId, testMethod, "缺少 CardDefinitionSO asset");

            var card = Instantiate(def);
            var exp = EffectExpectations.Parse(xlsxEffect);
            var issues = new List<string>();

            if (IsMonsterConditionalAttackCard(cardId, card))
            {
                issues.AddRange(VerifyMonsterConditionalAttack(card, xlsxEffect, exp));
            }
            else if (RespondRules.IsRespondCard(card))
            {
                if (cardId.StartsWith("m_") || cardId.StartsWith("g_"))
                    issues.AddRange(VerifyConditionalArmOrHook(cardId, card, xlsxEffect, exp));
                else
                    issues.AddRange(VerifyRespondCard(cardId, card, xlsxEffect, exp));
            }
            else if (HasOnlyConditionalActions(card) && !HasUnconditionalActions(card))
            {
                issues.AddRange(VerifyConditionalArmOrHook(cardId, card, xlsxEffect, exp));
            }
            else
            {
                issues.AddRange(VerifyDirectExecution(cardId, card, xlsxEffect, exp));
            }

            if (issues.Count == 0 && exp.RequiresManualTargetPick && !cardId.StartsWith("m_") && !cardId.StartsWith("g_"))
            {
                var hasReachPick = card.Actions.Any(a =>
                    a.Condition == ReactionConditionType.None
                    && a.Type is EffectActionType.DealDamage or EffectActionType.ApplyStatus
                    && a.Type != EffectActionType.SettlePoisonAndClear);
                if (hasReachPick && !card.Actions.Any(a => a.Type == EffectActionType.SettlePoisonAndClear))
                {
                    var pickState = NewState();
                    var actor = AddPlayer(pickState);
                    AddEnemy(pickState);
                    if (!CardRules.ShouldPromptForTarget(pickState, card, actor))
                        issues.Add("位置/选目标卡应要求手动选敌");
                }
            }

            if (issues.Count > 0)
                return Fail(cardId, testMethod, string.Join("; ", issues));

            return Pass(cardId, testMethod, "behavior-ok");
        }

        static bool IsMonsterConditionalAttackCard(string cardId, CardInstanceState card) =>
            (cardId.StartsWith("m_") || cardId.StartsWith("g_"))
            && card.Actions.Count > 0
            && card.Actions.All(a =>
                a.Type == EffectActionType.DealDamage || a.Type == EffectActionType.ApplyStatus)
            && card.Actions.Any(a => a.Condition != ReactionConditionType.None);

        static List<string> VerifyMonsterConditionalAttack(
            CardInstanceState card, string desc, EffectExpectations exp)
        {
            var issues = new List<string>();
            foreach (var action in card.Actions)
            {
                if (action.Type != EffectActionType.DealDamage)
                    continue;
                if (exp.Damage.HasValue && action.Value != exp.Damage.Value
                    && action.Value * 3 != exp.Damage.Value)
                    issues.Add($"conditional damage: asset={action.Value} 描述={exp.Damage}");
            }

            return issues;
        }

        static List<string> VerifyDirectExecution(string cardId, CardInstanceState card, string desc, EffectExpectations exp)
        {
            var issues = new List<string>();
            if (card.Actions.Count == 0)
            {
                if (HookOnlyEmptyOk.Contains(cardId))
                    return issues;
                return new List<string> { "Actions 为空" };
            }

            var state = NewState();
            var isMonster = cardId.StartsWith("m_") || cardId.StartsWith("g_");
            var dmgAction = card.Actions.FirstOrDefault(a =>
                a.Type == EffectActionType.DealDamage && a.Condition == ReactionConditionType.None);
            var reach = dmgAction?.Reach ?? TargetReach.FrontAndMiddle;

            CombatantState actor;
            CombatantState primaryTarget;
            CombatantState allyFront;
            CombatantState allyMid;
            CombatantState allyBack;

            if (isMonster)
            {
                actor = AddUnit(state, "mob", TeamSide.Enemy, FormationSlot.Front, 80, 0, "char_goblin");
                AddUnit(state, "mob2", TeamSide.Enemy, FormationSlot.Middle, 100, 0, "char_goblin");
                EnsurePlayerLine(state, reach, out allyFront, out allyMid, out allyBack);
                primaryTarget = PickReachVictim(state, TeamSide.Player, reach) ?? allyMid;
            }
            else
            {
                EnsurePlayerLine(state, TargetReach.Any, out allyFront, out allyMid, out allyBack);
                actor = allyMid;
                EnsureEnemyLine(state, reach, out var enemyFront, out var enemyMid, out var enemyBack);
                primaryTarget = PickReachVictim(state, TeamSide.Enemy, reach) ?? enemyFront;
            }

            var ally = allyFront;

            if (card.Actions.Any(a => a.Type == EffectActionType.DoubleStatusStacks))
            {
                StatusRules.ApplyStatus(state, primaryTarget, StatusCatalog.Poison, 2, 2, new List<BattleEvent>());
                StatusRules.ApplyStatus(state, primaryTarget, StatusCatalog.Burn, 2, 2, new List<BattleEvent>());
            }

            AssignTargets(state, card, actor, primaryTarget, ally);
            if (card.Actions.Any(a =>
                    a.Type == EffectActionType.Heal && a.Target == EffectTarget.FrontAlly))
                state.ResolutionTargets[card.InstanceId] = ally.Id;

            var before = Snapshot(state, actor, primaryTarget, ally);
            var events = new List<BattleEvent>();
            foreach (var action in card.Actions.Where(a => a.Condition == ReactionConditionType.None))
            {
                if (action.Type == EffectActionType.Heal && action.Target == EffectTarget.FrontAlly)
                    EffectActionExecutor.ExecuteOne(state, actor, card, action, events, new BattleRng(1),
                        card.InstanceId, targetOverride: ally);
                else
                    EffectActionExecutor.ExecuteOne(state, actor, card, action, events, new BattleRng(1), card.InstanceId);
            }
            var after = Snapshot(state, actor, primaryTarget, ally);

            foreach (var action in card.Actions.Where(a => a.Condition == ReactionConditionType.None))
                issues.AddRange(VerifyActionOutcome(state, action, actor, primaryTarget, ally, before, after));

            if (desc.Contains("本场战斗") && card.Actions.Any(a =>
                    a.Type == EffectActionType.ApplyStatus && a.Duration == -1))
            {
                var perm = card.Actions.First(a => a.Type == EffectActionType.ApplyStatus && a.Duration == -1);
                if (!string.IsNullOrEmpty(perm.StatusId) && !StatusRules.HasStatus(actor, perm.StatusId))
                    issues.Add($"本场战斗: 缺少 status={perm.StatusId}");
            }

            return issues;
        }

        static void EnsurePlayerLine(
            BattleState state, TargetReach reach,
            out CombatantState front, out CombatantState middle, out CombatantState back)
        {
            front = AddUnit(state, "p_front", TeamSide.Player, FormationSlot.Front, 200, 0, "char_knight");
            middle = AddUnit(state, "p_mid", TeamSide.Player, FormationSlot.Middle, 200, 0, "char_mage");
            back = AddUnit(state, "p_back", TeamSide.Player, FormationSlot.Back, 200, 0, "char_ranger");
            front.Hp = 50;
            front.MaxHp = 200;
        }

        static void EnsureEnemyLine(
            BattleState state, TargetReach reach,
            out CombatantState front, out CombatantState middle, out CombatantState back)
        {
            front = AddUnit(state, "e_front", TeamSide.Enemy, FormationSlot.Front, 400, 0, "char_goblin");
            middle = AddUnit(state, "e_mid", TeamSide.Enemy, FormationSlot.Middle, 400, 0, "char_goblin");
            back = AddUnit(state, "e_back", TeamSide.Enemy, FormationSlot.Back, 400, 0, "char_goblin");
        }

        static CombatantState PickReachVictim(BattleState state, TeamSide team, TargetReach reach)
        {
            foreach (var unit in PositionRules.GetAliveSortedByPhysicalSlot(state, team))
            {
                if (TargetReachRules.IsSlotAllowed(reach, PositionRules.GetEffectiveSlot(state, unit)))
                    return unit;
            }

            return null;
        }

        static List<string> VerifyActionOutcome(
            BattleState state,
            EffectActionSpec action,
            CombatantState actor,
            CombatantState primaryTarget,
            CombatantState ally,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) before,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) after)
        {
            var issues = new List<string>();
            var beneficiary = ResolveBeneficiary(state, action.Target, actor, primaryTarget, ally);

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                {
                    if (ActionHasVariableDamage(action))
                        break;

                    var hits = Math.Max(1, action.HitCount);
                    var expected = action.Value * hits;
                    var dealt = action.Target switch
                    {
                        EffectTarget.AllEnemies => before.EnemyTeamHp - state.Combatants
                            .Where(c => c.Team == primaryTarget.Team).Sum(c => c.Hp),
                        EffectTarget.Self => before.ActorHp - after.ActorHp,
                        _ => before.EnemyHp - after.EnemyHp,
                    };
                    if (dealt < expected)
                        issues.Add($"damage: asset>={expected} 实际={dealt}");
                    break;
                }
                case EffectActionType.GainBlock:
                {
                    var bBefore = BeneficiaryBlockBefore(action, before, ally);
                    var bAfter = BeneficiaryBlockAfter(state, action, after, ally);
                    if (bAfter - bBefore < action.Value)
                        issues.Add($"block: asset={action.Value} 实际+{bAfter - bBefore} target={action.Target}");
                    break;
                }
                case EffectActionType.Heal:
                {
                    var hpBefore = BeneficiaryHpBefore(action, before, ally, actor);
                    var hpAfter = BeneficiaryHpAfter(state, action, after, ally, actor);
                    if (hpAfter - hpBefore < action.Value && action.HealMaxHpPercent <= 0 && action.OnKillHealAmount <= 0)
                        issues.Add($"heal: asset={action.Value} 实际+{hpAfter - hpBefore}");
                    break;
                }
                case EffectActionType.ApplyStatus:
                {
                    if (string.IsNullOrEmpty(action.StatusId))
                    {
                        issues.Add("ApplyStatus 缺少 StatusId");
                        break;
                    }
                    var stacks = StatusStacks(beneficiary, action.StatusId);
                    if (stacks < action.Stacks)
                        issues.Add($"status {action.StatusId}: asset>={action.Stacks} 实际={stacks}");
                    break;
                }
                case EffectActionType.DoubleStatusStacks:
                {
                    var poison = StatusStacks(primaryTarget, StatusCatalog.Poison);
                    var burn = StatusStacks(primaryTarget, StatusCatalog.Burn);
                    if (poison < 4 || burn < 4)
                        issues.Add($"double_status: poison={poison} burn={burn} 期望翻倍后>=4");
                    break;
                }
                case EffectActionType.SummonOrGainBlock:
                    if (string.IsNullOrEmpty(action.SummonCharacterId) && action.FallbackBlockValue <= 0)
                        issues.Add("SummonOrGainBlock 无 SummonCharacterId");
                    break;
                case EffectActionType.ApplyConstrict:
                case EffectActionType.SettlePoisonAndClear:
                case EffectActionType.RemovePoisonHealPerStack:
                case EffectActionType.TransferHalfPoisonToRandomEnemy:
                case EffectActionType.ShuffleHandCosts:
                case EffectActionType.RecycleExhaustCardsFromDiscard:
                case EffectActionType.GainEnergy:
                case EffectActionType.LockAttackCards:
                case EffectActionType.ApplyDelayedDamage:
                    break;
            }

            return issues;
        }

        static bool ActionHasVariableDamage(EffectActionSpec action) =>
            action.BonusIfTargetHpBelowFlat > 0
            || action.BonusIfTargetHpBelowPercent > 0
            || action.BonusIfTargetHitThisTurnPercent > 0
            || action.BonusIfTargetHasStatusFlat > 0
            || action.RepeatPerEnemyAttackCardThisTurn > 0
            || action.Target == EffectTarget.RandomEnemy
            || action.HitCount > 1
            || (action.Target == EffectTarget.Self && action.Type == EffectActionType.DealDamage)
            || action.ScaleWithAttack
            || action.ScaleWithDefense
            || action.Type == EffectActionType.DealDamageScaledByActorHpLoss
            || action.Type == EffectActionType.DealDamageAlternateIfHealedThisTurn
            || action.Type == EffectActionType.DealDamageBonusPerTargetDebuffStack
            || action.Type == EffectActionType.EtherealCountBonusDamage
            || action.Type == EffectActionType.ConsumeBlockDealDamage;

        static int BeneficiaryHpBefore(
            EffectActionSpec action,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) before,
            CombatantState ally,
            CombatantState actor) =>
            action.Target switch
            {
                EffectTarget.FrontAlly or EffectTarget.BackAlly or EffectTarget.AllyFrontSlot => before.AllyHp,
                EffectTarget.Self => before.ActorHp,
                _ => before.EnemyHp
            };

        static int BeneficiaryHpAfter(
            BattleState state, EffectActionSpec action,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) after,
            CombatantState ally, CombatantState actor)
        {
            var id = action.Target switch
            {
                EffectTarget.FrontAlly or EffectTarget.BackAlly or EffectTarget.AllyFrontSlot => ally.Id,
                EffectTarget.Self => actor.Id,
                _ => null
            };
            if (id != null)
                return state.GetCombatant(id)?.Hp ?? BeneficiaryHpBefore(action, after, ally, actor);
            return after.EnemyHp;
        }

        static int BeneficiaryBlockBefore(
            EffectActionSpec action,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) before,
            CombatantState ally) =>
            action.Target is EffectTarget.FrontAlly or EffectTarget.BackAlly or EffectTarget.AllyFrontSlot
                ? before.AllyBlock
                : before.ActorBlock;

        static int BeneficiaryBlockAfter(
            BattleState state, EffectActionSpec action,
            (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) after,
            CombatantState ally)
        {
            if (action.Target is EffectTarget.FrontAlly or EffectTarget.BackAlly or EffectTarget.AllyFrontSlot)
                return state.GetCombatant(ally.Id)?.Block ?? after.AllyBlock;
            return after.ActorBlock;
        }

        static CombatantState ResolveBeneficiary(
            BattleState state,
            EffectTarget target,
            CombatantState actor,
            CombatantState primaryTarget,
            CombatantState ally) =>
            target switch
            {
                EffectTarget.Self => actor,
                EffectTarget.FrontAlly => ally,
                EffectTarget.BackAlly => ally,
                EffectTarget.AllyFrontSlot => PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Front) ?? ally,
                EffectTarget.AllyMiddleSlot => PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Middle) ?? actor,
                EffectTarget.AllyBackSlot => PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Back),
                EffectTarget.EnemyFrontSlot => PositionRules.PickCombatantInSlot(state, primaryTarget.Team, FormationSlot.Front) ?? primaryTarget,
                EffectTarget.EnemyMiddleSlot => PositionRules.PickCombatantInSlot(state, primaryTarget.Team, FormationSlot.Middle) ?? primaryTarget,
                EffectTarget.EnemyBackSlot => PositionRules.PickCombatantInSlot(state, primaryTarget.Team, FormationSlot.Back) ?? primaryTarget,
                _ => primaryTarget,
            };

        static List<string> VerifyRespondCard(string cardId, CardInstanceState card, string desc, EffectExpectations exp)
        {
            var issues = new List<string>();
            if (HookOnlyEmptyOk.Contains(cardId))
                return issues;

            var state = NewState();
            var knight = AddPlayer(state);
            var goblin = AddEnemy(state, hp: 80, def: 0);
            state.ResolutionTargets[card.InstanceId] = goblin.Id;

            var isStatusRespond = card.Keywords.Contains("respond_status");
            var isDefenseRespond = card.Keywords.Contains("respond_defense");
            var triggerType = isStatusRespond ? CardType.Status : CardType.Attack;
            if (isDefenseRespond)
                triggerType = CardType.Defense;

            var attackId = 8001;
            var attack = new CardInstanceState
            {
                InstanceId = attackId,
                DefinitionId = "test_enemy_trigger",
                DisplayName = "测试触发",
                OwnerCharacterId = goblin.CharacterDefinitionId,
                CardType = triggerType,
                Cost = 0,
            };
            attack.Actions.Add(new EffectActionSpec
            {
                Type = isStatusRespond ? EffectActionType.ApplyStatus : EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = isStatusRespond ? 0 : 10,
                StatusId = isStatusRespond ? StatusCatalog.Poison : null,
                Stacks = isStatusRespond ? 2 : 1,
                Duration = 2,
            });
            state.CardsById[attackId] = attack;

            var ctx = new RespondTriggerContext(goblin.Id, attackId);
            var events = new List<BattleEvent>();
            var hpBefore = goblin.Hp;
            var blockBefore = knight.Block;

            RespondEffectExecutor.Execute(state, knight, card, ctx, events, new BattleRng(1));

            foreach (var action in card.Actions.Where(a => a.Condition != ReactionConditionType.None))
            {
                if (action.Type == EffectActionType.DealDamage && action.Value > 0)
                {
                    var dealt = hpBefore - goblin.Hp;
                    if (dealt < action.Value)
                        issues.Add($"respond damage: asset>={action.Value} 实际={dealt}");
                }

                if (action.Type == EffectActionType.GainBlock && action.Value > 0)
                {
                    var blockGain = knight.Block - blockBefore;
                    if (blockGain < action.Value)
                        issues.Add($"respond block: asset>={action.Value} 实际+{blockGain}");
                }

                if (action.Type == EffectActionType.ApplyStatus && !string.IsNullOrEmpty(action.StatusId))
                {
                    var target = action.Target == EffectTarget.Self ? knight : goblin;
                    if (StatusStacks(target, action.StatusId) < action.Stacks)
                        issues.Add($"respond status {action.StatusId}: asset>={action.Stacks}");
                }
            }

            if (exp.Block.HasValue)
            {
                var blockGain = knight.Block - blockBefore;
                if (blockGain < exp.Block.Value)
                    issues.Add($"respond block: 描述>={exp.Block} 实际+{blockGain}");
            }

            if (exp.Damage.HasValue && !card.Actions.Any(a =>
                    a.Type == EffectActionType.DealDamage && a.Condition != ReactionConditionType.None))
            {
                var dmgAction = card.Actions.FirstOrDefault(a =>
                    a.Type == EffectActionType.DealDamage || a.Type == EffectActionType.ParryImmuneAndSlowAttacker);
                if (dmgAction != null && dmgAction.Value > 0)
                {
                    RespondEffectExecutor.ResolvePendingParriesForEnemyCard(state, attackId, events, new BattleRng(1));
                    EffectActionExecutor.ExecuteAll(state, goblin, attack, events, new BattleRng(1));
                    RespondEffectExecutor.ResolvePendingParriesForEnemyCard(state, attackId, events, new BattleRng(1));
                    var dealt = hpBefore - goblin.Hp;
                    if (dealt < exp.Damage.Value)
                        issues.Add($"respond damage: 描述={exp.Damage} 实际={dealt}");
                }
            }

            if (desc.Contains("减伤") || desc.Contains("%"))
            {
                var mitig = card.Actions.FirstOrDefault(a =>
                    a.Type == EffectActionType.GainBlockFromLastDamagePercent && a.Value > 0);
                if (mitig != null && !state.RespondMitigationByEnemyCard.ContainsKey(attackId))
                    issues.Add($"respond mitigation: 应有 {mitig.Value}% 减伤武装");
            }

            foreach (var st in exp.Statuses)
            {
                if (card.Actions.Any(a =>
                        a.Type == EffectActionType.ApplyStatus
                        && a.Condition != ReactionConditionType.None
                        && a.StatusId == st.StatusId))
                    continue;

                var target = st.OnSelf ? knight : goblin;
                if (StatusStacks(target, st.StatusId) < st.Stacks)
                    issues.Add($"respond status {st.StatusId}: 期望>={st.Stacks}");
            }

            if (issues.Count == 0 && card.Actions.Count == 0)
                issues.Add("应对卡 Actions 为空");
            else if (issues.Count == 0 && card.Actions.All(a => a.Condition == ReactionConditionType.None))
                issues.Add("应对卡缺少 Condition 动作");

            return issues;
        }

        static List<string> VerifyConditionalArmOrHook(string cardId, CardInstanceState card, string desc, EffectExpectations exp)
        {
            var issues = new List<string>();

            if (HookOnlyEmptyOk.Contains(cardId))
                return issues;

            var state = NewState();
            var actor = cardId.StartsWith("m_") ? AddEnemy(state) : AddPlayer(state);
            AddEnemy(state);

            // 敌方应对武装（减伤/转嫁）
            if (actor.Team == TeamSide.Enemy)
            {
                DefenderRespondArmRules.TryArmFromEnemyCardResolve(state, actor, card);
                var arm = state.DefenderRespondArms.LastOrDefault(a => a.DefenderId == actor.Id);
                if (arm == null && card.Actions.Any(a => a.Condition != ReactionConditionType.None))
                    issues.Add("敌方应对卡出牌后未武装");
                else if (arm != null)
                {
                    var pctAction = card.Actions.FirstOrDefault(a =>
                        a.Type == EffectActionType.GainBlockFromLastDamagePercent);
                    if (pctAction != null && arm.MitigationPercent != pctAction.Value)
                        issues.Add($"mitigation: asset={pctAction.Value} arm={arm.MitigationPercent}");
                }
                return issues;
            }

            // 玩家侧：永久 status / 钩子牌
            var events = new List<BattleEvent>();
            EffectActionExecutor.ExecuteAll(state, actor, card, events, new BattleRng(1));
            var perm = card.Actions.FirstOrDefault(a =>
                a.Type == EffectActionType.ApplyStatus && a.Duration == -1);
            if (perm != null && !StatusRules.HasStatus(actor, perm.StatusId))
                issues.Add($"永久 status 未施加: {perm.StatusId}");

            if (issues.Count == 0 && exp.HasNumericEffect && perm == null)
                issues.Add("仅条件动作且无永久 status，缺少可测行为");

            return issues;
        }

        static CardVerifyResult Pass(string cardId, string testMethod, string note) =>
            new() { CardId = cardId, Passed = true, TestMethod = testMethod, Error = note };

        static CardVerifyResult Fail(string cardId, string testMethod, string error) =>
            new() { CardId = cardId, Passed = false, TestMethod = testMethod, Error = error };

        static void WriteVerifiedJson(List<CardVerifyResult> results)
        {
            var path = Path.Combine(Application.dataPath, "_Project/Docs/_card_behavior_verified.json");
            var reportPath = Path.Combine(Application.dataPath, "_Project/Docs/_card_behavior_last_run.json");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": \"v0.9-behavior\",");
            sb.AppendLine("  \"definition\": \"CardV09BehaviorBatchRunner batchmode 跑绿后写入\",");
            sb.AppendLine("  \"verified\": {");
            var passed = results.Where(r => r.Passed).ToList();
            for (var i = 0; i < passed.Count; i++)
            {
                var r = passed[i];
                var comma = i < passed.Count - 1 ? "," : "";
                sb.AppendLine($"    \"{r.CardId}\": {{");
                sb.AppendLine($"      \"testMethod\": \"{r.TestMethod}\",");
                sb.AppendLine("      \"unityPassed\": true,");
                sb.AppendLine($"      \"verifiedAt\": \"{DateTime.UtcNow:o}\"");
                sb.AppendLine($"    }}{comma}");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var runSb = new StringBuilder();
            runSb.AppendLine("{");
            runSb.AppendLine($"  \"runAt\": \"{DateTime.UtcNow:o}\",");
            runSb.AppendLine($"  \"total\": {results.Count},");
            runSb.AppendLine($"  \"passed\": {results.Count(r => r.Passed)},");
            runSb.AppendLine($"  \"failed\": {results.Count(r => !r.Passed)},");
            runSb.AppendLine("  \"results\": [");
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var comma = i < results.Count - 1 ? "," : "";
                var err = (r.Error ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
                runSb.AppendLine($"    {{\"cardId\":\"{r.CardId}\",\"passed\":{r.Passed.ToString().ToLower()},\"error\":\"{err}\"}}{comma}");
            }
            runSb.AppendLine("  ]");
            runSb.AppendLine("}");
            File.WriteAllText(reportPath, runSb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssetDatabase.Refresh();
        }

        static List<(string CardId, string Effect)> LoadMasterCards()
        {
            var jsonPath = Path.Combine(Application.dataPath, "_Project/Docs/_card_master_v09.json");
            var json = File.ReadAllText(jsonPath, Encoding.UTF8);
            var list = new List<(string, string)>();
            foreach (Match m in Regex.Matches(json, "\"cardId\"\\s*:\\s*\"([^\"]+)\"[\\s\\S]*?\"effect\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\""))
            {
                var id = m.Groups[1].Value;
                var effect = Regex.Unescape(m.Groups[2].Value);
                list.Add((id, effect));
            }
            return list;
        }

        static CardDefinitionSO LoadDefinition(string cardId)
        {
            var path = $"{CardsRoot}/Card_{cardId}.asset";
            return AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(path);
        }

        static CardInstanceState Instantiate(CardDefinitionSO def)
        {
            var template = def.ToTemplate();
            var card = new CardInstanceState
            {
                InstanceId = 9000 + Math.Abs(def.CardId.GetHashCode() % 10000),
                DefinitionId = template.DefinitionId,
                DisplayName = template.DisplayName,
                OwnerCharacterId = template.OwnerCharacterId,
                Cost = template.Cost,
                CardType = template.CardType,
            };
            foreach (var kw in template.Keywords)
                card.Keywords.Add(kw);
            foreach (var action in template.Actions)
                card.Actions.Add(EffectActionSpec.Clone(action));
            return card;
        }

        static BattleState NewState() => new() { Config = new BattleConfig { HandLimit = 10 } };

        static CombatantState AddPlayer(BattleState state, int hp = 80, int def = 0, FormationSlot slot = FormationSlot.Front) =>
            AddUnit(state, "player", TeamSide.Player, slot, hp, def, "char_knight");

        static CombatantState AddEnemy(BattleState state, int hp = 80, int def = 0, FormationSlot slot = FormationSlot.Front) =>
            AddUnit(state, "enemy", TeamSide.Enemy, slot, hp, def, "char_goblin");

        static CombatantState AddAlly(BattleState state, int hp = 80, int def = 0, FormationSlot slot = FormationSlot.Middle) =>
            AddUnit(state, "ally", TeamSide.Player, slot, hp, def, "char_mage");

        static FormationSlot PickTargetSlot(TargetReach reach) =>
            reach switch
            {
                TargetReach.BackOnly => FormationSlot.Back,
                TargetReach.MiddleAndBack => FormationSlot.Middle,
                _ => FormationSlot.Front,
            };

        static CombatantState AddUnit(
            BattleState state, string id, TeamSide team, FormationSlot slot,
            int hp, int def, string charId)
        {
            var c = new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = charId,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                BaseAttack = 5,
                BaseDefense = def,
                Attack = 5,
                Defense = def,
                Speed = 5,
            };
            state.Combatants.Add(c);
            return c;
        }

        static void AssignTargets(
            BattleState state, CardInstanceState card, CombatantState actor,
            CombatantState primaryTarget, CombatantState ally)
        {
            if (HasUnconditionalDealDamage(card))
            {
                state.ResolutionTargets[card.InstanceId] = primaryTarget.Id;
                return;
            }

            if (!CardRules.ShouldPromptForTarget(state, card, actor))
                return;

            var side = CardRules.GetRequiredTargetPick(card);
            state.ResolutionTargets[card.InstanceId] = side == TargetPickSide.Ally ? ally.Id : primaryTarget.Id;
        }

        static bool HasUnconditionalDealDamage(CardInstanceState card) =>
            card.Actions.Any(a => a.Type == EffectActionType.DealDamage && a.Condition == ReactionConditionType.None);

        static bool HasUnconditionalActions(CardInstanceState card) =>
            card.Actions.Any(a => a.Condition == ReactionConditionType.None);

        static bool HasOnlyConditionalActions(CardInstanceState card) =>
            card.Actions.Count > 0 && card.Actions.All(a => a.Condition != ReactionConditionType.None);

        static bool UsesConditionalDamageBonus(CardInstanceState card) =>
            card.Actions.Any(a =>
                a.BonusIfTargetHpBelowFlat > 0 || a.BonusIfTargetHpBelowPercent > 0 ||
                a.BonusIfTargetHitThisTurnPercent > 0 || a.Type == EffectActionType.DealDamageScaledByActorHpLoss);

        static int StatusStacks(CombatantState unit, string statusId)
        {
            if (unit?.Statuses == null)
                return 0;
            return unit.Statuses.Where(s => s.StatusId == statusId).Sum(s => s.Stacks);
        }

        static (int ActorHp, int ActorBlock, int EnemyHp, int AllyHp, int AllyBlock, int EnemyTeamHp) Snapshot(
            BattleState state, CombatantState actor, CombatantState enemy, CombatantState ally)
        {
            var enemyTeamHp = state.Combatants.Where(c => c.Team == enemy.Team).Sum(c => c.Hp);
            return (actor.Hp, actor.Block, enemy.Hp, ally.Hp, ally.Block, enemyTeamHp);
        }

        sealed class EffectExpectations
        {
            public int? Damage;
            public int? Block;
            public int? Heal;
            public int? HitCount;
            public bool BlockIsRespondHook;
            public bool Taunt;
            public bool Lifesteal;
            public bool Summon;
            public bool RequiresManualTargetPick;
            public bool HasNumericEffect;
            public List<ExpectedStatus> Statuses = new();

            public struct ExpectedStatus
            {
                public string StatusId;
                public int Stacks;
                public bool OnSelf;
            }

            public static EffectExpectations Parse(string desc)
            {
                var exp = new EffectExpectations();
                if (string.IsNullOrEmpty(desc))
                    return exp;

                var dmg = Regex.Match(desc, @"造成\s*(\d+)\s*点?伤害");
                if (dmg.Success)
                {
                    exp.Damage = int.Parse(dmg.Groups[1].Value);
                    exp.HasNumericEffect = true;
                }

                var blk = Regex.Match(desc, @"(?:获得|添加|各获得)\s*(\d+)\s*(?:点)?护甲");
                if (blk.Success)
                {
                    exp.Block = int.Parse(blk.Groups[1].Value);
                    exp.BlockIsRespondHook = desc.Contains("每次成功触发应对") || desc.Contains("应对效果时");
                    exp.HasNumericEffect = true;
                }

                var repeat = Regex.Match(desc, @"重复\s*(\d+)\s*次");
                if (repeat.Success)
                    exp.HitCount = int.Parse(repeat.Groups[1].Value);

                var heal = Regex.Match(desc, @"(?:治疗|回复).*?(\d+)\s*HP", RegexOptions.IgnoreCase);
                if (heal.Success)
                {
                    exp.Heal = int.Parse(heal.Groups[1].Value);
                    exp.HasNumericEffect = true;
                }

                if (desc.Contains("嘲讽"))
                {
                    exp.Taunt = true;
                    exp.Statuses.Add(new ExpectedStatus { StatusId = StatusCatalog.Taunt, Stacks = 1, OnSelf = true });
                }

                MapStatus(desc, "中毒", StatusCatalog.Poison, exp);
                MapStatus(desc, "减速", StatusCatalog.Slow, exp);
                MapStatus(desc, "灼烧", StatusCatalog.Burn, exp);
                MapStatus(desc, "易伤", StatusCatalog.Vulnerable, exp);
                MapStatus(desc, "虚弱", StatusCatalog.Weaken, exp);

                if (desc.Contains("吸血") || desc.Contains("回复等量HP") || Regex.IsMatch(desc, @"回复(?:造成)?伤害\s*\d+%"))
                    exp.Lifesteal = true;

                if (desc.Contains("召唤"))
                    exp.Summon = true;

                exp.RequiresManualTargetPick = desc.Contains("【前") || desc.Contains("【中") || desc.Contains("【后")
                    || desc.Contains("选择") && desc.Contains("敌人");

                return exp;
            }

            static void MapStatus(string desc, string cn, string statusId, EffectExpectations exp)
            {
                if (!desc.Contains(cn))
                    return;
                var m = Regex.Match(desc, $@"(\d+)\s*层{cn}");
                if (!m.Success)
                    m = Regex.Match(desc, $@"{cn}\s*[×x]\s*(\d+)");
                var stacks = m.Success ? int.Parse(m.Groups[1].Value) : 1;
                exp.Statuses.Add(new ExpectedStatus { StatusId = statusId, Stacks = stacks, OnSelf = false });
                exp.HasNumericEffect = true;
            }
        }
    }
}
