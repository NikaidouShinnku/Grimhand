using System.Collections.Generic;
using Grimhand.Battle.AI;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.V091
{
    /// <summary>v0.91 总览表新增卡牌机制。</summary>
    public static class V091MechanicsRules
    {
        public const string ThornArmorCardId = "w_thorn_armor";
        public const string RetaliatoryStrikeCardId = "w_retaliatory_strike";
        public const string BattleRoarCardId = "w_battle_roar";
        public const string FearlessChargeCardId = "w_fearless_charge";
        public const string RegroupCardId = "w_regroup";
        public const string SoulBondCardId = "p_soul_bond";
        public const string DoomProphecyCardId = "p_doom_prophecy";
        public const string SandForesightCardId = "p_sand_foresight";
        public const string LifeSpringCardId = "p_life_spring";
        public const string PainConvertCardId = "d_pain_convert";
        public const string BloodThirstCardId = "d_blood_thirst";
        public const string DemonEchoCardId = "d_demon_echo";
        public const string PoisonMistCardId = "v_poison_mist";
        public const string SnakeNestCardId = "v_snake_nest";
        public const string QueenKissCardId = "v_queen_kiss";
        public const string EtherealShieldCardId = "l_ethereal_shield";
        public const string PsionicScryCardId = "l_psionic_scry";
        public const string PsionicArrowRainCardId = "l_psionic_arrow_rain";
        public const string MemoryEternalVoidCardId = "l_memory_eternal_void";
        public const string MemoryPsionicMasteryCardId = "l_memory_psionic_mastery";
        public const string MemoryTimeDistortionCardId = "l_memory_time_distortion";

        public const int ThornReflectDamage = 5;
        public const int RetaliatoryBaseDamage = 13;
        public const int RetaliatoryBonusPerHit = 8;
        public const int BattleRoarAttackPercent = 3;
        public const int FearlessChargeBaseDamage = 15;
        public const int FearlessChargeCostReduction = 2;
        public const int RegroupDrawCount = 4;
        public const int RegroupBlockPerDiscard = 3;
        public const int SoulBondSharePercent = 50;
        public const int DoomProphecyAfterActDamage = 5;
        public const int LifeSpringHeal = 4;
        public const int PainConvertBlock = 6;
        public const int PainConvertHealOnHit = 2;
        public const int BloodThirstSacrifice = 3;
        public const int BloodThirstDraw = 2;
        public const int DemonEchoBaseDamage = 20;
        public const int DemonEchoCostReductionPerSacrifice = 2;
        public const int PoisonMistStacks = 2;
        public const int PoisonMistVulnerableStacks = 20;
        public const int QueenKissPoisonStacks = 30;
        public const int EtherealShieldBlock = 8;
        public const int PsionicScryCount = 3;
        public const int PsionicArrowRainDamage = 10;
        public const int PsionicArrowRainTurns = 3;

        public static bool IsV091SpecialCard(CardInstanceState card) =>
            card != null && card.DefinitionId switch
            {
                ThornArmorCardId or RetaliatoryStrikeCardId or BattleRoarCardId or FearlessChargeCardId
                    or RegroupCardId or SoulBondCardId or DoomProphecyCardId or SandForesightCardId
                    or LifeSpringCardId or PainConvertCardId or BloodThirstCardId or DemonEchoCardId
                    or PoisonMistCardId or SnakeNestCardId or QueenKissCardId or EtherealShieldCardId
                    or PsionicScryCardId or PsionicArrowRainCardId or MemoryEternalVoidCardId
                    or MemoryPsionicMasteryCardId or MemoryTimeDistortionCardId => true,
                _ => false
            };

        public static bool TryResolveSpecialCard(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || rng == null)
                return false;

            return card.DefinitionId switch
            {
                ThornArmorCardId => ResolveThornArmor(state, actor, card, events, rng),
                RetaliatoryStrikeCardId => ResolveRetaliatoryStrike(state, actor, card, events, rng),
                BattleRoarCardId => ResolveBattleRoar(state, actor, card, events),
                FearlessChargeCardId => ResolveFearlessCharge(state, actor, card, events, rng),
                RegroupCardId => ResolveRegroup(state, actor, card, events, rng),
                SoulBondCardId => ResolveSoulBond(state, actor, card, events),
                DoomProphecyCardId => ResolveDoomProphecy(state, actor, card, events),
                SandForesightCardId => ResolveSandForesight(state, actor, card, events),
                LifeSpringCardId => ResolveLifeSpring(state, actor, card, events),
                PainConvertCardId => ResolvePainConvert(state, actor, card, events, rng),
                BloodThirstCardId => ResolveBloodThirst(state, actor, card, events, rng),
                DemonEchoCardId => ResolveDemonEcho(state, actor, card, events, rng),
                PoisonMistCardId => ResolvePoisonMist(state, actor, card, events),
                SnakeNestCardId => ResolveApplyPermanentStatus(state, actor, card, StatusCatalog.SnakeNest, events),
                QueenKissCardId => ResolveQueenKiss(state, actor, card, events, rng),
                EtherealShieldCardId => ResolveEtherealShield(state, actor, card, events, rng),
                PsionicScryCardId => ResolvePsionicScry(state, actor, card, events, rng),
                PsionicArrowRainCardId => ResolvePsionicArrowRain(state, actor, card, events),
                MemoryEternalVoidCardId => ResolveApplyPermanentStatus(state, actor, card, StatusCatalog.EternalVoid, events),
                MemoryPsionicMasteryCardId => ResolveApplyPermanentStatus(state, actor, card, StatusCatalog.PsionicMastery, events),
                MemoryTimeDistortionCardId => ResolveMemoryTimeDistortion(state, actor, card, events, rng),
                _ => false
            };
        }

        public static int AdjustPlayCost(BattleState state, CombatantState owner, CardInstanceState card, int cost)
        {
            if (state == null || owner == null || card == null)
                return cost;

            if (card.DefinitionId == FearlessChargeCardId
                && owner.TookDamagePreviousTurn)
                cost = System.Math.Max(0, cost - FearlessChargeCostReduction);

            if (card.DefinitionId == DemonEchoCardId)
            {
                if (state.DemonEchoCostReductionByCardId.TryGetValue(card.DefinitionId, out var reduction))
                    cost = System.Math.Max(0, cost - reduction);
            }

            return cost;
        }

        public static void OnSacrificeCardPlayed(BattleState state, CardInstanceState card)
        {
            if (state == null || card == null)
                return;

            if (!state.DemonEchoCostReductionByCardId.ContainsKey(DemonEchoCardId))
                state.DemonEchoCostReductionByCardId[DemonEchoCardId] = 0;

            state.DemonEchoCostReductionByCardId[DemonEchoCardId] += DemonEchoCostReductionPerSacrifice;
        }

        public static void OnCardShuffledToDrawPile(BattleState state, CardInstanceState card)
        {
            if (state == null || card == null || card.DefinitionId != DemonEchoCardId)
                return;

            state.DemonEchoCostReductionByCardId.Remove(DemonEchoCardId);
        }

        /// <summary>受到攻击时触发（含护甲全额吸收）；用于荆棘护甲与受击计数。</summary>
        public static void OnAttacked(
            BattleState state,
            CombatantState recipient,
            CombatantState attacker,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || recipient == null)
                return;

            recipient.HitsTakenThisTurn++;

            if (attacker != null
                && attacker.IsAlive
                && attacker.Team != recipient.Team
                && StatusRules.HasStatus(recipient, StatusCatalog.ThornArmor))
            {
                var reflect = StatusRules.FindStatus(recipient, StatusCatalog.ThornArmor)?.Stacks
                              ?? ThornReflectDamage;
                if (reflect > 0)
                {
                    // Status：避免反伤再触发「受到攻击」钩子导致荆棘互反死循环
                    DamageRules.ApplyDamage(
                        state,
                        recipient,
                        attacker,
                        reflect,
                        CardType.Status,
                        events,
                        canTriggerParry: false,
                        rng: rng,
                        logSuffix: "（荆棘护甲）");
                }
            }
        }

        /// <summary>实际掉血时触发（苦痛转化等）。</summary>
        public static void OnHpDamageTaken(
            BattleState state,
            CombatantState recipient,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (state == null || recipient == null || hpDamage <= 0)
                return;

            if (StatusRules.HasStatus(recipient, StatusCatalog.PainConvert))
            {
                var painConvert = StatusRules.FindStatus(recipient, StatusCatalog.PainConvert);
                var heal = painConvert?.Stacks ?? PainConvertHealOnHit;
                if (heal > 0)
                    DamageRules.ApplyHeal(state, recipient, heal, events, recipient);
            }
        }

        public static void OnBlockGained(
            BattleState state,
            CombatantState actor,
            int amount,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || amount <= 0)
                return;

            if (actor.CharacterDefinitionId == "char_knight"
                && StatusRules.HasStatus(actor, StatusCatalog.BattleRoar))
            {
                var roar = StatusRules.FindStatus(actor, StatusCatalog.BattleRoar);
                var percent = roar?.Stacks ?? BattleRoarAttackPercent;
                if (percent > 0)
                {
                    StatusRules.ApplyStatus(
                        state,
                        actor,
                        StatusCatalog.AttackUpPercent,
                        percent,
                        -1,
                        events);
                }
            }
        }

        public static void OnHealApplied(
            BattleState state,
            CombatantState healed,
            int amount,
            List<BattleEvent> events,
            CombatantState healer)
        {
            if (state == null || healed == null || amount <= 0)
                return;

            if (!state.SoulBondPartnerByCombatantId.TryGetValue(healed.Id, out var partnerId))
                return;

            var partner = state.GetCombatant(partnerId);
            if (partner == null || !partner.IsAlive)
                return;

            var share = System.Math.Max(1, amount * SoulBondSharePercent / 100);
            DamageRules.ApplyHeal(state, partner, share, events, healer ?? healed);
        }

        public static void OnEnemyCardResolved(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || !actor.IsAlive)
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.DoomProphecy))
                return;

            DamageRules.ApplyDamage(
                state,
                actor,
                actor,
                DoomProphecyAfterActDamage,
                CardType.Status,
                events,
                canTriggerParry: false,
                rng: rng,
                logSuffix: "（末日预言）");
        }

        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null)
                return;

            state.SoulBondPartnerByCombatantId.Clear();

            if (state.RevealAllEnemyIntentsTurnsRemaining > 0)
            {
                RevealAllHiddenIntents(state, events);
                state.RevealAllEnemyIntentsTurnsRemaining--;
            }

            foreach (var ally in state.GetTeam(TeamSide.Player))
            {
                if (ally == null || !ally.IsAlive)
                    continue;

                if (StatusRules.HasStatus(ally, StatusCatalog.LifeSpring))
                {
                    var lifeSpring = StatusRules.FindStatus(ally, StatusCatalog.LifeSpring);
                    var heal = lifeSpring?.Stacks ?? LifeSpringHeal;
                    if (heal > 0)
                        DamageRules.ApplyHeal(state, ally, heal, events, ally);
                }

                if (state.PendingDelayedBlockByCombatantId.TryGetValue(ally.Id, out var pendingBlock)
                    && pendingBlock > 0)
                {
                    DamageRules.ApplyBlock(ally, pendingBlock, events, state, rng);
                    state.PendingDelayedBlockByCombatantId.Remove(ally.Id);
                }

                if (StatusRules.HasStatus(ally, StatusCatalog.PsionicArrowRain))
                {
                    var rain = StatusRules.FindStatus(ally, StatusCatalog.PsionicArrowRain);
                    if (rain != null && rain.RemainingTurns != 0)
                    {
                        var target = PickRandomAliveEnemy(state, rng);
                        if (target != null)
                        {
                            var power = rain.Stacks > 0 ? rain.Stacks : PsionicArrowRainDamage;
                            DamageRules.ApplyDamage(
                                state,
                                ally,
                                target,
                                power,
                                CardType.Attack,
                                events,
                                rng: rng,
                                logSuffix: "（灵能箭雨）");
                        }
                    }
                }

                if (StatusRules.HasStatus(ally, StatusCatalog.SnakeNest))
                    TryAddRandomSnakeAttackToHand(state, ally, events, rng);
            }

            ProcessQueenKissConversion(state, events);
        }

        public static void ResetCombatantTurnFlags(CombatantState combatant)
        {
            if (combatant == null)
                return;

            combatant.HitsTakenThisTurn = 0;
        }

        public static int GetIgnoreDefPercentForAttacker(CombatantState attacker)
        {
            if (attacker == null)
                return 0;

            if (StatusRules.HasStatus(attacker, StatusCatalog.Ethereal)
                && StatusRules.HasStatus(attacker, StatusCatalog.PsionicMastery))
                return 100;

            return 0;
        }

        static bool ResolveApplyPermanentStatus(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            string statusId,
            List<BattleEvent> events)
        {
            StatusRules.ApplyStatus(state, actor, statusId, 1, -1, events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveThornArmor(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var block = 12 + card.UpgradeLevel;
            DamageRules.ApplyBlock(actor, block, events, state, rng);
            StatusRules.ApplyStatus(state, actor, StatusCatalog.ThornArmor, ThornReflectDamage, 1, events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
            return true;
        }

        static bool ResolveBattleRoar(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            var percent = BattleRoarAttackPercent + card.UpgradeLevel;
            StatusRules.ApplyStatus(state, actor, StatusCatalog.BattleRoar, percent, -1, events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveLifeSpring(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            var heal = LifeSpringHeal + card.UpgradeLevel;
            foreach (var ally in state.GetTeam(TeamSide.Player))
            {
                if (ally == null || !ally.IsAlive)
                    continue;
                StatusRules.ApplyStatus(state, ally, StatusCatalog.LifeSpring, heal, -1, events);
            }

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveRetaliatoryStrike(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var reachAction = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle
            };
            var target = TargetRules.ResolveTarget(
                state, actor, EffectTarget.DefaultEnemy, card.InstanceId, rng, reachAction);
            if (target == null
                || !TargetRules.IsTargetValidForAction(state, target, TargetReach.FrontAndMiddle, reachAction))
                return true;

            var power = RetaliatoryBaseDamage
                        + card.UpgradeLevel * 2
                        + actor.HitsTakenThisTurn * RetaliatoryBonusPerHit;
            DamageRules.ApplyDamage(
                state, actor, target, power, card.CardType, events,
                rng: rng, sourceCardInstanceId: card.InstanceId);
            return true;
        }

        static bool ResolveFearlessCharge(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var reachAction = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle
            };
            var target = TargetRules.ResolveTarget(
                state, actor, EffectTarget.DefaultEnemy, card.InstanceId, rng, reachAction);
            if (target == null
                || !TargetRules.IsTargetValidForAction(state, target, TargetReach.FrontAndMiddle, reachAction))
                return true;

            var power = FearlessChargeBaseDamage + card.UpgradeLevel * 3;
            if (actor.TookDamagePreviousTurn)
                power *= 2;

            DamageRules.ApplyDamage(
                state, actor, target, power, card.CardType, events,
                rng: rng, sourceCardInstanceId: card.InstanceId);
            DamageRules.ApplyBlock(actor, power, events, state, rng);
            return true;
        }

        static bool ResolveRegroup(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var discarded = 0;
            for (var i = state.PlayerHand.Count - 1; i >= 0; i--)
            {
                var handCard = state.PlayerHand[i];
                if (handCard.InstanceId == card.InstanceId)
                    continue;

                state.PlayerHand.RemoveAt(i);
                state.PlayerDiscardPile.Add(handCard);
                discarded++;
            }

            var drawCount = RegroupDrawCount + System.Math.Min(2, card.UpgradeLevel);
            DeckRules.DrawCards(state, actor.Team, rng, drawCount, events);
            if (discarded > 0)
                DamageRules.ApplyBlock(actor, discarded * RegroupBlockPerDiscard, events, state, rng);

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveSoulBond(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            var ally = TargetRules.ResolveTarget(
                state, actor, EffectTarget.FrontAlly, card.InstanceId, null, null);
            if (ally == null || ally.Team != actor.Team)
                ally = actor;

            state.SoulBondPartnerByCombatantId[actor.Id] = ally.Id;
            if (ally.Id != actor.Id)
                state.SoulBondPartnerByCombatantId[ally.Id] = actor.Id;

            StatusRules.ApplyStatus(state, actor, StatusCatalog.SoulBond, SoulBondSharePercent, 1, events);
            if (ally.Id != actor.Id)
                StatusRules.ApplyStatus(state, ally, StatusCatalog.SoulBond, SoulBondSharePercent, 1, events);

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, ally.Id, false, 0);
            return true;
        }

        static bool ResolveDoomProphecy(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                StatusRules.ApplyStatus(
                    state, enemy, StatusCatalog.DoomProphecy, DoomProphecyAfterActDamage, -1, events);
            }

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveSandForesight(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            RevealAllHiddenIntents(state, events);
            state.RevealAllEnemyIntentsTurnsRemaining = 2;
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static void RevealAllHiddenIntents(BattleState state, List<BattleEvent> events)
        {
            foreach (var intent in state.EnemyIntents)
            {
                if (!intent.IsHidden)
                    continue;

                intent.IsHidden = false;
                var intentCard = state.GetCard(intent.CardInstanceId);
                var intentOwner = intent.OwnerCombatantId != null
                    ? state.GetCombatant(intent.OwnerCombatantId)
                    : null;
                var label = intentCard != null
                    ? CardPowerRules.DescribeCardEffect(intentCard, intentOwner, false)
                    : "未知意图";
                events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared, label)
                {
                    CardInstanceId = intent.CardInstanceId
                });
            }
        }

        static bool ResolvePainConvert(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var block = PainConvertBlock + card.UpgradeLevel;
            var healOnHit = PainConvertHealOnHit + card.UpgradeLevel;
            DamageRules.ApplyBlock(actor, block, events, state, rng);
            StatusRules.ApplyStatus(
                state,
                actor,
                StatusCatalog.PainConvert,
                healOnHit,
                1,
                events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
            return true;
        }

        static bool ResolveBloodThirst(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            DamageRules.ApplyDamage(
                state, actor, actor, BloodThirstSacrifice, CardType.Status, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: card.InstanceId);
            DeckRules.DrawCards(state, actor.Team, rng, BloodThirstDraw, events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveDemonEcho(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var reachAction = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.Any,
                Value = DemonEchoBaseDamage
            };
            var target = TargetRules.ResolveTarget(
                state, actor, EffectTarget.DefaultEnemy, card.InstanceId, rng, reachAction);
            if (target == null
                || !TargetRules.IsTargetValidForAction(state, target, TargetReach.Any, reachAction))
                return true;

            var power = DemonEchoBaseDamage + card.UpgradeLevel * 5;
            DamageRules.ApplyDamage(
                state, actor, target, power, card.CardType, events,
                rng: rng, sourceCardInstanceId: card.InstanceId);
            return true;
        }

        static bool ResolvePoisonMist(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            var poisonStacks = PoisonMistStacks + card.UpgradeLevel;
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                var hadPoison = StatusRules.HasStatus(enemy, StatusCatalog.Poison);
                StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, poisonStacks, -1, events);
                if (hadPoison || StatusRules.HasStatus(enemy, StatusCatalog.Poison))
                {
                    StatusRules.ApplyStatus(
                        state,
                        enemy,
                        StatusCatalog.Vulnerable,
                        PoisonMistVulnerableStacks,
                        2,
                        events);
                }
            }

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveQueenKiss(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var target = PickRandomAliveEnemy(state, rng);
            if (target == null)
                return true;

            var stacks = QueenKissPoisonStacks + card.UpgradeLevel * 3;
            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, stacks, -1, events);
            state.QueenKissConversionPending = true;
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target.Id, false, 0);
            return true;
        }

        static void ProcessQueenKissConversion(BattleState state, List<BattleEvent> events)
        {
            if (state == null || !state.QueenKissConversionPending)
                return;

            state.QueenKissConversionPending = false;
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                var poison = StatusRules.FindStatus(enemy, StatusCatalog.Poison);
                if (poison == null || poison.Stacks <= 0)
                    continue;

                var duration = poison.RemainingTurns < 0 ? -1 : poison.RemainingTurns;
                StatusRules.RemoveStatus(enemy, StatusCatalog.Poison, poison.Stacks, events);
                StatusRules.ApplyStatus(
                    state,
                    enemy,
                    StatusCatalog.Vulnerable,
                    poison.Stacks,
                    duration,
                    events);
            }
        }

        static bool ResolveEtherealShield(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var block = EtherealShieldBlock + card.UpgradeLevel;
            if (StatusRules.HasStatus(actor, StatusCatalog.Ethereal))
            {
                state.PendingDelayedBlockByCombatantId[actor.Id] =
                    state.PendingDelayedBlockByCombatantId.TryGetValue(actor.Id, out var existing)
                        ? existing + block
                        : block;
                events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{actor.DisplayName} 下回合获得{block}护甲")
                {
                    CombatantId = actor.Id,
                    Amount = block
                });
            }
            else
            {
                DamageRules.ApplyBlock(actor, block, events, state, rng);
            }

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
            return true;
        }

        static bool ResolvePsionicScry(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var pile = state.PlayerDrawPile;
            var take = System.Math.Min(PsionicScryCount, pile.Count);
            if (take <= 0)
                return true;

            var top = new List<CardInstanceState>();
            for (var i = 0; i < take; i++)
                top.Add(pile[i]);

            pile.RemoveRange(0, take);
            if (top.Count > 1 && rng != null)
            {
                var discardIndex = rng.NextIndex(top.Count);
                var discarded = top[discardIndex];
                top.RemoveAt(discardIndex);
                state.PlayerDiscardPile.Add(discarded);
                events.Add(new BattleEvent(BattleEventKind.CardDiscarded, discarded.DisplayName)
                {
                    CardInstanceId = discarded.InstanceId
                });
            }

            for (var i = top.Count - 1; i >= 0; i--)
                pile.Insert(0, top[i]);

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "灵能预知：检视牌组顶")
            {
                CombatantId = actor.Id,
                Amount = take
            });
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolvePsionicArrowRain(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            StatusRules.ApplyStatus(
                state,
                actor,
                StatusCatalog.PsionicArrowRain,
                PsionicArrowRainDamage + card.UpgradeLevel,
                PsionicArrowRainTurns,
                events);
            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static bool ResolveMemoryTimeDistortion(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var enemyTurn = EnemyTurnPlanner.PrepareEnemyTurn(state, rng);
            state.EnemyPlan.PlayQueue.Clear();
            state.EnemyPlan.PlayQueue.AddRange(enemyTurn.Plan.PlayQueue);
            state.EnemyPlan.EnergySpent = enemyTurn.Plan.EnergySpent;
            state.EnemyIntents.Clear();
            state.EnemyIntents.AddRange(enemyTurn.Intents);
            TargetRules.PrerollEnemyAutoTargets(state, state.EnemyPlan, rng);

            foreach (var intent in state.EnemyIntents)
            {
                if (intent.IsHidden)
                    continue;

                var intentCard = state.GetCard(intent.CardInstanceId);
                var intentOwner = intent.OwnerCombatantId != null
                    ? state.GetCombatant(intent.OwnerCombatantId)
                    : null;
                var label = intentCard != null
                    ? CardPowerRules.DescribeCardEffect(intentCard, intentOwner, false)
                    : "未知意图";
                events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared, label)
                {
                    CardInstanceId = intent.CardInstanceId
                });
            }

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
            return true;
        }

        static void TryAddRandomSnakeAttackToHand(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var pool = new List<CardInstanceState>();
            foreach (var pileCard in state.PlayerDrawPile)
            {
                if (pileCard.OwnerCharacterId == "char_snake_queen" && pileCard.CardType == CardType.Attack)
                    pool.Add(pileCard);
            }

            foreach (var pileCard in state.PlayerDiscardPile)
            {
                if (pileCard.OwnerCharacterId == "char_snake_queen" && pileCard.CardType == CardType.Attack)
                    pool.Add(pileCard);
            }

            if (pool.Count == 0)
                return;

            var source = pool[rng.NextIndex(pool.Count)];
            var clone = CloneCardForHand(state, source);
            clone.Cost = 0;
            state.PlayerHand.Add(clone);
            events.Add(new BattleEvent(BattleEventKind.CardDrawn, clone.DisplayName)
            {
                CardInstanceId = clone.InstanceId,
                CombatantId = actor.Id
            });
        }

        static CombatantState PickRandomAliveEnemy(BattleState state, BattleRng rng)
        {
            var pool = new List<CombatantState>();
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (enemy.IsAlive)
                    pool.Add(enemy);
            }

            return pool.Count == 0 ? null : pool[rng.NextIndex(pool.Count)];
        }

        static CardInstanceState CloneCardForHand(BattleState state, CardInstanceState source)
        {
            var clone = new CardInstanceState
            {
                InstanceId = state.NextCardInstanceId++,
                DefinitionId = source.DefinitionId,
                OwnerCharacterId = source.OwnerCharacterId,
                OwnerCombatantId = source.OwnerCombatantId,
                Cost = source.Cost,
                CardType = source.CardType,
                IsUsable = source.IsUsable,
                DisplayName = source.DisplayName,
                UpgradeLevel = source.UpgradeLevel
            };

            foreach (var keyword in source.Keywords)
                clone.Keywords.Add(keyword);

            foreach (var action in source.Actions)
                clone.Actions.Add(EffectActionSpec.Clone(action));

            state.CardsById[clone.InstanceId] = clone;
            return clone;
        }
    }
}
