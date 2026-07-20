using System;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Battle.V091;
using Grimhand.Core;

namespace Grimhand.Battle.Effects
{
    public static class DamageRules
    {
        public static void ApplyDamage(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int power,
            CardType cardType,
            System.Collections.Generic.List<BattleEvent> events,
            bool canTriggerParry = true,
            bool isSacrificeDamage = false,
            BattleRng rng = null,
            string logSuffix = "",
            int cardCost = 0,
            int ignoreDefPercent = 0,
            bool redirectedByGuard = false,
            int sourceCardInstanceId = 0,
            bool isAoEWave = false)
        {
            if (target == null)
                return;

            var recipient = CombatMechanicsRules.ResolveDamageRecipient(state, actor, target);
            var isGuardRedirect = recipient.Id != target.Id;
            if (isGuardRedirect)
                redirectedByGuard = true;

            var outgoingPower = RelicBattleRules.ComputeOutgoingPower(
                state,
                actor,
                cardType,
                power,
                isSacrificeDamage,
                cardCost,
                applyPositionMultiplier: false);

            RelicEffectRules.AdjustRelicOutgoingDamage(
                state, actor, recipient, cardType, ref outgoingPower, ref ignoreDefPercent);

            if (actor != null)
                ignoreDefPercent = System.Math.Max(
                    ignoreDefPercent,
                    V091MechanicsRules.GetIgnoreDefPercentForAttacker(actor));

            RelicBattleRules.MarkFirstAttackConsumed(state, actor, cardType);

            var raw = outgoingPower;

            // 女王的命令等：在扣护甲前转嫁，避免原目标吞掉护甲后 hpDamage=0 导致转伤失效
            var defenderMitigated = 0;
            var hadDefenderArm = false;
            var originalRecipientId = recipient.Id;
            if (raw > 0 && !isSacrificeDamage)
            {
                var redirectHp = raw;
                hadDefenderArm = DefenderRespondArmRules.TryConsumeForIncomingPlayerAttack(
                    state, actor, ref recipient, ref redirectHp, events, out defenderMitigated, rng);
                if (hadDefenderArm)
                    raw = redirectHp;
            }

            var respondBlockerId = hadDefenderArm ? originalRecipientId : "";

            if (raw > 0)
                BossTraitRules.TryApplyFirstHitBlock(state, recipient, events);

            var effectiveBlock = CombatModifierRules.ComputeEffectiveBlock(recipient, ignoreDefPercent);
            var blocked = Math.Min(effectiveBlock, raw);
            // 无视 N% 护甲：按有效护甲折算格挡量，仍扣减真实护甲（例：10 护甲 + 50% 无视 + 10 伤 → 护甲 -5，HP -5）
            recipient.Block = Math.Max(0, recipient.Block - blocked);
            if (raw > 0)
                MinionTraitRules.OnIncomingDamageHit(state, actor, recipient, events);

            // 攻击命中（含全额被护甲吸收）：荆棘/受击计数等按「受到攻击」结算，不要求掉血
            if (raw > 0
                && !isSacrificeDamage
                && actor != null
                && actor.Team != recipient.Team
                && cardType == CardType.Attack)
            {
                V091MechanicsRules.OnAttacked(state, recipient, actor, events, rng);
            }

            var afterBlock = raw - blocked;

            var hpDamage = CombatModifierRules.ApplyIncomingDamageModifiers(
                recipient, afterBlock, ignoreDefPercent);

            if (redirectedByGuard)
                hpDamage = CombatMechanicsRules.ApplyGuardReduction(hpDamage);

            hpDamage = RelicBattleRules.ApplyIncomingDamageRelics(
                state, actor, recipient, hpDamage, rng, events);

            var beforeRespondMitigation = hpDamage;
            hpDamage = RespondEffectExecutor.ApplyMitigation(
                state, sourceCardInstanceId, recipient.Id, hpDamage);
            var respondMitigated = beforeRespondMitigation - hpDamage + defenderMitigated;
            var hadRespondDefense = hadDefenderArm
                || respondMitigated > 0
                || RespondEffectExecutor.HasRespondDefenseForHit(
                    state, sourceCardInstanceId, recipient.Id);

            if (StatusRules.HasStatus(recipient, StatusCatalog.Ethereal) && hpDamage > 0)
            {
                // v0.9 巫妖 s1_lv4：虚化中受伤改为0并回3HP（接管 ethereal 封顶1）
                if (!TalentBattleRules.TryHandleEtherealDamage(state, recipient, ref hpDamage, events))
                    hpDamage = 1;
            }

            if (hpDamage > 0 && rng != null)
            {
                var mods = state.Config?.RunModifiers;
                if (RelicBattleRules.TryWarriorBlockOnHit(recipient, mods, rng))
                {
                    var relicBlock = Math.Min(hpDamage, mods.WarriorBlockAmountOnHit);
                    hpDamage -= relicBlock;
                    blocked += relicBlock;
                    events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{recipient.DisplayName} 不动明王格挡")
                    {
                        CombatantId = recipient.Id,
                        Amount = relicBlock
                    });
                }
            }

            if (!isSacrificeDamage && cardType == CardType.Attack && actor != null && actor.Id != recipient.Id)
                recipient.HitThisTurn = true;

            if (hpDamage > 0)
                recipient.HitThisTurn = true;

            hpDamage = MinionTraitRules.ApplySpiderPoisonVulnerability(state, recipient, hpDamage);

            var hpBefore = recipient.Hp;
            var wasAlive = recipient.IsAlive;
            if (hpDamage > 0)
            {
                // v0.9 背水一战：HP将降至0以下时保留1HP
                PassiveCardMechanicsRules.TryTriggerLastStand(state, recipient, ref hpDamage, events);
                recipient.Hp = Math.Max(0, recipient.Hp - hpDamage);
            }

            BossTraitRules.TryTriggerGhostQueenEnrage(state, recipient, hpBefore, events);
            if (hpDamage > 0)
                MinionTraitRules.OnDamageTaken(state, recipient, hpDamage, events);

            if (hpDamage > 0)
                MinionTraitRules.OnDamageDealt(state, actor, recipient, hpDamage, events);

            if (!recipient.IsAlive && wasAlive
                && CombatMechanicsRules.TryPreventDeathWithReviveBlessing(state, recipient, events))
            {
                wasAlive = true;
            }

            var killed = wasAlive && !recipient.IsAlive;

            events.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{actor.DisplayName} -> {recipient.DisplayName}{logSuffix}")
            {
                CombatantId = actor.Id,
                TargetId = recipient.Id,
                Amount = hpDamage,
                BlockedAmount = blocked,
                RespondMitigatedAmount = respondMitigated,
                HadRespondDefense = hadRespondDefense,
                RespondBlockerId = respondBlockerId,
                IsSacrificeDamage = isSacrificeDamage,
                IsAoEWave = isAoEWave,
                CardType = cardType,
                CardInstanceId = sourceCardInstanceId
            });

            state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, recipient.Id, killed, hpDamage);

            if (hpDamage > 0)
            {
                CombatMechanicsRules.TryTriggerUnyielding(state, recipient, events);
                PassiveCardMechanicsRules.OnDamageTakenBattleWill(state, recipient, hpDamage, events);
                // v0.9 两界行者：受击后获虚化
                V09NewMechanicsRules.AfterDamageResolveEtherealOnNextHit(state, recipient, hpDamage, events);
                V091MechanicsRules.OnHpDamageTaken(state, recipient, hpDamage, events);
                // v0.9 蛇 s2_lv2：单次受到超过25%最大HP伤害后清负面
                TalentBattleRules.OnDamageTakenV09(state, recipient, hpDamage, events);
            }

            if (killed)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, recipient.DisplayName)
                {
                    CombatantId = recipient.Id
                });
                CombatantDeathRules.OnCharacterDied(state, recipient, events, rng);

                if (recipient.Team == TeamSide.Enemy && actor != null)
                    RelicEffectRules.OnEnemyKilled(state, actor, events, rng);
            }
            else if (hpDamage > 0 && actor != null && recipient.Team == TeamSide.Enemy)
            {
                TalentBattleRules.OnMageDamageDealt(state, actor, recipient, hpDamage, events);
            }
        }

        public static void ApplyBlock(
            CombatantState actor,
            int amount,
            System.Collections.Generic.List<BattleEvent> events,
            BattleState state = null,
            BattleRng rng = null)
        {
            if (actor == null || amount <= 0)
                return;

            if (state != null
                && actor.TalentDisableBlockGain
                && TalentBattleRules.HasTalent(state, "talent_knight_s2_lv10"))
            {
                actor.TalentIronWallPendingDamageBonus += amount;
                events.Add(new BattleEvent(BattleEventKind.IronWallConverted, $"{actor.DisplayName} 铁壁转化")
                {
                    CombatantId = actor.Id,
                    Amount = amount
                });
                return;
            }

            if (state != null)
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);

            amount = CombatModifierRules.ApplyBlockGainModifiers(actor, amount);
            // v0.9 重甲强化：获得护甲时额外 +20%
            amount = PassiveCardMechanicsRules.ApplyHeavyArmorBlockBonus(actor, amount);
            if (amount <= 0)
                return;

            actor.Block += amount;
            events.Add(new BattleEvent(BattleEventKind.BlockGained, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = amount
            });

            if (state != null)
            {
                PassiveCardMechanicsRules.TryTriggerGodDescendsOnBlockGain(state, actor, amount, events, rng);
                V091MechanicsRules.OnBlockGained(state, actor, amount, events);
            }
        }

        public static void ApplyHeal(
            BattleState state,
            CombatantState actor,
            int amount,
            System.Collections.Generic.List<BattleEvent> events,
            CombatantState healer = null,
            bool isLifesteal = false)
        {
            if (actor == null || !actor.IsAlive)
                return;

            var mods = state?.Config?.RunModifiers;
            var boosted = RelicBattleRules.ApplyHealBonus(mods, healer ?? actor, amount);
            var before = actor.Hp;
            actor.Hp = Math.Min(actor.MaxHp, actor.Hp + boosted);
            var healed = actor.Hp - before;
            var overflow = boosted - healed;
            if (healed <= 0 && overflow <= 0)
                return;

            if (healed > 0)
            {
                actor.HealedThisTurn = true;
                events.Add(new BattleEvent(BattleEventKind.HealApplied, actor.DisplayName)
                {
                    CombatantId = actor.Id,
                    Amount = healed,
                    IsLifesteal = isLifesteal
                });
                // v0.9 分血仪式：恶魔回复HP时治疗其他我方30%
                PassiveCardMechanicsRules.TryTriggerBloodSharingOnHeal(state, actor, healed, events);
                V091MechanicsRules.OnHealApplied(state, actor, healed, events, healer);
            }

            if (overflow > 0)
            {
                if (isLifesteal
                    && actor.CharacterDefinitionId == TalentBattleRules.RangerId
                    && TalentBattleRules.HasTalent(state, "talent_ranger_s2_lv2"))
                {
                    ApplyBlock(actor, overflow, events, state);
                }
                else if (healer?.CharacterDefinitionId == TalentBattleRules.MageId
                         && TalentBattleRules.HasTalent(state, "talent_mage_s1_lv8"))
                {
                    ApplyBlock(actor, overflow, events, state);
                }
            }

            if (mods != null && mods.HealGrantsBlock > 0 && healed > 0)
                ApplyBlock(actor, mods.HealGrantsBlock, events);
        }

        public static void ApplyRevive(
            BattleState state,
            CombatantState actor,
            int amount,
            System.Collections.Generic.List<BattleEvent> events,
            CombatantState healer = null)
        {
            if (actor == null || actor.IsAlive)
                return;

            var mods = state?.Config?.RunModifiers;
            var boosted = RelicBattleRules.ApplyHealBonus(mods, healer ?? actor, amount);
            var restored = Math.Max(1, Math.Min(actor.MaxHp, boosted));
            actor.Hp = restored;

            CombatantDeathRules.RestoreUsableCards(state, actor);
            RelicBattleRules.RefreshDerivedStats(state, actor, mods);

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = restored
            });

            if (mods != null && mods.HealGrantsBlock > 0)
                ApplyBlock(actor, mods.HealGrantsBlock, events);
        }
    }
}
