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

                // 缠绕：每回合开始受到 Stacks 伤害
                var constrict = StatusRules.FindStatus(combatant, StatusCatalog.Constrict);
                if (constrict != null && constrict.Stacks > 0)
                    ApplyTickDamage(state, combatant, constrict.Stacks, "缠绕", events);

                // 延迟伤害：下回合开始受到 Stacks 伤害（Duration=1，本回合末到期，故此处结算）
                var delayed = StatusRules.FindStatus(combatant, StatusCatalog.DelayedDamage);
                if (delayed != null && delayed.Stacks > 0)
                    ApplyTickDamage(state, combatant, delayed.Stacks, "延迟伤害", events);

                // 永恒虚无：每回合受 25% 最大 HP 真伤
                if (StatusRules.HasStatus(combatant, StatusCatalog.EternalVoid))
                {
                    var trueDmg = System.Math.Max(1, combatant.MaxHp * 25 / 100);
                    ApplyTickDamage(state, combatant, trueDmg, "永恒虚无", events);
                    // 永恒虚化：若虚化已掉则补回（ethereal 为回合制，靠每回合刷新实现"永久"）
                    if (!StatusRules.HasStatus(combatant, StatusCatalog.Ethereal))
                        StatusRules.ApplyStatus(state, combatant, StatusCatalog.Ethereal, 1, 1, events);
                }

                // 祈求远古蛇神：每回合开始将【蛇神的回应】置入玩家手牌
                if (StatusRules.HasStatus(combatant, StatusCatalog.PrayAncientSnakeGod)
                    && combatant.Team == TeamSide.Player)
                {
                    AddSnakeGodResponseToHand(state, combatant, events);
                }
            }
        }

        static void ApplyTickDamage(BattleState state, CombatantState combatant, int damage, string label, List<BattleEvent> events)
        {
            if (combatant == null || damage <= 0)
                return;

            combatant.Hp = System.Math.Max(0, combatant.Hp - damage);
            events.Add(new BattleEvent(BattleEventKind.StatusTickDamage, label)
            {
                CombatantId = combatant.Id,
                Amount = damage
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
            List<BattleEvent> events)
        {
            if (target == null || !target.IsAlive)
                return;

            StatusRules.ApplyStatus(state, target, StatusCatalog.Constrict, damage, duration, events);

            // 施法者在此期间无法出牌（与持续时间一致）
            if (actor != null && actor.IsAlive)
            {
                var lockTurns = System.Math.Max(1, duration);
                CardLockRules.ApplyLock(actor, lockTurns);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, "缠绕施法者锁出牌")
                {
                    CombatantId = actor.Id,
                    Amount = lockTurns
                });
            }
        }

        // ----- 中毒即时结算并清除 -----
        public static void SettlePoisonAndClear(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (target == null)
                return;

            var poison = StatusRules.FindStatus(target, StatusCatalog.Poison);
            if (poison == null || poison.Stacks <= 0)
                return;

            // 永久视为 3 回合；否则按剩余回合
            var effectiveDuration = poison.RemainingTurns < 0 ? 3 : System.Math.Max(1, poison.RemainingTurns);
            var damage = poison.Stacks * effectiveDuration;

            // 清除中毒
            StatusRules.RemoveStatus(target, StatusCatalog.Poison, poison.Stacks, events);

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

            var poison = StatusRules.FindStatus(actor, StatusCatalog.Poison);
            if (poison == null || poison.Stacks <= 0)
                return;

            var stacks = poison.Stacks;
            StatusRules.RemoveStatus(actor, StatusCatalog.Poison, stacks, events);
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

            var poison = StatusRules.FindStatus(actor, StatusCatalog.Poison);
            if (poison == null || poison.Stacks <= 1)
                return;

            var transfer = poison.Stacks / 2;
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
                StatusRules.ApplyStatus(state, target, StatusCatalog.AttackUpPercent, 10, 5, events);
        }

        // ----- 获得虚化时：绝望之魔回收 + 巫妖天赋 -----
        public static void OnEtherealGained(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (state == null || target == null || events == null)
                return;

            // 绝望之魂：巫妖女王获虚化时，从弃牌堆直接加入手牌
            if (StatusRules.HasStatus(target, StatusCatalog.DespairSoulRecall)
                && target.Team == TeamSide.Player)
            {
                TryRecallDespairSoulFromDiscard(state, target, events);
            }

            // 巫妖女王天赋 s1_lv1：获得虚化时回 3HP
            TalentBattleRules.OnEtherealGained(state, target, events);
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
                card.IsBonusHandCard = true;
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
                AlternateValue = 75
            });

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
    }
}
