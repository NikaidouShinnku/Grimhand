using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.V09
{
    /// <summary>
    /// v0.9 毒蛇女王 / 巫妖女王 引入的新机制集中处理。
    /// 钩子由 StatusRules / EffectActionExecutor / DamageRules / BattleEngine 在相应时机调用。
    /// </summary>
    public static class V09NewMechanicsRules
    {
        public const string SnakeGodResponseCardId = "v_snake_god_response";
        public const string DespairSoulCardId = "l_despair_soul";
        public const int SnakeGodResponseTokenCost = 0;

        // ----- 回合开始：缠绕 / 延迟伤害 / 永恒虚无真伤 / 祈求远古蛇神注入 token -----
        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || !combatant.IsAlive)
                    continue;

                // 缠绕：每回合开始受到 Stacks 伤害（施法者自身 Stacks=0 仅锁牌展示；施法者死亡则失效）
                for (var i = combatant.Statuses.Count - 1; i >= 0; i--)
                {
                    var constrict = combatant.Statuses[i];
                    if (constrict?.StatusId != StatusCatalog.Constrict)
                        continue;

                    if (!string.IsNullOrEmpty(constrict.SourceCombatantId))
                    {
                        var source = state.GetCombatant(constrict.SourceCombatantId);
                        if (source == null || !source.IsAlive)
                        {
                            combatant.Statuses.RemoveAt(i);
                            events.Add(new BattleEvent(BattleEventKind.StatusRemoved, StatusCatalog.Constrict)
                            {
                                CombatantId = combatant.Id,
                                TargetId = StatusCatalog.Constrict,
                                Amount = System.Math.Max(1, constrict.Stacks)
                            });
                            continue;
                        }
                    }

                    if (constrict.Stacks > 0
                        && (string.IsNullOrEmpty(constrict.SourceCombatantId)
                            || constrict.SourceCombatantId != combatant.Id))
                        ApplyTickDamage(state, combatant, constrict.Stacks, "缠绕", events);
                }

                // 延迟伤害：下回合开始受到 Stacks 伤害（持续在跳伤后由 ProcessTurnStartDurations 扣减）
                var delayed = StatusRules.FindStatus(combatant, StatusCatalog.DelayedDamage);
                if (delayed != null && delayed.Stacks > 0)
                {
                    ApplyTickDamage(
                        state,
                        combatant,
                        delayed.Stacks,
                        "延迟伤害",
                        events,
                        StatusCatalog.DelayedDamage,
                        delayed.SourceCombatantId);
                }

                // 永恒虚无：每回合受 25% 最大 HP 真伤
                if (StatusRules.HasStatus(combatant, StatusCatalog.EternalVoid))
                {
                    var trueDmg = System.Math.Max(1, combatant.MaxHp * 25 / 100);
                    ApplyTickDamage(state, combatant, trueDmg, "永恒虚无", events);
                    // 永恒虚化：若虚化已掉则补回（ethereal 为回合制，靠每回合刷新实现"永久"）
                    if (!StatusRules.HasStatus(combatant, StatusCatalog.Ethereal))
                        StatusRules.ApplyStatus(state, combatant, StatusCatalog.Ethereal, 1, 1, events);
                }
            }
        }

        /// <summary>
        /// 须在 ProcessTurnStartDurations 之后调用，避免当回合立刻扣减持续回合。
        /// 处理蓄能等「下回合开始获得状态」。
        /// </summary>
        public static void ProcessPendingStatusesNextTurn(BattleState state, List<BattleEvent> events)
        {
            if (state == null || state.PendingStatusesNextTurn.Count == 0)
                return;

            var pending = new List<PendingNextTurnStatus>(state.PendingStatusesNextTurn);
            state.PendingStatusesNextTurn.Clear();
            foreach (var entry in pending)
            {
                if (entry == null || string.IsNullOrEmpty(entry.StatusId) || entry.Stacks <= 0)
                    continue;

                var combatant = state.GetCombatant(entry.CombatantId);
                if (combatant == null || !combatant.IsAlive)
                    continue;

                StatusRules.ApplyStatus(
                    state, combatant, entry.StatusId, entry.Stacks, entry.Duration, events);
            }
        }

        /// <summary>
        /// 须在 ProcessTurnStartDurations 之后调用：
        /// 启动（蛇神降临）本回合到期后，与硬锁解除同一拍将【蛇神的回应】置入手牌。
        /// </summary>
        public static void ProcessSnakeGodResponseHand(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || !combatant.IsAlive || combatant.Team != TeamSide.Player)
                    continue;

                if (!StatusRules.HasStatus(combatant, StatusCatalog.PrayAncientSnakeGod))
                    continue;

                if (StatusRules.HasStatus(combatant, StatusCatalog.SnakeGodChanneling))
                    continue;

                AddSnakeGodResponseToHand(state, combatant, events);
            }
        }

        static void ApplyTickDamage(
            BattleState state,
            CombatantState combatant,
            int damage,
            string label,
            List<BattleEvent> events,
            string statusId = "",
            string sourceCombatantId = "")
        {
            if (combatant == null || damage <= 0)
                return;

            if (combatant.Team == TeamSide.Enemy)
                damage = ApplyPsionicBodyBonus(state, TeamSide.Player, damage);
            if (damage <= 0)
                return;

            combatant.Hp = System.Math.Max(0, combatant.Hp - damage);
            events.Add(new BattleEvent(BattleEventKind.StatusTickDamage, label)
            {
                CombatantId = combatant.Id,
                Amount = damage,
                TargetId = statusId ?? "",
                SourceCombatantId = sourceCombatantId ?? ""
            });

            if (!combatant.IsAlive
                && CombatMechanicsRules.TryPreventDeathWithReviveBlessing(state, combatant, events))
                return;

            if (!combatant.IsAlive)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, combatant.DisplayName)
                {
                    CombatantId = combatant.Id
                });
                CombatantDeathRules.OnCharacterDied(state, combatant, events);
            }
        }

        // ----- 缠绕 -----
        public static void ApplyConstrict(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int damage,
            int duration,
            List<BattleEvent> events,
            bool applyCasterLock = true)
        {
            if (target == null || !target.IsAlive)
                return;

            StatusRules.ApplyStatus(state, target, StatusCatalog.Constrict, damage, duration, events);
            TagConstrictSource(target, actor?.Id);

            if (applyCasterLock)
                ApplyConstrictCasterLock(state, actor, duration, events);
        }

        public static void ApplyConstrictCasterLock(
            BattleState state,
            CombatantState actor,
            int duration,
            List<BattleEvent> events)
        {
            if (actor == null || !actor.IsAlive)
                return;

            var lockTurns = System.Math.Max(1, duration);
            ApplyCasterConstrictStatus(state, actor, lockTurns, events);
            CardLockRules.ApplyConstrictLock(actor, lockTurns);
        }

        static void TagConstrictSource(CombatantState target, string sourceId)
        {
            if (target == null || string.IsNullOrEmpty(sourceId))
                return;

            for (var i = target.Statuses.Count - 1; i >= 0; i--)
            {
                var status = target.Statuses[i];
                if (status?.StatusId == StatusCatalog.Constrict && status.Stacks > 0)
                    status.SourceCombatantId = sourceId;
            }
        }

        /// <summary>施法者缠绕展示：不跳伤，仅表示缠绕期间无法出牌。</summary>
        static void ApplyCasterConstrictStatus(
            BattleState state,
            CombatantState actor,
            int duration,
            List<BattleEvent> events)
        {
            var def = StatusCatalog.Get(StatusCatalog.Constrict);
            if (def == null || actor == null)
                return;

            StatusInstance existing = null;
            foreach (var status in actor.Statuses)
            {
                // 自身来源的缠绕标记（与敌人身上的伤害缠绕区分）
                if (status?.StatusId == StatusCatalog.Constrict
                    && status.SourceCombatantId == actor.Id)
                {
                    existing = status;
                    break;
                }
            }

            if (existing == null)
            {
                existing = new StatusInstance
                {
                    StatusId = StatusCatalog.Constrict,
                    Stacks = 1,
                    RemainingTurns = 0,
                    SourceCombatantId = actor.Id
                };
                actor.Statuses.Add(existing);
            }

            existing.SourceCombatantId = actor.Id;
            existing.Stacks = System.Math.Max(1, existing.Stacks);
            if (existing.RemainingTurns >= 0)
            {
                var turns = System.Math.Max(1, duration);
                if (existing.RemainingTurns < turns)
                    existing.RemainingTurns = turns;
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "缠绕期间无法出牌")
            {
                CombatantId = actor.Id,
                Amount = existing.Stacks,
                TargetId = StatusCatalog.Constrict
            });
        }

        /// <summary>施法者死亡：清除其施加的全部缠绕，并解除其自身锁牌展示。</summary>
        public static void OnConstrictCasterDied(BattleState state, CombatantState caster, List<BattleEvent> events)
        {
            if (state == null || caster == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null)
                    continue;

                for (var i = combatant.Statuses.Count - 1; i >= 0; i--)
                {
                    var status = combatant.Statuses[i];
                    if (status?.StatusId != StatusCatalog.Constrict)
                        continue;

                    if (status.SourceCombatantId != caster.Id && combatant.Id != caster.Id)
                        continue;

                    var removed = System.Math.Max(1, status.Stacks);
                    combatant.Statuses.RemoveAt(i);
                    events.Add(new BattleEvent(BattleEventKind.StatusRemoved, StatusCatalog.Constrict)
                    {
                        CombatantId = combatant.Id,
                        TargetId = StatusCatalog.Constrict,
                        Amount = removed
                    });
                }
            }

            if (caster.ConstrictLockTurnsRemaining > 0)
                CardLockRules.ClearConstrictLock(caster);
        }

        /// <summary>缠绕目标死亡：若施法者已无存活缠绕目标，解除缠绕锁牌。</summary>
        public static void OnConstrictTargetDied(BattleState state, CombatantState dead, List<BattleEvent> events)
        {
            if (state == null || dead == null)
                return;

            var sourceIds = new HashSet<string>();
            foreach (var status in dead.Statuses)
            {
                if (status?.StatusId != StatusCatalog.Constrict)
                    continue;
                if (string.IsNullOrEmpty(status.SourceCombatantId))
                    continue;
                if (status.SourceCombatantId == dead.Id)
                    continue;

                sourceIds.Add(status.SourceCombatantId);
            }

            foreach (var sourceId in sourceIds)
                TryReleaseConstrictCasterIfNoLivingTargets(state, sourceId, events);
        }

        public static void TryReleaseConstrictCasterIfNoLivingTargets(
            BattleState state,
            string casterId,
            List<BattleEvent> events)
        {
            if (state == null || string.IsNullOrEmpty(casterId))
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || !combatant.IsAlive || combatant.Id == casterId)
                    continue;

                foreach (var status in combatant.Statuses)
                {
                    if (status?.StatusId == StatusCatalog.Constrict
                        && status.SourceCombatantId == casterId
                        && status.Stacks > 0)
                        return;
                }
            }

            var caster = state.GetCombatant(casterId);
            if (caster == null)
                return;

            // 清除施法者自身缠绕标记与缠绕锁（不影响祈求等硬锁）。
            for (var i = caster.Statuses.Count - 1; i >= 0; i--)
            {
                var status = caster.Statuses[i];
                if (status?.StatusId != StatusCatalog.Constrict)
                    continue;
                if (status.SourceCombatantId != casterId)
                    continue;

                caster.Statuses.RemoveAt(i);
                events?.Add(new BattleEvent(BattleEventKind.StatusRemoved, StatusCatalog.Constrict)
                {
                    CombatantId = caster.Id,
                    TargetId = StatusCatalog.Constrict,
                    Amount = System.Math.Max(1, status.Stacks)
                });
            }

            if (caster.ConstrictLockTurnsRemaining > 0)
            {
                CardLockRules.ClearConstrictLock(caster);
                events?.Add(new BattleEvent(BattleEventKind.StatusRemoved, "缠绕解除，可再次出牌")
                {
                    CombatantId = caster.Id,
                    Amount = 0
                });
            }
        }

        // ----- 中毒即时结算并清除（不同持续时间分桶：Σ 层数×持续，永久按 3）-----
        public static void SettlePoisonAndClear(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (target == null || !StatusRules.HasStatus(target, StatusCatalog.Poison))
                return;

            var damage = 0;
            foreach (var status in target.Statuses)
            {
                if (status?.StatusId != StatusCatalog.Poison || status.Stacks <= 0)
                    continue;

                var effectiveDuration = status.RemainingTurns < 0
                    ? 3
                    : System.Math.Max(1, status.RemainingTurns);
                damage += status.Stacks * effectiveDuration;
            }

            StatusRules.RemoveAllStatus(target, StatusCatalog.Poison, events);

            if (damage > 0)
                ApplyTickDamage(state, target, damage, "引爆毒囊", events);
        }

        // ----- 蜕皮：清除自身中毒，每层治疗 -----
        public static void RemovePoisonHealPerStack(
            BattleState state,
            CombatantState actor,
            int healPerStack,
            List<BattleEvent> events)
        {
            if (actor == null)
                return;

            var stacks = StatusRules.GetStatusStacks(actor, StatusCatalog.Poison);
            if (stacks <= 0)
                return;

            StatusRules.RemoveAllStatus(actor, StatusCatalog.Poison, events);
            var heal = stacks * System.Math.Max(0, healPerStack);
            if (heal > 0)
                DamageRules.ApplyHeal(state, actor, heal, events, actor);
        }

        // ----- 剧毒反哺：转移自身一半中毒给随机敌人 -----
        public static void TransferHalfPoisonToRandomEnemy(
            BattleState state,
            CombatantState actor,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (actor == null || rng == null)
                return;

            var total = StatusRules.GetStatusStacks(actor, StatusCatalog.Poison);
            if (total <= 1)
                return;

            var transfer = total / 2;
            if (transfer <= 0)
                return;

            StatusRules.RemoveStatus(actor, StatusCatalog.Poison, transfer, events);

            var enemy = TargetRules.PickRandomEnemies(state, actor.Team, 1, rng);
            if (enemy == null || enemy.Count == 0)
                return;

            StatusRules.ApplyStatus(state, enemy[0], StatusCatalog.Poison, transfer, -1, events);
        }

        // ----- 毒囊破裂被动：施毒时 +1 层 -----
        public static int AdjustPoisonStacksForVenomSac(CombatantState applier, string statusId, int stacks)
        {
            if (applier == null || statusId != StatusCatalog.Poison)
                return stacks;
            if (StatusRules.HasStatus(applier, StatusCatalog.VenomSacBurst))
                stacks += 1;
            return stacks;
        }

        // ----- 获得中毒时：不朽蛇蜕 +10%增伤5回合 -----
        public static void OnPoisonAppliedToSelf(
            BattleState state,
            CombatantState target,
            string statusId,
            List<BattleEvent> events)
        {
            if (state == null || target == null || events == null)
                return;
            if (statusId != StatusCatalog.Poison)
                return;
            if (StatusRules.HasStatus(target, StatusCatalog.ImmortalShed))
            {
                var shed = StatusRules.FindStatus(target, StatusCatalog.ImmortalShed);
                var percent = shed != null && shed.Stacks > 0 ? shed.Stacks : 10;
                StatusRules.ApplyStatus(state, target, StatusCatalog.AttackUpPercent, percent, 5, events);
            }
        }

        // ----- 获得虚化时：绝望之魔回收 + 巫妖天赋 -----
        public static void OnEtherealGained(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (state == null || target == null || events == null)
                return;

            // 绝望之魂：巫妖女王获虚化时，从弃牌堆直接加入手牌；
            // 若处于战斗结算中，则改为下回合开始时加入手牌。
            if (StatusRules.HasStatus(target, StatusCatalog.DespairSoulRecall)
                && target.Team == TeamSide.Player)
            {
                if (IsNonCombatPhase(state))
                    TryRecallDespairSoulFromDiscard(state, target, events);
                else
                    state.PendingDespairSoulRecallNextTurn = true;
            }

            // 巫妖女王天赋 s1_lv1：获得虚化时回 3HP
            TalentBattleRules.OnEtherealGained(state, target, events);
        }

        /// <summary>下回合开始：处理战斗中获虚化而延迟的绝望之魂回收。</summary>
        public static void ProcessPendingDespairSoulRecall(BattleState state, List<BattleEvent> events)
        {
            if (state == null || !state.PendingDespairSoulRecallNextTurn)
                return;

            state.PendingDespairSoulRecallNextTurn = false;
            CombatantState owner = null;
            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || !combatant.IsAlive || combatant.Team != TeamSide.Player)
                    continue;
                if (!StatusRules.HasStatus(combatant, StatusCatalog.DespairSoulRecall))
                    continue;
                owner = combatant;
                break;
            }

            if (owner != null)
                TryRecallDespairSoulFromDiscard(state, owner, events);
        }

        static void TryRecallDespairSoulFromDiscard(BattleState state, CombatantState owner, List<BattleEvent> events)
        {
            var discard = state.GetDiscardPile(owner.Team);
            for (var i = 0; i < discard.Count; i++)
            {
                var card = discard[i];
                if (card == null || card.DefinitionId != DespairSoulCardId)
                    continue;

                var hand = state.GetHand(owner.Team);
                if (hand.Count >= state.Config.HandLimit)
                    return;

                discard.RemoveAt(i);
                card.OwnerCombatantId = owner.Id;
                card.IsBonusHandCard = false;
                hand.Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDrawn, "绝望之魂回收")
                {
                    CombatantId = owner.Id,
                    CardInstanceId = card.InstanceId
                });
                return;
            }
        }

        // ----- 受伤后：两界行者下次受击获虚化 -----
        public static void AfterDamageResolveEtherealOnNextHit(
            BattleState state,
            CombatantState recipient,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (state == null || recipient == null || hpDamage <= 0)
                return;
            if (!StatusRules.HasStatus(recipient, StatusCatalog.EtherealOnNextHit))
                return;

            StatusRules.RemoveStatus(recipient, StatusCatalog.EtherealOnNextHit, 1, events);
            StatusRules.ApplyStatus(state, recipient, StatusCatalog.Ethereal, 1, 1, events);
        }

        // ----- 灵界降临：本回合手牌 0 费 -----
        public static int AdjustPlayCostForHandCostZero(BattleState state, CombatantState owner, int baseCost)
        {
            if (state != null)
            {
                foreach (var ally in state.GetTeam(TeamSide.Player))
                {
                    if (ally.IsAlive && StatusRules.HasStatus(ally, StatusCatalog.HandCostZero))
                        return 0;
                }
            }

            if (owner != null && StatusRules.HasStatus(owner, StatusCatalog.HandCostZero))
                return 0;
            return baseCost;
        }

        // ----- 召唤混乱之灵：打乱手牌费用 -----
        public static void ShuffleHandCosts(BattleState state, CombatantState actor, BattleRng rng)
        {
            if (state == null || actor == null || rng == null)
                return;

            var hand = state.GetHand(actor.Team);
            if (hand.Count <= 1)
                return;

            // 收集原始费用，随机重排后写回（用 CostReduction 字段不可行，直接改 Cost）
            var costs = new List<int>();
            foreach (var card in hand)
                costs.Add(card.Cost);

            for (var i = costs.Count - 1; i > 0; i--)
            {
                var j = rng.NextIndex(i + 1);
                (costs[i], costs[j]) = (costs[j], costs[i]);
            }

            for (var i = 0; i < hand.Count; i++)
                hand[i].Cost = costs[i];
        }

        // ----- 蛇神的回应 token -----
        public static void AddSnakeGodResponseToHand(
            BattleState state,
            CombatantState owner,
            List<BattleEvent> events)
        {
            if (state == null || owner == null)
                return;

            var hand = state.GetHand(owner.Team);
            if (hand.Count >= state.Config.HandLimit)
                return;

            var id = state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = SnakeGodResponseCardId,
                DisplayName = "蛇神的回应",
                OwnerCharacterId = owner.CharacterDefinitionId,
                OwnerCombatantId = owner.Id,
                Cost = SnakeGodResponseTokenCost,
                BaseCost = SnakeGodResponseTokenCost,
                CardType = CardType.Status,
                IsUsable = true,
                IsBonusHandCard = true
            };
            card.Keywords.Add("exhaust");
            card.Keywords.Add("token");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.RandomSnakeGodEffect,
                Target = EffectTarget.AllEnemies,
                Value = 25,
                Stacks = 10,
                Duration = -1,
                AlternateValue = 75,
                Reach = TargetReach.Any
            });

            // token 不进玩家卡池，需手动注册稀有度，否则手牌会落到白色 Common 框。
            CardRarityTable.Register(SnakeGodResponseCardId, CardRarity.Legendary);

            state.CardsById[id] = card;
            hand.Add(card);
            events.Add(new BattleEvent(BattleEventKind.CardDrawn, "蛇神的回应置入手牌")
            {
                CombatantId = owner.Id,
                CardInstanceId = id
            });
        }

        /// <summary>按 DefinitionId 直接创建一个 token 卡实例并置入手牌（供 AddTokenCardToHand 动作使用）。</summary>
        public static void AddTokenCardToHand(
            BattleState state,
            CombatantState owner,
            string cardId,
            List<BattleEvent> events)
        {
            if (state == null || owner == null || string.IsNullOrEmpty(cardId))
                return;

            if (cardId == SnakeGodResponseCardId)
            {
                AddSnakeGodResponseToHand(state, owner, events);
                return;
            }

            // 通用回退：从弃牌堆/抽牌堆找一张同名卡加入手牌
            var hand = state.GetHand(owner.Team);
            if (hand.Count >= state.Config.HandLimit)
                return;

            var piles = new List<List<CardInstanceState>>
            {
                state.GetDiscardPile(owner.Team),
                state.GetDrawPile(owner.Team)
            };
            foreach (var pile in piles)
            {
                for (var i = 0; i < pile.Count; i++)
                {
                    var card = pile[i];
                    if (card != null && card.DefinitionId == cardId)
                    {
                        pile.RemoveAt(i);
                        card.OwnerCombatantId = owner.Id;
                        card.IsBonusHandCard = true;
                        hand.Add(card);
                        events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"{card.DisplayName} 置入手牌")
                        {
                            CombatantId = owner.Id,
                            CardInstanceId = card.InstanceId
                        });
                        return;
                    }
                }
            }
        }

        // ----- 灵魂挽歌：本场远征虚化次数加伤 -----
        public static int GetEtherealEntryCount(BattleState state)
        {
            return state?.Config?.RunModifiers?.EtherealEntryCount ?? 0;
        }

        public static void IncrementEtherealEntryCount(BattleState state)
        {
            if (state?.Config?.RunModifiers == null)
                return;
            state.Config.RunModifiers.EtherealEntryCount += 1;
        }

        public static bool IsNonCombatPhase(BattleState state) =>
            state != null
            && state.Phase != TurnPhase.SpeedResolve
            && state.Phase != TurnPhase.BattleEnd;

        public static bool TeamHasPsionicBody(BattleState state)
        {
            if (state == null)
                return false;

            foreach (var ally in state.GetTeam(TeamSide.Player))
            {
                if (ally != null && ally.IsAlive && StatusRules.HasStatus(ally, StatusCatalog.PsionicBody))
                    return true;
            }

            return false;
        }

        public static int AdjustRealmBurstDamage(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            int power,
            List<BattleEvent> events)
        {
            if (card?.DefinitionId != "l_realm_burst" || actor == null || !StatusRules.HasStatus(actor, StatusCatalog.Ethereal))
                return power;

            StatusRules.RemoveStatus(actor, StatusCatalog.Ethereal, 1, events);
            return 30;
        }

        /// <summary>灵界专注：非战斗时段我方造成的伤害 +10%。</summary>
        public static float GetLichSpiritFocusDamageMultiplier(BattleState state, TeamSide sourceTeam)
        {
            if (state == null || sourceTeam != TeamSide.Player || !IsNonCombatPhase(state))
                return 1f;
            return TalentBattleRules.HasTalent(state, "talent_lich_s2_lv2") ? 1.1f : 1f;
        }

        /// <summary>灵能体 / 灵界专注：非战斗时段我方造成的伤害增幅（可叠加）。</summary>
        public static float GetNonCombatOutgoingDamageMultiplier(BattleState state, TeamSide sourceTeam)
        {
            if (state == null || sourceTeam != TeamSide.Player || !IsNonCombatPhase(state))
                return 1f;

            var mul = 1f;
            if (TeamHasPsionicBody(state))
                mul *= 1.2f;
            mul *= GetLichSpiritFocusDamageMultiplier(state, sourceTeam);
            return mul;
        }

        /// <summary>灵能体：非战斗时段我方造成的伤害 +20%；灵界专注：+10%（可叠加）。</summary>
        public static int ApplyPsionicBodyBonus(BattleState state, TeamSide sourceTeam, int damage)
        {
            if (damage <= 0 || sourceTeam != TeamSide.Player)
                return damage;

            var mul = GetNonCombatOutgoingDamageMultiplier(state, sourceTeam);
            if (mul <= 1f)
                return damage;

            return System.Math.Max(1, (int)System.Math.Round(damage * mul));
        }
    }
}
