using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>策划案中的被动/触发型卡牌机制（天神下凡、最终鲜血仪式、无尽血刃等）。</summary>
    public static class PassiveCardMechanicsRules
    {
        public const string EndlessBladeCardId = "d_endless_blade";
        public const string SandSpearReforgeCardId = "p_sand_spear_reforge";
        public const string SpiderFatalBindCardId = "m_spider_fatal_bind";
        public const string GargoyleSunderCardId = "m_gargoyle_sunder";
        public const string FinalBindCardId = "m_final_bind";
        public const string MagicLightningCardId = "m_magic_lightning";
        public const string GolemCrackFistCardId = "m_golem_crack_fist";
        public const string FinalGuardCardId = "m_final_guard";
        public const int GolemCrackFistBonusBlock = 8;
        public const int GodDescendsFlatDamage = 8;
        public const int GodDescendsAttackScalePercent = 120;
        public const int FinalBloodRitualDraw = 1;
        public const int FinalBloodRitualHeal = 5;
        public const int SandSpearReforgeBaseDamage = 4;
        public const int FinalBindBonusPoisonStacks = 30;

        // v0.9 玩家卡牌被动常量
        public const int RespondStanceBlock = 8;
        public const int BattleWillAttackPercentPerHit = 5;
        public const int HeavyArmorBlockBonusPercent = 20;
        public const int FinalBulwarkKeepPercent = 50;
        public const int RotAvatarPoisonStacks = 2;
        public const int BloodFrenzyAttackPercent = 5;
        public const int BloodSharingAllyHealPercent = 30;
        public const int PlagueSpreadChancePercent = 30;
        public const string HolyInfusionCardId = "p_holy_infusion";

        public static int GetEndlessBladeDamageMultiplierPercent(BattleState state, int cardInstanceId)
        {
            if (state == null || cardInstanceId <= 0)
                return 100;

            return state.CardInstanceDamageMultiplierPercent.TryGetValue(cardInstanceId, out var pct)
                ? Math.Max(100, pct)
                : 100;
        }

        public static int ApplyEndlessBladeMultiplier(BattleState state, CardInstanceState card, int power)
        {
            if (state == null || card == null || card.DefinitionId != EndlessBladeCardId || power <= 0)
                return power;

            var mul = GetEndlessBladeDamageMultiplierPercent(state, card.InstanceId);
            return Math.Max(1, (int)Math.Round(power * mul / 100f));
        }

        public static void OnEndlessBladeResolved(BattleState state, CardInstanceState card, List<BattleEvent> events)
        {
            if (state == null || card == null || card.DefinitionId != EndlessBladeCardId)
                return;

            var current = GetEndlessBladeDamageMultiplierPercent(state, card.InstanceId);
            state.CardInstanceDamageMultiplierPercent[card.InstanceId] = current * 2;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{card.DisplayName} 伤害翻倍")
            {
                CardInstanceId = card.InstanceId,
                Amount = state.CardInstanceDamageMultiplierPercent[card.InstanceId]
            });
        }

        public const int EndlessBladeSacrificeHpPercent = 25;

        /// <summary>无尽血刃的 25% 最大生命值献祭代价，须在造成伤害前结算。</summary>
        public static void ApplyEndlessBladeSacrifice(
            BattleState state, CombatantState actor, CardInstanceState card,
            List<BattleEvent> events, BattleRng rng)
        {
            if (state == null || actor == null || card == null || card.DefinitionId != EndlessBladeCardId)
                return;
            if (!actor.IsAlive || actor.MaxHp <= 0)
                return;

            var rawDamage = (int)Math.Round(actor.MaxHp * EndlessBladeSacrificeHpPercent / 100f);
            if (rawDamage <= 0)
                return;

            var dmg = RelicEffectRules.AdjustSacrificeSelfDamage(
                state, state.Config?.RunModifiers, actor, rawDamage);
            DamageRules.ApplyDamage(
                state, actor, actor, dmg, CardType.Status, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: card.InstanceId);
            if (state.LastAction.DamageAmount > 0)
                TalentBattleRules.OnSacrificeHpSpent(state, actor, state.LastAction.DamageAmount);
        }

        public static void TryTriggerFinalBloodRitualOnSacrifice(
            BattleState state,
            CombatantState actor,
            CardInstanceState sacrificeCard,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || sacrificeCard == null || rng == null)
                return;

            if (!sacrificeCard.Keywords.Contains("sacrifice"))
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.FinalBloodRitual))
                return;

            DamageRules.ApplyHeal(state, actor, FinalBloodRitualHeal, events, actor);
            state.PendingDrawNextTurn += FinalBloodRitualDraw;
            events.Add(new BattleEvent(BattleEventKind.CardDrawn,
                $"{actor.DisplayName} 最终鲜血仪式：下回合额外抽 {FinalBloodRitualDraw} 张")
            {
                CombatantId = actor.Id,
                Amount = FinalBloodRitualDraw
            });
        }

        public static void TryTriggerGodDescendsOnBlockGain(
            BattleState state,
            CombatantState actor,
            int blockGained,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || blockGained <= 0 || rng == null)
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.GodDescends))
                return;

            var power = GodDescendsFlatDamage;
            if (actor.Attack > 0)
                power += (int)Math.Round(actor.Attack * GodDescendsAttackScalePercent / 100f);

            if (power <= 0)
                return;

            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                DamageRules.ApplyDamage(
                    state,
                    actor,
                    enemy,
                    power,
                    CardType.Attack,
                    events,
                    rng: rng,
                    logSuffix: "（天神下凡）",
                    isAoEWave: true);
            }
        }

        /// <summary>玩家打出消耗牌：远征沙矛计数 +1（不计入沙矛本身结算，结算后再 +1）。</summary>
        public static void RecordExpeditionExhaustCardPlayed(
            BattleState state,
            List<BattleEvent> events)
        {
            if (state?.Config?.RunModifiers == null)
                return;

            state.Config.RunModifiers.SandSpearExhaustCardsPlayed += 1;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "沙矛计数：消耗牌 +1")
            {
                Amount = state.Config.RunModifiers.SandSpearExhaustCardsPlayed
            });
        }

        public static int GetSandSpearExhaustCount(BattleState state) =>
            state?.Config?.RunModifiers?.SandSpearExhaustCardsPlayed ?? 0;

        /// <summary>打出沙矛重塑：按远征累计消耗牌次数，每次随机敌人 4 伤。</summary>
        public static void OnSandSpearReforgePlayed(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || rng == null)
                return;

            var hits = GetSandSpearExhaustCount(state);
            if (hits <= 0)
            {
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{card.DisplayName}：尚无消耗牌计数")
                {
                    CombatantId = actor.Id,
                    CardInstanceId = card.InstanceId
                });
                return;
            }

            for (var i = 0; i < hits; i++)
            {
                var target = PickRandomAliveEnemy(state, rng);
                if (target == null)
                    break;

                DamageRules.ApplyDamage(
                    state, actor, target, SandSpearReforgeBaseDamage, CardType.Attack, events,
                    rng: rng, logSuffix: "（沙矛重塑）", sourceCardInstanceId: card.InstanceId);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{card.DisplayName}：4 伤 ×{hits}（消耗牌计数）")
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId,
                Amount = hits
            });
        }

        public static void AfterSingleHitResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            CombatantState target,
            bool targetHadBlockBeforeHit,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || target == null)
                return;

            if (card.DefinitionId == MagicLightningCardId)
            {
                var poisonStacks = StatusRules.GetStatusStacks(target, StatusCatalog.Poison);
                if (poisonStacks > 0)
                {
                    StatusRules.ApplyStatus(
                        state, target, StatusCatalog.Burn, poisonStacks, -1, events);
                }
            }

            if (card.DefinitionId == GolemCrackFistCardId && !targetHadBlockBeforeHit)
            {
                DamageRules.ApplyBlock(actor, GolemCrackFistBonusBlock, events, state, rng);
            }
        }

        public static void OnFinalSummonPendingExpired(
            BattleState state,
            CombatantState caster,
            List<BattleEvent> events)
        {
            if (state == null || caster == null || !caster.IsAlive)
                return;

            if (caster.CharacterDefinitionId != "char_jellyfish_caster")
                return;

            var bonusHp = Math.Max(1, caster.MaxHp / 2);
            var slot = caster.Slot;

            if (!state.Config.SummonTemplates.TryGetValue(
                    MinionTraitCatalog.AbyssCreatureCharacterId, out var template))
            {
                SummonRules.SelfDestruct(state, caster, events);
                return;
            }

            SummonRules.SelfDestruct(state, caster, events);
            SummonRules.SpawnFromTemplate(state, template, slot, events, bonusHp);
        }

        public static void OnFinalGuardResponded(BattleState state, List<BattleEvent> events)
        {
            if (state == null || state.EnergyCurrent <= 0)
                return;

            state.EnergyCurrent = 0;
            events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "终焉守护：能量被清空")
            {
                Energy = 0,
                EnergyMax = state.EnergyMax,
                EnergyRemaining = 0
            });
        }

        public static void OnSpiderFatalBindResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null || !actor.IsAlive)
                return;

            if (card.DefinitionId != SpiderFatalBindCardId)
                return;

            var selfDamage = Math.Max(1, actor.Hp / 2);
            DamageRules.ApplyDamage(
                state, actor, actor, selfDamage, CardType.Attack, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: card.InstanceId);
        }

        public static void PrepareGargoyleSunderTarget(
            BattleState state,
            CombatantState target,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || target == null || card == null)
                return;

            if (card.DefinitionId != GargoyleSunderCardId || target.Block <= 0)
                return;

            events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{target.DisplayName} 护甲被移除")
            {
                CombatantId = target.Id,
                Amount = target.Block
            });
            target.Block = 0;
        }

        public static int ResolveFinalBindPoisonStacks(BattleState state, CombatantState target, int defaultStacks)
        {
            if (state == null || target == null)
                return defaultStacks;

            var hasPoison = StatusRules.GetStatusStacks(target, StatusCatalog.Poison) > 0;
            var hasSlow = StatusRules.GetStatusStacks(target, StatusCatalog.Slow) > 0;
            return hasPoison && hasSlow ? FinalBindBonusPoisonStacks : defaultStacks;
        }

        // ===== v0.9 玩家被动卡牌触发钩子 =====

        /// <summary>应对姿态：应对成功时获得8护甲。在 RespondEffectExecutor 应对成功后调用。</summary>
        public static void TryTriggerRespondStanceOnRespondSuccess(
            BattleState state, CombatantState actor, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null || actor == null || !actor.IsAlive || events == null)
                return;
            if (!StatusRules.HasStatus(actor, StatusCatalog.RespondStance))
                return;
            DamageRules.ApplyBlock(actor, RespondStanceBlock, events, state, rng);
        }

        /// <summary>战意觉醒：受到HP伤害后获得5%增伤（永久）。在 DamageRules 受伤后调用。</summary>
        public static void OnDamageTakenBattleWill(
            BattleState state, CombatantState target, int hpDamage, List<BattleEvent> events)
        {
            if (state == null || target == null || hpDamage <= 0 || events == null)
                return;
            if (!StatusRules.HasStatus(target, StatusCatalog.BattleWill))
                return;
            StatusRules.ApplyStatus(
                state, target, StatusCatalog.AttackUpPercent,
                BattleWillAttackPercentPerHit, -1, events);
        }

        /// <summary>重甲强化：获得护甲时额外+20%。返回放大后的护甲值。</summary>
        public static int ApplyHeavyArmorBlockBonus(CombatantState actor, int amount)
        {
            if (actor == null || amount <= 0)
                return amount;
            if (!StatusRules.HasStatus(actor, StatusCatalog.HeavyArmor))
                return amount;
            return Math.Max(1, (int)Math.Round(amount * (100f + HeavyArmorBlockBonusPercent) / 100f));
        }

        /// <summary>最终壁垒：回合末护甲清零时仅清除 (100-FinalBulwarkKeepPercent)%。返回应保留的护甲。</summary>
        public static int GetFinalBulwarkRetainedBlock(CombatantState combatant)
        {
            if (combatant == null || combatant.Block <= 0)
                return 0;
            if (!StatusRules.HasStatus(combatant, StatusCatalog.FinalBulwark))
                return 0;
            return (int)Math.Round(combatant.Block * FinalBulwarkKeepPercent / 100f);
        }

        /// <summary>背水一战：HP将降至0时，改为保留1HP（消耗1层持续时间）。返回是否触发。</summary>
        public static bool TryTriggerLastStand(
            BattleState state, CombatantState target, ref int hpDamage, List<BattleEvent> events)
        {
            if (state == null || target == null || hpDamage <= 0 || events == null)
                return false;
            if (!StatusRules.HasStatus(target, StatusCatalog.LastStand))
                return false;
            if (target.Hp - hpDamage > 0)
                return false;

            hpDamage = target.Hp - 1;
            if (hpDamage < 0)
                hpDamage = 0;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{target.DisplayName} 背水一战：HP 保留 1")
            {
                CombatantId = target.Id,
                Amount = 1
            });
            return true;
        }

        /// <summary>腐朽化身：回合开始时给所有敌人施加2层中毒（永久）。</summary>
        public static void TryTriggerRotAvatarOnTurnStart(
            BattleState state, List<BattleEvent> events)
        {
            if (state == null || events == null)
                return;
            var caster = FindAliveWithStatus(state, TeamSide.Player, StatusCatalog.RotAvatar);
            if (caster == null)
                return;
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;
                StatusRules.ApplyStatus(
                    state, enemy, StatusCatalog.Poison, RotAvatarPoisonStacks, -1, events);
            }
        }

        /// <summary>鲜血狂欢：献祭后获得5%增伤（永久）。在献祭自伤结算后调用。</summary>
        public static void TryTriggerBloodFrenzyOnSacrifice(
            BattleState state, CombatantState actor, List<BattleEvent> events)
        {
            if (state == null || actor == null || events == null)
                return;
            if (!StatusRules.HasStatus(actor, StatusCatalog.BloodFrenzy))
                return;
            StatusRules.ApplyStatus(
                state, actor, StatusCatalog.AttackUpPercent,
                BloodFrenzyAttackPercent, -1, events);
        }

        /// <summary>分血仪式：恶魔回复HP时，治疗其他我方角色30%的回复量。</summary>
        public static void TryTriggerBloodSharingOnHeal(
            BattleState state, CombatantState healed, int healedAmount, List<BattleEvent> events)
        {
            if (state == null || healed == null || healedAmount <= 0 || events == null)
                return;
            if (!StatusRules.HasStatus(healed, StatusCatalog.BloodSharing))
                return;
            var share = Math.Max(1, (int)Math.Round(healedAmount * BloodSharingAllyHealPercent / 100f));
            foreach (var ally in state.GetTeam(healed.Team))
            {
                if (!ally.IsAlive || ally.Id == healed.Id)
                    continue;
                DamageRules.ApplyHeal(state, ally, share, events, healed);
            }
        }

        /// <summary>瘟疫蔓延：敌人因中毒受伤时，30%概率将一半层数传染给相邻敌人。</summary>
        public static void TryTriggerPlagueSpreadOnPoisonTick(
            BattleState state, CombatantState victim, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null || victim == null || victim.Team != TeamSide.Enemy || rng == null)
                return;
            var caster = FindAliveWithStatus(state, TeamSide.Player, StatusCatalog.PlagueSpread);
            if (caster == null)
                return;
            if (rng.NextInt(1, 100) > PlagueSpreadChancePercent)
                return;
            var poison = StatusRules.FindStatus(victim, StatusCatalog.Poison);
            if (poison == null || poison.Stacks <= 0)
                return;
            var spreadStacks = Math.Max(1, poison.Stacks / 2);
            var duration = poison.RemainingTurns;
            var adjacent = CollectAdjacentAliveEnemies(state, victim);
            if (adjacent.Count == 0)
                return;
            var spreadTarget = adjacent[rng.NextIndex(adjacent.Count)];
            StatusRules.ApplyStatus(state, spreadTarget, StatusCatalog.Poison, spreadStacks, duration, events);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"瘟疫蔓延：传染 {spreadStacks} 层中毒")
            {
                CombatantId = spreadTarget.Id,
                Amount = spreadStacks,
                TargetId = StatusCatalog.Poison
            });
        }

        static List<CombatantState> CollectAdjacentAliveEnemies(BattleState state, CombatantState victim)
        {
            var result = new List<CombatantState>();
            if (state == null || victim == null || !victim.IsAlive)
                return result;

            var alive = PositionRules.GetAliveSortedByPhysicalSlot(state, TeamSide.Enemy);
            var rank = PositionRules.GetEffectiveRank(state, victim);
            if (rank > 0)
                result.Add(alive[rank - 1]);
            if (rank >= 0 && rank + 1 < alive.Count)
                result.Add(alive[rank + 1]);
            return result;
        }

        /// <summary>神圣灌注：解析应重复的规划队列上一张牌（穿透灌注链）。</summary>
        public static bool TryGetHolyInfusionRepeatTarget(
            BattleState state,
            int holyInfusionInstanceId,
            out int repeatCardInstanceId)
        {
            repeatCardInstanceId = 0;
            if (state == null || holyInfusionInstanceId <= 0)
                return false;

            return TryGetHolyInfusionRepeatTargetFromQueue(
                state, state.PlayerPlan.PlayQueue, holyInfusionInstanceId, out repeatCardInstanceId);
        }

        /// <summary>规划草稿队列版本：用于取消选择时判断灌注是否仍有效。</summary>
        public static bool TryGetHolyInfusionRepeatTargetFromQueue(
            BattleState state,
            IList<int> playQueue,
            int holyInfusionInstanceId,
            out int repeatCardInstanceId)
        {
            repeatCardInstanceId = 0;
            if (state == null || playQueue == null || holyInfusionInstanceId <= 0)
                return false;

            var idx = IndexOfQueue(playQueue, holyInfusionInstanceId);
            if (idx <= 0)
                return false;

            repeatCardInstanceId = ResolveHolyInfusionRepeatTargetFromQueue(
                state, playQueue, playQueue[idx - 1]);
            return repeatCardInstanceId > 0;
        }

        static int ResolveHolyInfusionRepeatTarget(BattleState state, int cardInstanceId) =>
            ResolveHolyInfusionRepeatTargetFromQueue(state, state.PlayerPlan.PlayQueue, cardInstanceId);

        static int ResolveHolyInfusionRepeatTargetFromQueue(
            BattleState state,
            IList<int> playQueue,
            int cardInstanceId)
        {
            var card = state.GetCard(cardInstanceId);
            if (card == null)
                return 0;

            if (card.DefinitionId == HolyInfusionCardId)
            {
                var idx = IndexOfQueue(playQueue, cardInstanceId);
                if (idx <= 0)
                    return 0;
                return ResolveHolyInfusionRepeatTargetFromQueue(state, playQueue, playQueue[idx - 1]);
            }

            var ownerId = PositionRules.GetOwnerCombatantId(state, card);
            var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
            if (owner == null || owner.Team != TeamSide.Player || !owner.IsAlive)
                return 0;

            return cardInstanceId;
        }

        static int IndexOfQueue(IList<int> queue, int value)
        {
            for (var i = 0; i < queue.Count; i++)
            {
                if (queue[i] == value)
                    return i;
            }

            return -1;
        }

        public static bool CanSelectHolyInfusion(Planning.PlanningDraft draft) =>
            draft != null && draft.SelectedQueue.Count > 0;

        public static int GetHolyInfusionPlayCost(BattleState state, Planning.PlanningDraft draft)
        {
            // 不可选时返回极大费用（供 CanAfford）；禁止用于退费回算
            if (state == null || draft == null || draft.SelectedQueue.Count == 0)
                return 999;

            var prev = state.GetCard(draft.SelectedQueue[draft.SelectedQueue.Count - 1]);
            if (prev == null)
                return 999;

            var prevOwnerId = PositionRules.GetOwnerCombatantId(state, prev);
            var prevOwner = prevOwnerId != null ? state.GetCombatant(prevOwnerId) : null;
            var baseCost = TalentBattleRules.GetEffectivePlayCost(state, prevOwner, prev);
            return Math.Max(0, baseCost + 1);
        }

        static CombatantState FindAliveWithStatus(BattleState state, TeamSide team, string statusId)
        {
            foreach (var c in state.GetTeam(team))
            {
                if (c.IsAlive && StatusRules.HasStatus(c, statusId))
                    return c;
            }
            return null;
        }

        static CombatantState PickRandomAliveEnemy(BattleState state, BattleRng rng)
        {
            if (state == null || rng == null)
                return null;

            var pool = new List<CombatantState>();
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (enemy.IsAlive)
                    pool.Add(enemy);
            }

            if (pool.Count == 0)
                return null;

            return pool[rng.NextIndex(pool.Count)];
        }
    }
}
