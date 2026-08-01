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

            SyncAllSpiderPoisonVulnerability(state, events);

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

                RefreshSeahorseWaveSurge(state, combatant, events);
                RefreshPhantomCaptainFrenzy(state, combatant, events);
            }

            state.EnemyAttackCardsPlayedThisTurn = 0;
        }

        /// <summary>
        /// 鬼灵海盗船长被动：回合开始检测敌方是否有 HP&lt;25% 或死亡；
        /// 有则挂 33% 增伤 + 20% 易伤（固定层数，不多次叠加）。
        /// </summary>
        static void RefreshPhantomCaptainFrenzy(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || !combatant.IsAlive
                || !HasTrait(combatant, MinionTraitCatalog.PhantomCaptainFrenzy))
                return;

            var active = HasLowHpOrDeadPlayer(state);
            SyncPhantomCaptainFrenzyStatus(
                state, combatant, StatusCatalog.PhantomCaptainFrenzyAtk,
                active ? MinionTraitCatalog.PhantomCaptainFrenzyAttackPercent : 0, events);
            SyncPhantomCaptainFrenzyStatus(
                state, combatant, StatusCatalog.PhantomCaptainFrenzyVuln,
                active ? MinionTraitCatalog.PhantomCaptainFrenzyDefensePercent : 0, events);
        }

        static void SyncPhantomCaptainFrenzyStatus(
            BattleState state,
            CombatantState combatant,
            string statusId,
            int desiredStacks,
            List<BattleEvent> events)
        {
            var current = StatusRules.GetStatusStacks(combatant, statusId);
            if (desiredStacks <= 0)
            {
                if (current > 0)
                    StatusRules.RemoveStatus(combatant, statusId, current, events);
                return;
            }

            if (current == desiredStacks)
                return;

            if (current > 0)
                StatusRules.RemoveStatus(combatant, statusId, current, events);

            StatusRules.ApplyStatus(state, combatant, statusId, desiredStacks, -1, events);
        }

        /// <summary>
        /// 踏潮守卫被动：回合开始检测同位置对手速度差，挂「浪潮」增伤 buff（层数=增伤%）。
        /// </summary>
        static void RefreshSeahorseWaveSurge(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || !combatant.IsAlive
                || !HasTrait(combatant, MinionTraitCatalog.SeahorseGuardSpeedAttack))
                return;

            var bonusPercent = ComputeSeahorseWaveSurgePercent(state, combatant);
            var current = StatusRules.GetStatusStacks(combatant, StatusCatalog.WaveSurge);
            if (bonusPercent <= 0)
            {
                if (current > 0)
                    StatusRules.RemoveStatus(combatant, StatusCatalog.WaveSurge, current, events);
                return;
            }

            if (current == bonusPercent)
                return;

            if (current > 0)
                StatusRules.RemoveStatus(combatant, StatusCatalog.WaveSurge, current, events);

            StatusRules.ApplyStatus(
                state, combatant, StatusCatalog.WaveSurge, bonusPercent, -1, events);
        }

        static int ComputeSeahorseWaveSurgePercent(BattleState state, CombatantState actor)
        {
            var sameSlotEnemy = FindAliveOpposingInSlot(state, actor);
            if (sameSlotEnemy == null)
                return 50;

            var actorSpeed = StatusRules.GetEffectiveSpeed(state, actor);
            var enemySpeed = StatusRules.GetEffectiveSpeed(state, sameSlotEnemy);
            return System.Math.Min(50, System.Math.Max(0, actorSpeed - enemySpeed) * 10);
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

            // AttackUpPercent / DefenseUpPercent 在回合末扣持续；挂 1 回合 = 本回合内有效
            var applyDuration = MinionTraitCatalog.GargoyleTraitDurationTurns;
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

            if (HasTrait(combatant, MinionTraitCatalog.SpiderLadyPoisonVulnerability))
                SyncAllSpiderPoisonVulnerability(state, events);

            var isRat = combatant.CharacterDefinitionId == MinionTraitCatalog.RatCharacterId;
            var spawnSwarm = StatusRules.HasStatus(combatant, StatusCatalog.RatSwarmCall);

            // 先计入死亡数，再召唤克隆，克隆才能带上最新鼠群狂怒
            if (isRat)
                state.RatDeathsThisBattle++;

            if (spawnSwarm)
                SummonRules.SpawnRatSwarmClone(state, combatant, events);

            if (!isRat)
                return;

            RefreshRatPackAttackBonuses(state, events);
        }

        /// <summary>按本场鼠人死亡数刷新所有存活鼠人的永久增伤。</summary>
        public static void RefreshRatPackAttackBonuses(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var bonus = state.RatDeathsThisBattle * MinionTraitCatalog.RatPackAttackBonusPercentPerDeath;
            foreach (var unit in state.Combatants)
            {
                if (unit == null || !unit.IsAlive)
                    continue;
                if (unit.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId
                    && !HasTrait(unit, MinionTraitCatalog.RatPackAttackOnAllyDeath))
                    continue;

                if (unit.RatPackAttackBonusPercent == bonus)
                    continue;

                unit.RatPackAttackBonusPercent = bonus;
                RelicBattleRules.RefreshDerivedStats(state, unit, state.Config?.RunModifiers);
                events?.Add(new BattleEvent(BattleEventKind.StatusApplied,
                    $"{unit.DisplayName} 鼠群狂怒 +{bonus}% 攻击（本场死亡 {state.RatDeathsThisBattle}）")
                {
                    CombatantId = unit.Id,
                    Amount = bonus
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
            if (state == null || target == null || stacks <= 0 || string.IsNullOrEmpty(statusId))
                return;

            if (!HasTrait(target, MinionTraitCatalog.ChainWraithDebuffShare))
                return;

            var def = StatusCatalog.Get(statusId);
            if (!StatusRules.IsDebuffDefinition(def))
                return;

            // 镜像到敌对全体，持续时间固定 2 回合（与自身原持续无关）
            var mirrorDuration = MinionTraitCatalog.ChainWraithMirrorDebuffDurationTurns;
            var mirrorTeam = target.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            foreach (var unit in state.GetTeam(mirrorTeam))
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                StatusRules.ApplyStatusInternal(
                    state, unit, statusId, stacks, mirrorDuration, events, mirrorChainWraith: false);
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

            if (!HasTrait(source, MinionTraitCatalog.ChainWraithDebuffShare))
                return;

            var def = StatusCatalog.Get(statusId);
            if (!StatusRules.IsDebuffDefinition(def))
                return;

            var mirrorTeam = source.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            foreach (var unit in state.GetTeam(mirrorTeam))
            {
                if (unit == null || !unit.IsAlive)
                    continue;

                StatusRules.RemoveAllStatus(unit, statusId, events);
            }
        }

        /// <summary>
        /// 伤害已由 <see cref="StatusCatalog.SpiderPoisonVulnerable"/> 经 CombatModifierRules 结算，
        /// 此处保留入口避免旧调用双重乘伤。
        /// </summary>
        public static int ApplySpiderPoisonVulnerability(BattleState state, CombatantState recipient, int hpDamage) =>
            hpDamage;

        /// <summary>
        /// 场上有蜘蛛贵妇时，按玩家中毒层数同步可见易伤：每 5 层中毒 = 10 层易伤（+10% 受伤）。
        /// </summary>
        public static void SyncSpiderPoisonVulnerability(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (target == null || target.Team != TeamSide.Player)
                return;

            var poisonStacks = StatusRules.GetStatusStacks(target, StatusCatalog.Poison);
            var desired = 0;
            if (target.IsAlive)
            {
                if (state != null)
                {
                    if (HasAliveSpiderLady(state))
                        desired = poisonStacks / 5 * MinionTraitCatalog.SpiderPoisonVulnPercentPerFiveStacks;
                }
                else
                {
                    // 无 BattleState 时按中毒推算；蜘蛛死亡/回合初会再校正
                    desired = poisonStacks / 5 * MinionTraitCatalog.SpiderPoisonVulnPercentPerFiveStacks;
                }
            }

            var current = StatusRules.GetStatusStacks(target, StatusCatalog.SpiderPoisonVulnerable);
            if (desired == current)
                return;

            if (desired <= 0)
            {
                StatusRules.RemoveAllStatus(target, StatusCatalog.SpiderPoisonVulnerable, events);
                CombatantRules.RefreshDerivedStats(target);
                RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
                return;
            }

            var existing = StatusRules.FindStatus(target, StatusCatalog.SpiderPoisonVulnerable);
            if (existing == null)
            {
                StatusRules.ApplyStatusInternal(
                    state, target, StatusCatalog.SpiderPoisonVulnerable, desired, -1, events,
                    mirrorChainWraith: false);
                return;
            }

            existing.Stacks = desired;
            existing.RemainingTurns = -1;
            events?.Add(new BattleEvent(BattleEventKind.StatusApplied, "易伤")
            {
                CombatantId = target.Id,
                Amount = desired,
                TargetId = StatusCatalog.SpiderPoisonVulnerable
            });
            CombatantRules.RefreshDerivedStats(target);
            RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
        }

        public static void SyncAllSpiderPoisonVulnerability(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var unit in state.GetTeam(TeamSide.Player))
                SyncSpiderPoisonVulnerability(state, unit, events);
        }

        static bool HasAliveSpiderLady(BattleState state)
        {
            if (state == null)
                return false;

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

            if (HasTrait(actor, MinionTraitCatalog.MermaidZeroCostAttack)
                && GetAdjustedCardCost(state, actor, card) == 0)
            {
                actor.MermaidZeroCostAttackBonusPercent = System.Math.Min(
                    100, actor.MermaidZeroCostAttackBonusPercent + 5);
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
            }

            // 骷髅 / 精英：跨回合累计出牌数，每满 3 的倍数触发一次
            if (actor.CardsResolvedCount <= 0
                || actor.CardsResolvedCount % MinionTraitCatalog.CardsPerStatBonus != 0)
                return;

            if (HasTrait(actor, MinionTraitCatalog.SkeletonCardDef))
            {
                DamageRules.ApplyBlock(
                    actor, MinionTraitCatalog.SkeletonArmorPerThreshold, events, state);
            }

            if (HasTrait(actor, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                DamageRules.ApplyBlock(
                    actor, MinionTraitCatalog.SkeletonEliteArmorPerThreshold, events, state);
                StatusRules.ApplyStatus(
                    state,
                    actor,
                    StatusCatalog.AttackUpPercent,
                    MinionTraitCatalog.SkeletonEliteAttackPercentPerThreshold,
                    -1,
                    events);
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
            // 踏潮「浪潮」/船长狂怒增伤由回合开始挂的状态经 CombatModifierRules 结算

            // 人鱼零费增伤已计入 OutgoingDamagePercentBonus，此处不再二次乘算
            return power;
        }

        /// <summary>敌人出牌费用（含潮汐之力对劈砍/破浪斩 -1）。</summary>
        public static int GetAdjustedCardCost(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            if (card == null)
                return 0;

            var cost = card.Cost;
            if (owner != null
                && (card.DefinitionId == AbyssMonsterCardCatalog.MermaidSlashCardId
                    || card.DefinitionId == AbyssMonsterCardCatalog.WaveCleaveCardId))
            {
                var cut = StatusRules.GetStatusStacks(owner, StatusCatalog.MermaidTidalCostCut);
                if (cut > 0)
                    cost = System.Math.Max(0, cost - cut);
            }

            return cost;
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

        /// <summary>
        /// 水母海巫被动：每当敌人（玩家方）换位时，自身 +10 最大 HP。
        /// </summary>
        public static void OnPositionsSwapped(
            BattleState state,
            CombatantState a,
            CombatantState b,
            List<BattleEvent> events)
        {
            if (state == null || a == null || b == null)
                return;

            // 至少一方是玩家阵营（对海巫而言的敌人）才触发
            if (a.Team != TeamSide.Player && b.Team != TeamSide.Player)
                return;

            foreach (var unit in state.GetTeam(TeamSide.Enemy))
            {
                if (unit == null || !unit.IsAlive)
                    continue;
                if (!HasTrait(unit, MinionTraitCatalog.JellyfishCasterSwapMaxHp))
                    continue;

                var bonus = MinionTraitCatalog.JellyfishCasterSwapMaxHpBonus;
                unit.MaxHp += bonus;
                unit.Hp = System.Math.Min(unit.MaxHp, unit.Hp + bonus);
                RelicBattleRules.RefreshDerivedStats(state, unit, state.Config?.RunModifiers);
                events?.Add(new BattleEvent(BattleEventKind.StatusApplied,
                    $"{unit.DisplayName} 换位共鸣 +{bonus} 最大生命")
                {
                    CombatantId = unit.Id,
                    Amount = bonus
                });
            }
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

        static CombatantState FindAliveOpposingInSlot(BattleState state, CombatantState actor)
        {
            if (state == null || actor == null)
                return null;

            var opposing = actor.Team == TeamSide.Enemy ? TeamSide.Player : TeamSide.Enemy;
            foreach (var unit in state.Combatants)
            {
                if (unit != null && unit.IsAlive && unit.Team == opposing && unit.Slot == actor.Slot)
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
            if (target == null || !target.FirstHitDodgePending)
                return false;

            if (!HasTrait(target, MinionTraitCatalog.BatFirstHitDodge))
                return false;

            // 首次受击即消耗（成败皆清）；无 rng 时仍消耗但不闪避
            target.FirstHitDodgePending = false;
            if (rng == null)
                return false;

            // 严格 50%：0..99 中 <50 成功
            var roll = (int)(rng.NextUInt() % 100u);
            if (roll >= MinionTraitCatalog.BatFirstHitDodgeChancePercent)
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
