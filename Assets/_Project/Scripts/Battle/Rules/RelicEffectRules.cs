using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>遗物特殊效果的战斗内钩子（对照 RelicDatabase 图鉴）。</summary>
    public static class RelicEffectRules
    {
        public const string DemonCharacterId = "char_ranger";
        public const string WarriorCharacterId = "char_knight";

        public static void ResetTurnFlags(CombatantState combatant)
        {
            if (combatant == null)
                return;

            combatant.FirstAttackBonusPending = true;
            combatant.FirstDefenseBonusPending = true;
            combatant.FirstHitReductionPending = true;
            combatant.WarriorFirstHitBlockPending = true;
            combatant.UsedAttackThisTurn = false;
            combatant.UsedDefenseThisTurn = false;
            combatant.TurnAttackBonusPercent = 0;
            combatant.TurnDefenseBonusPercent = 0;
        }

        public static void ProcessTurnStart(
            BattleState state,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (state == null)
                return;

            var mods = state.Config?.RunModifiers;
            state.TeamFirstHitReductionPending = mods != null && mods.FirstHitDamageReductionPercent > 0f;

            foreach (var c in state.Combatants)
            {
                ResetTurnFlags(c);
                if (c.InvulnerableTurnsRemaining > 0)
                    c.InvulnerableTurnsRemaining--;
            }

            RelicBattleRules.RefreshAllDerivedStats(state);

            if (mods == null)
                return;

            if (mods.TurnStartRandomAllyBlock > 0)
            {
                var allies = CollectAlivePlayerTeam(state);
                if (allies.Count > 0 && rng != null)
                {
                    var pick = allies[rng.NextIndex(allies.Count)];
                    DamageRules.ApplyBlock(pick, mods.TurnStartRandomAllyBlock, events, state);
                }
            }

            if (mods.TurnStartTeamBlock > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                    DamageRules.ApplyBlock(ally, mods.TurnStartTeamBlock, events, state);
            }
        }

        public static void ProcessEndOfTurn(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var mods = state.Config?.RunModifiers;

            if (mods != null && mods.EndTurnTeamHeal > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                    DamageRules.ApplyHeal(state, ally, mods.EndTurnTeamHeal, events);
            }

            if (mods != null && mods.EndTurnEnemyFireDamage > 0)
            {
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (!enemy.IsAlive)
                        continue;

                    var before = enemy.Hp;
                    enemy.Hp = Math.Max(0, enemy.Hp - mods.EndTurnEnemyFireDamage);
                    var dealt = before - enemy.Hp;
                    if (dealt <= 0)
                        continue;

                    events.Add(new BattleEvent(BattleEventKind.DamageApplied,
                        $"赤红烈焰靴 -> {enemy.DisplayName}")
                    {
                        TargetId = enemy.Id,
                        Amount = dealt,
                        IsAoEWave = true
                    });

                    if (!enemy.IsAlive)
                    {
                        events.Add(new BattleEvent(BattleEventKind.CharacterDied, enemy.DisplayName)
                        {
                            CombatantId = enemy.Id
                        });
                        CombatantDeathRules.OnCharacterDied(state, enemy, events);
                    }
                }
            }

            if (mods != null && mods.AttackAndDefenseSameTurnHeal > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                {
                    if (!ally.UsedAttackThisTurn || !ally.UsedDefenseThisTurn)
                        continue;

                    DamageRules.ApplyHeal(state, ally, mods.AttackAndDefenseSameTurnHeal, events);
                }
            }
        }

        public static int GetBattleSpeedBonus(BattleState state, CombatantState combatant)
        {
            var mods = state?.Config?.RunModifiers;
            if (mods == null || combatant == null || combatant.Team != TeamSide.Player)
                return 0;

            if (mods.BattleStartSpeedBonus <= 0 || mods.BattleStartSpeedBonusTurns <= 0)
                return 0;

            return state.TurnNumber <= mods.BattleStartSpeedBonusTurns ? mods.BattleStartSpeedBonus : 0;
        }

        public static int AdjustSacrificeSelfDamage(
            RunModifierSnapshot mods,
            CombatantState actor,
            int rawDamage)
        {
            if (mods == null || actor == null || mods.SacrificeHpCostReductionPercent <= 0)
                return rawDamage;

            if (actor.CharacterDefinitionId != DemonCharacterId)
                return rawDamage;

            var reduced = (int)System.Math.Round(
                rawDamage * (100f - mods.SacrificeHpCostReductionPercent) / 100f);
            return Math.Max(1, reduced);
        }

        public static void OnSacrificeCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (state == null || actor == null || card == null || !card.Keywords.Contains("sacrifice"))
                return;

            PassiveCardMechanicsRules.TryTriggerFinalBloodRitualOnSacrifice(
                state, actor, card, events, rng);

            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.SacrificeStackAttackBonus <= 0)
                return;

            if (actor.CharacterDefinitionId != DemonCharacterId)
                return;

            actor.SacrificeAttackStacks += mods.SacrificeStackAttackBonus;
            RelicBattleRules.RefreshDerivedStats(state, actor, mods);
            events.Add(new BattleEvent(BattleEventKind.BlockGained,
                $"{actor.DisplayName} 血祭坛 ATK+{mods.SacrificeStackAttackBonus}")
            {
                CombatantId = actor.Id,
                Amount = actor.SacrificeAttackStacks
            });
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null)
                return;

            if (card.CardType == CardType.Attack)
            {
                actor.UsedAttackThisTurn = true;
                actor.FirstAttackBonusPending = false;
            }
            else if (card.CardType == CardType.Defense)
            {
                actor.UsedDefenseThisTurn = true;
                actor.FirstDefenseBonusPending = false;
            }

            if (card.CardType == CardType.Attack && rng != null)
                TryProcAttackBurn(state, actor, card, events, rng);

            OnSacrificeCardResolved(state, actor, card, events, rng);
            MinionTraitRules.OnCardResolved(state, actor, card, events);
            if (card.CardType == CardType.Attack)
                MinionTraitRules.ConsumeBloodRageAfterAttack(actor, card.CardType);
        }

        public static void OnEnemyKilled(
            BattleState state,
            CombatantState killer,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || killer == null || state.JadeDaggerFirstKillConsumed || rng == null)
                return;

            var mods = state.Config?.RunModifiers;
            if (mods == null || !mods.JadeDaggerFirstKillBonus)
                return;

            state.JadeDaggerFirstKillConsumed = true;
            DeckRules.DrawCards(state, TeamSide.Player, rng, 1, events);
            state.EnergyCurrent = Math.Min(state.EnergyMax, state.EnergyCurrent + 2);
            events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "翡翠短刀：首杀奖励")
            {
                Energy = state.EnergyCurrent,
                EnergyMax = state.EnergyMax,
                EnergyRemaining = state.EnergyCurrent
            });
        }

        static void TryProcAttackBurn(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.AttackBurnProcChance <= 0f)
                return;

            if (actor.Team != TeamSide.Player)
                return;

            var roll = rng.NextUInt() % 1000u / 1000f;
            if (roll >= mods.AttackBurnProcChance)
                return;

            var targetId = state.ResolutionTargets.TryGetValue(card.InstanceId, out var id)
                ? id
                : null;
            var target = targetId != null ? state.GetCombatant(targetId) : null;
            target ??= PositionRules.PickDefaultTarget(state, actor.Team);
            if (target == null || !target.IsAlive)
                return;

            var stacks = mods.AttackBurnStacks > 0 ? mods.AttackBurnStacks : 5;
            var duration = mods.AttackBurnDurationTurns > 0 ? mods.AttackBurnDurationTurns : 5;
            StatusRules.ApplyStatus(state, target, StatusCatalog.Burn, stacks, duration, events);
        }

        public static bool TryDodgeIncoming(
            BattleState state,
            RunModifierSnapshot mods,
            CombatantState target,
            BattleRng rng)
        {
            if (target == null || rng == null)
                return false;

            var dodge = state?.ConsumableDodgeBonusThisTurn ?? 0f;
            dodge += target.DodgeChanceBonus;
            if (mods != null && target.Team == TeamSide.Player)
                dodge += mods.DodgeChanceOnHit;

            if (dodge <= 0f)
                return false;

            var roll = rng.NextUInt() % 1000u / 1000f;
            return roll < dodge;
        }

        public static bool TryMiracleLeafSave(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events,
            ref int hpDamage)
        {
            if (state == null || target == null || target.Team != TeamSide.Player)
                return false;

            if (state.MiracleLeafRevivesRemaining <= 0)
                return false;

            if (target.Hp - hpDamage > 0)
                return false;

            state.MiracleLeafRevivesRemaining--;
            var restore = Math.Max(1, (int)Math.Round(target.MaxHp * 0.2f));
            target.Hp = restore;
            target.InvulnerableTurnsRemaining = 1;
            hpDamage = 0;

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived,
                $"{target.DisplayName} 奇迹之叶")
            {
                CombatantId = target.Id,
                Amount = restore
            });

            return true;
        }

        public static bool ShouldExpandBackRowReach(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            var mods = state?.Config?.RunModifiers;
            if (mods == null || !mods.BackRowAttackAnyTarget || owner == null || card == null)
                return false;

            if (owner.Team != TeamSide.Player)
                return false;

            if (card.CardType != CardType.Attack)
                return false;

            return PositionRules.GetEffectiveSlot(state, owner) == FormationSlot.Back;
        }

        static List<CombatantState> CollectAlivePlayerTeam(BattleState state)
        {
            var list = new List<CombatantState>();
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player && c.IsAlive)
                    list.Add(c);
            }

            return list;
        }
    }
}
