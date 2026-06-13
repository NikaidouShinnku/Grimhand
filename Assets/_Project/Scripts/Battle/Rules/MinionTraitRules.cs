using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class MinionTraitRules
    {
        static readonly HashSet<string> ChainWraithSharedDebuffs = new()
        {
            StatusCatalog.Poison,
            StatusCatalog.Slow,
            StatusCatalog.Burn,
            StatusCatalog.AttackDown,
            StatusCatalog.DefenseDownPercent,
            StatusCatalog.NecroticPoison
        };

        public static bool HasTrait(CombatantState combatant, string traitId)
        {
            if (combatant == null || string.IsNullOrEmpty(traitId))
                return false;

            return combatant.Traits.Contains(traitId);
        }

        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                if (HasTrait(combatant, MinionTraitCatalog.SlimeRegen) && !combatant.TookDamageLastTurn)
                    DamageRules.ApplyHeal(state, combatant, MinionTraitCatalog.SlimeRegenAmount, events);

                combatant.TookDamageLastTurn = false;
                combatant.FirstHitDodgePending = HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge);
                combatant.InvulnerableRestOfTurn = false;
                combatant.RespondArmedThisTurn = false;
                combatant.DodgeChanceBonus = 0f;
                combatant.CardsResolvedThisTurn = 0;
                combatant.GargoyleStanceAttackBonus = 0;
                combatant.GargoyleStanceDefenseBonus = 0;

                if (combatant.CarryOverBlock > 0)
                {
                    combatant.Block += combatant.CarryOverBlock;
                    combatant.CarryOverBlock = 0;
                }
            }

            state.EnemyAttackCardsPlayedThisTurn = 0;
        }

        public static void PrepareTurnEndArmorRetain(BattleState state)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive || !HasTrait(combatant, MinionTraitCatalog.StoneGolemArmorRetain))
                    continue;

                if (combatant.Block > 0)
                    combatant.CarryOverBlock = combatant.Block / 2;
            }
        }

        public static void OnCharacterDied(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            if (state == null || combatant == null)
                return;

            if (combatant.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId)
                return;

            foreach (var ally in state.Combatants)
            {
                if (!ally.IsAlive || ally.Team != TeamSide.Enemy)
                    continue;

                if (ally.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId)
                    continue;

                ally.RatPackAttackBonusPercent += MinionTraitCatalog.RatPackAttackBonusPercentPerDeath;
                RelicBattleRules.RefreshDerivedStats(state, ally, state.Config?.RunModifiers);
                events?.Add(new BattleEvent(BattleEventKind.StatusApplied,
                    $"{ally.DisplayName} 鼠群狂怒 +{MinionTraitCatalog.RatPackAttackBonusPercentPerDeath}% 攻击")
                {
                    CombatantId = ally.Id
                });
            }
        }

        public static void ShareChainWraithDebuff(
            BattleState state,
            CombatantState target,
            string statusId,
            int stacks,
            int durationOverride,
            List<BattleEvent> events)
        {
            if (state == null || target == null || stacks <= 0)
                return;

            if (!HasTrait(target, MinionTraitCatalog.ChainWraithDebuffShare)
                || !ChainWraithSharedDebuffs.Contains(statusId))
                return;

            foreach (var enemy in state.Combatants)
            {
                if (!enemy.IsAlive || enemy.Team != TeamSide.Enemy || enemy.Id == target.Id)
                    continue;

                StatusRules.ApplyStatusInternal(state, enemy, statusId, stacks, durationOverride, events, mirrorChainWraith: false);
            }
        }

        public static int ApplySpiderPoisonVulnerability(BattleState state, CombatantState recipient, int hpDamage)
        {
            if (state == null || recipient == null || hpDamage <= 0 || recipient.Team != TeamSide.Enemy)
                return hpDamage;

            if (!HasAliveSpiderLady(state))
                return hpDamage;

            var poisonStacks = StatusRules.GetStatusStacks(recipient, StatusCatalog.Poison);
            if (poisonStacks < 5)
                return hpDamage;

            var bonusPercent = poisonStacks / 5 * MinionTraitCatalog.SpiderPoisonVulnPercentPerFiveStacks;
            return System.Math.Max(1,
                (int)System.Math.Round(hpDamage * (100 + bonusPercent) / 100f));
        }

        static bool HasAliveSpiderLady(BattleState state)
        {
            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive || combatant.Team != TeamSide.Enemy)
                    continue;

                if (HasTrait(combatant, MinionTraitCatalog.SpiderLadyPoisonVulnerability))
                    return true;
            }

            return false;
        }

        public static void OnDamageTaken(
            BattleState state,
            CombatantState recipient,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (recipient == null || hpDamage <= 0)
                return;

            recipient.TookDamageLastTurn = true;

            if (HasTrait(recipient, MinionTraitCatalog.OgreBloodRage))
            {
                recipient.BloodRageStacks = System.Math.Min(
                    MinionTraitCatalog.OgreBloodRageMaxStacks,
                    recipient.BloodRageStacks + 1);
            }

            TryTriggerWraithEliteEnrage(state, recipient, events);
            RefreshLowHpSpeed(state, recipient);
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || card == null || !actor.IsAlive)
                return;

            if (actor.Team == TeamSide.Enemy && card.CardType == CardType.Attack)
                state.EnemyAttackCardsPlayedThisTurn++;

            if (HasTrait(actor, MinionTraitCatalog.GargoyleFirstCardStance) && actor.CardsResolvedThisTurn == 0)
            {
                if (card.CardType == CardType.Attack)
                    actor.GargoyleStanceAttackBonus = MinionTraitCatalog.GargoyleStanceBonus;
                else if (card.CardType is CardType.Defense or CardType.Status)
                    actor.GargoyleStanceDefenseBonus = MinionTraitCatalog.GargoyleStanceBonus;

                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
            }

            actor.CardsResolvedThisTurn++;
            actor.CardsResolvedCount++;
            if (actor.CardsResolvedCount % MinionTraitCatalog.CardsPerStatBonus != 0)
                return;

            if (HasTrait(actor, MinionTraitCatalog.SkeletonCardDef))
            {
                actor.BaseDefense += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 防御")
                {
                    CombatantId = actor.Id
                });
            }

            if (HasTrait(actor, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                actor.BaseDefense += 1;
                actor.BaseAttack += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 攻击 +1 防御")
                {
                    CombatantId = actor.Id
                });
            }
        }

        public static int ApplyBloodRageOutgoingBonus(CombatantState actor, CardType cardType, int power)
        {
            if (actor == null || cardType != CardType.Attack || power <= 0)
                return power;

            if (!HasTrait(actor, MinionTraitCatalog.OgreBloodRage) || actor.BloodRageStacks <= 0)
                return power;

            var bonusPercent = actor.BloodRageStacks * MinionTraitCatalog.OgreBloodRageDamagePercentPerStack;
            var boosted = (int)System.Math.Round(power * (100 + bonusPercent) / 100f);
            return System.Math.Max(1, boosted);
        }

        public static void ConsumeBloodRageAfterAttack(CombatantState actor, CardType cardType)
        {
            if (actor == null || cardType != CardType.Attack)
                return;

            if (!HasTrait(actor, MinionTraitCatalog.OgreBloodRage))
                return;

            actor.BloodRageStacks = 0;
        }

        public static bool TryFirstHitDodge(
            BattleState state,
            CombatantState target,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (target == null || rng == null || !target.FirstHitDodgePending)
                return false;

            if (!HasTrait(target, MinionTraitCatalog.BatFirstHitDodge))
                return false;

            target.FirstHitDodgePending = false;
            var roll = rng.NextUInt() % 1000u / 1000f;
            if (roll >= MinionTraitCatalog.BatFirstHitDodgeChance)
                return false;

            events?.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{target.DisplayName} 闪避")
            {
                TargetId = target.Id,
                Amount = 0
            });
            return true;
        }

        public static void RefreshLowHpSpeed(BattleState state, CombatantState combatant)
        {
            if (combatant == null || !combatant.IsAlive || combatant.MaxHp <= 0)
                return;

            var belowHalf = combatant.Hp * 100 / combatant.MaxHp < 50;
            var wantsBonus = belowHalf
                && (HasTrait(combatant, MinionTraitCatalog.WraithLowHpSpeed)
                    || HasTrait(combatant, MinionTraitCatalog.WraithEliteLowHpEthereal));

            var desiredBonus = wantsBonus ? MinionTraitCatalog.WraithLowHpSpeedBonus : 0;
            if (desiredBonus == combatant.LowHpSpeedBonusApplied)
                return;

            combatant.Speed += desiredBonus - combatant.LowHpSpeedBonusApplied;
            combatant.LowHpSpeedBonusApplied = desiredBonus;
        }

        static void TryTriggerWraithEliteEnrage(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || !combatant.IsAlive)
                return;

            if (!HasTrait(combatant, MinionTraitCatalog.WraithEliteLowHpEthereal)
                || combatant.WraithEliteEnrageTriggered
                || combatant.MaxHp <= 0)
                return;

            if (combatant.Hp * 100 / combatant.MaxHp >= 50)
                return;

            combatant.WraithEliteEnrageTriggered = true;
            StatusRules.ApplyStatus(state, combatant, StatusCatalog.Ethereal, 1, 1, events);
            RefreshLowHpSpeed(state, combatant);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{combatant.DisplayName} 虚化")
            {
                CombatantId = combatant.Id
            });
        }
    }
}
