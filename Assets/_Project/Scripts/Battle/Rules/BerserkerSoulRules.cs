using System;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    /// <summary>A spectral card is a second planned play, not a new owned/deck card.</summary>
    public static class BerserkerSoulRules
    {
        public const string CardId = "b_berserker_soul";
        public const string CharacterId = "char_berserker";
        public const string Description = "【消耗】本场战斗中，攻击牌获得【继承】，自身防御牌禁用。每回合首张非消耗、非快速启动攻击可【再战】一次。";

        public static bool IsActive(BattleState state, CombatantState owner) =>
            state != null && owner != null && owner.IsAlive && state.BerserkerSoulOwners.Contains(owner.Id);

        public static bool IsEligible(CardInstanceState card) => card != null
            && card.SpectralRepeatSourceId == 0 && card.CardType == CardType.Attack
            && !card.Keywords.Contains("exhaust") && !card.Keywords.Contains("quick_start")
            && !card.IsBonusHandCard;

        public static void Activate(BattleState state, CombatantState owner, List<BattleEvent> events)
        {
            if (owner == null || !owner.IsAlive || owner.CharacterDefinitionId != CharacterId)
                return;
            state.BerserkerSoulOwners.Add(owner.Id);
            if (!owner.Traits.Contains("berserker_soul")) owner.Traits.Add("berserker_soul");
            foreach (var card in state.CardsById.Values)
                if (PositionRules.GetOwnerCombatantId(state, card) == owner.Id
                    && card.CardType == CardType.Attack && card.SpectralRepeatSourceId == 0
                    && !card.Keywords.Contains("inherit"))
                    card.Keywords.Add("inherit");
            events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, "狂战之魂：攻击继承，首张攻击可再战，自身防御禁用") { CombatantId = owner.Id });
        }

        public static bool BlocksDefense(CombatantState owner, CardInstanceState card) =>
            owner != null && owner.Traits.Contains("berserker_soul") && card?.CardType == CardType.Defense;

        public static void ApplyInherit(BattleState state, CardInstanceState card)
        {
            if (card == null || card.SpectralRepeatSourceId > 0 || card.CardType != CardType.Attack) return;
            var owner = state.GetCombatant(PositionRules.GetOwnerCombatantId(state, card));
            if (IsActive(state, owner) && !card.Keywords.Contains("inherit")) card.Keywords.Add("inherit");
        }

        public static CardInstanceState CreateSpectral(BattleState state, CardInstanceState source)
        {
            while (state.CardsById.ContainsKey(state.NextCardInstanceId)) state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = state.NextCardInstanceId++, SpectralRepeatSourceId = source.InstanceId,
                DefinitionId = source.DefinitionId, DisplayName = source.DisplayName,
                OwnerCharacterId = source.OwnerCharacterId, OwnerCombatantId = source.OwnerCombatantId,
                // Temporary hand discounts end after the original's first use.
                Cost = Math.Max(0, source.CostBeforeHandDiscount ?? source.Cost),
                BaseCost = source.BaseCost, CardType = source.CardType, UpgradeLevel = source.UpgradeLevel
            };
            foreach (var keyword in source.Keywords)
                if (keyword != "inherit") card.Keywords.Add(keyword);
            card.Keywords.Add("berserker_repeat");
            foreach (var action in source.Actions) card.Actions.Add(EffectActionSpec.Clone(action));
            state.CardsById.Add(card.InstanceId, card);
            // Kept outside PlayerHand: does not occupy a real hand slot or become a random-discard target.
            return card;
        }

        public static void ClearTurn(BattleState state)
        {
            state.BerserkerResolvedAttackIdsThisTurn.Clear();
            var ids = new List<int>();
            foreach (var pair in state.CardsById)
                if (pair.Value.SpectralRepeatSourceId > 0) ids.Add(pair.Key);
            foreach (var id in ids) state.CardsById.Remove(id);
        }

        public static bool CanResolveRepeat(BattleState state, CardInstanceState card)
        {
            if (card.SpectralRepeatSourceId == 0) return true;
            var source = state.GetCard(card.SpectralRepeatSourceId);
            return source?.IsUsable == true && !source.Keywords.Contains("exhaust")
                && !state.PlayerExhaustPile.Contains(source)
                && state.BerserkerResolvedAttackIdsThisTurn.Contains(source.InstanceId);
        }

        public static void RefreshSpectral(BattleState state, CardInstanceState card)
        {
            if (card.SpectralRepeatSourceId == 0) return;
            var source = state.GetCard(card.SpectralRepeatSourceId);
            if (source == null) return;
            card.UpgradeLevel = source.UpgradeLevel; card.BaseCost = source.BaseCost;
            card.Cost = Math.Max(0, source.CostBeforeHandDiscount ?? source.Cost);
            card.Actions.Clear(); foreach (var a in source.Actions) card.Actions.Add(EffectActionSpec.Clone(a));
        }

        public static void RecordResolved(BattleState state, CardInstanceState card)
        {
            if (card.SpectralRepeatSourceId == 0 && card.CardType == CardType.Attack)
                state.BerserkerResolvedAttackIdsThisTurn.Add(card.InstanceId);
        }
    }
}
