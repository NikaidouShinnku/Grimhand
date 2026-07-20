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

                // 先归档「上回合受伤」供本回合卡牌（无畏冲锋等）使用，再给史莱姆再生判定。
                combatant.TookDamagePreviousTurn = combatant.TookDamageLastTurn;

                if (HasTrait(combatant, MinionTraitCatalog.SlimeRegen) && !combatant.TookDamageLastTurn)
                    DamageRules.ApplyHeal(state, combatant, MinionTraitCatalog.SlimeRegenAmount, events);

                combatant.TookDamageLastTurn = false;
                combatant.FirstHitDodgePending = HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge);
                if (combatant.InvulnerableRestOfTurn
                    || StatusRules.HasStatus(combatant, StatusCatalog.Invulnerable))
                {
                    combatant.InvulnerableRestOfTurn = false;
                    StatusRules.RemoveAllStatus(combatant, StatusCatalog.Invulnerable, events);
                }
                else
                    combatant.InvulnerableRestOfTurn = false;
                combatant.RespondArmedThisTurn = false;
                combatant.DodgeChanceBonus = 0f;
                combatant.CardsResolvedThisTurn = 0;

                var priorFirstCardType = combatant.FirstCardTypeThisTurn;
                combatant.FirstCardTypeThisTurn = null;
                combatant.GargoyleStanceAttackBonus = 0;
                combatant.GargoyleStanceDefenseBonus = 0;

                // 石像鬼：用上回合首牌类型挂本回合增益（duration=2，抵消随后回合初 tick）
                ApplyGargoyleTraitFromPriorTurn(state, combatant, priorFirstCardType, events);

                if (combatant.CarryOverBlock > 0)
                {
                    combatant.Block += combatant.CarryOverBlock;
                    combatant.CarryOverBlock = 0;
                }
            }

            state.EnemyAttackCardsPlayedThisTurn = 0;
        }

        static void ApplyGargoyleTraitFromPriorTurn(
            BattleState state,
            CombatantState combatant,
            CardType? priorFirstCardType,
            List<BattleEvent> events)
        {
            if (!HasTrait(combatant, MinionTraitCatalog.GargoyleFirstCardStance)
                || !priorFirstCardType.HasValue)
                return;

            // 回合初 ProcessTurnStartDurations 会立刻 -1，故挂 2 回合保本回合内有效
            const int applyDuration = 2;
            if (priorFirstCardType.Value == CardType.Attack)
            {
                StatusRules.ApplyStatus(
                    state, combatant, StatusCatalog.AttackUpPercent,
                    MinionTraitCatalog.GargoyleTraitPercentBonus, applyDuration, events);
            }
            else if (priorFirstCardType.Value is CardType.Defense or CardType.Status)
            {
                StatusRules.ApplyStatus(
                    state, combatant, StatusCatalog.DefenseUpPercent,
                    MinionTraitCatalog.GargoyleTraitPercentBonus, applyDuration, events);
            }
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

            if (StatusRules.HasStatus(combatant, StatusCatalog.RatSwarmCall))
                SummonRules.SpawnRatSwarmClone(state, combatant, events);

            if (combatant.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId)
                return;

            foreach (var ally in state.Combatants)
            {
                if (!ally.IsAlive || ally.Team != combatant.Team || ally.Id == combatant.Id)
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

            // 自身 debuff 同步给敌对阵营全体（对玩家来说就是全体玩家角色）
            var mirrorTeam = target.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            foreach (var unit in state.GetTeam(mirrorTeam))
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                StatusRules.ApplyStatusInternal(
                    state, unit, statusId, stacks, durationOverride, events, mirrorChainWraith: false);
            }
        }

        /// <summary>锁链怨灵身上的同步 debuff 消失时，敌对阵营对应状态一并清除。</summary>
        public static void ClearSharedChainWraithDebuff(
            BattleState state,
            CombatantState source,
            string statusId,
            List<BattleEvent> events)
        {
            if (state == null || source == null || string.IsNullOrEmpty(statusId))
                return;

            if (!HasTrait(source, MinionTraitCatalog.ChainWraithDebuffShare)
                || !ChainWraithSharedDebuffs.Contains(statusId))
                return;

            var mirrorTeam = source.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            foreach (var unit in state.GetTeam(mirrorTeam))
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                StatusRules.RemoveAllStatus(unit, statusId, events);
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

            // 沉睡之石：看本回合第一张牌类型 → 攻击得增伤 / 防御或状态得强固（各 25%，3 回合）。
            if (card.DefinitionId == "m_gargoyle_sleep_stone")
            {
                var firstType = actor.CardsResolvedThisTurn == 0
                    ? card.CardType
                    : actor.FirstCardTypeThisTurn ?? card.CardType;

                if (firstType == CardType.Attack)
                {
                    StatusRules.ApplyStatus(
                        state, actor, StatusCatalog.AttackUpPercent, 25, 3, events);
                }
                else if (firstType is CardType.Defense or CardType.Status)
                {
                    StatusRules.ApplyStatus(
                        state, actor, StatusCatalog.DefenseUpPercent, 25, 3, events);
                }
            }

            if (actor.CardsResolvedThisTurn == 0)
                actor.FirstCardTypeThisTurn = card.CardType;

            actor.CardsResolvedThisTurn++;
            actor.CardsResolvedCount++;

            if (HasTrait(actor, MinionTraitCatalog.MermaidZeroCostAttack) && card.Cost == 0)
            {
                actor.MermaidZeroCostAttackBonusPercent = System.Math.Min(
                    100, actor.MermaidZeroCostAttackBonusPercent + 5);
            }

            if (actor.CardsResolvedCount % MinionTraitCatalog.CardsPerStatBonus != 0)
                return;

            if (HasTrait(actor, MinionTraitCatalog.SkeletonCardDef))
            {
                actor.PersistentBlockGainFlatBonus += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 护甲获取")
                {
                    CombatantId = actor.Id
                });
            }

            if (HasTrait(actor, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                actor.PersistentBlockGainFlatBonus += 1;
                actor.PersistentOutgoingDamageFlatBonus += 1;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} +1 增伤 +1 护甲获取")
                {
                    CombatantId = actor.Id
                });
            }
        }

        public static int ApplyMinionOutgoingAttackBonus(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            CardType cardType,
            int power)
        {
            if (actor == null || cardType != CardType.Attack || power <= 0)
                return power;

            power = ApplyBloodRageOutgoingBonus(actor, cardType, power);
            power = ApplySeahorseSpeedAttackBonus(state, actor, target, power);
            power = ApplyPhantomCaptainFrenzyBonus(state, actor, power);

            if (HasTrait(actor, MinionTraitCatalog.MermaidZeroCostAttack)
                && actor.MermaidZeroCostAttackBonusPercent > 0)
            {
                power = System.Math.Max(1,
                    (int)System.Math.Round(power * (100 + actor.MermaidZeroCostAttackBonusPercent) / 100f));
            }

            return power;
        }

        public static void OnDamageDealt(
            BattleState state,
            CombatantState actor,
            CombatantState recipient,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || recipient == null || hpDamage <= 0)
                return;

            if (recipient.Team != TeamSide.Player || !HasTrait(actor, MinionTraitCatalog.AbyssCreaturePoisonOnDamage))
                return;

            StatusRules.ApplyStatus(
                state,
                recipient,
                StatusCatalog.Poison,
                MinionTraitCatalog.AbyssCreaturePoisonStacks,
                -1,
                events);
        }

        public static void OnIncomingDamageHit(
            BattleState state,
            CombatantState actor,
            CombatantState recipient,
            List<BattleEvent> events)
        {
            if (state == null || recipient == null || !recipient.IsAlive)
                return;

            if (!HasTrait(recipient, MinionTraitCatalog.CorruptedCrabPoisonOnHit))
                return;

            var target = PickRandomAlivePlayer(state);
            if (target == null)
                return;

            StatusRules.ApplyStatus(
                state,
                target,
                StatusCatalog.Poison,
                MinionTraitCatalog.CorruptedCrabPoisonStacks,
                -1,
                events);
        }

        static CombatantState PickRandomAlivePlayer(BattleState state)
        {
            CombatantState first = null;
            var count = 0;
            foreach (var unit in state.Combatants)
            {
                if (!unit.IsAlive || unit.Team != TeamSide.Player)
                    continue;

                count++;
                if (count == 1)
                    first = unit;
            }

            return first;
        }

        static int ApplySeahorseSpeedAttackBonus(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int power)
        {
            if (state == null || actor == null || !HasTrait(actor, MinionTraitCatalog.SeahorseGuardSpeedAttack))
                return power;

            var sameSlotEnemy = FindAliveEnemyInSlot(state, actor.Slot);
            var bonusPercent = sameSlotEnemy == null
                ? 50
                : System.Math.Min(50, System.Math.Max(0, actor.Speed - sameSlotEnemy.Speed) * 10);

            if (bonusPercent <= 0)
                return power;

            return System.Math.Max(1, (int)System.Math.Round(power * (100 + bonusPercent) / 100f));
        }

        static int ApplyPhantomCaptainFrenzyBonus(BattleState state, CombatantState actor, int power)
        {
            if (state == null || actor == null || !HasTrait(actor, MinionTraitCatalog.PhantomCaptainFrenzy))
                return power;

            if (!HasLowHpOrDeadPlayer(state))
                return power;

            return System.Math.Max(1,
                (int)System.Math.Round(power * (100 + MinionTraitCatalog.PhantomCaptainFrenzyAttackPercent) / 100f));
        }

        static bool HasLowHpOrDeadPlayer(BattleState state)
        {
            foreach (var unit in state.Combatants)
            {
                if (unit.Team != TeamSide.Player)
                    continue;

                if (!unit.IsAlive)
                    return true;

                if (unit.MaxHp > 0 && unit.Hp * 100 / unit.MaxHp < 25)
                    return true;
            }

            return false;
        }

        static CombatantState FindAliveEnemyInSlot(BattleState state, FormationSlot slot)
        {
            foreach (var unit in state.Combatants)
            {
                if (unit.IsAlive && unit.Team == TeamSide.Enemy && unit.Slot == slot)
                    return unit;
            }

            return null;
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
