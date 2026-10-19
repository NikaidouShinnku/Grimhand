using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class AchievementBattleTests
    {
        static CombatantState Unit(string id, TeamSide team = TeamSide.Enemy, FormationSlot slot = FormationSlot.Front) =>
            new() { Id = id, CharacterDefinitionId = team == TeamSide.Player ? "char_knight" : "char_skeleton",
                DisplayName = id, Team = team, Slot = slot, Hp = 1, MaxHp = 1 };

        [Test]
        public void ResponsesRequireDifferentAttackInstancesInSameTurn()
        {
            var s = new BattleState();
            var p = Unit("player", TeamSide.Player);
            var t = s.AchievementTelemetry;
            var card = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            for (var i = 0; i < 3; i++) t.Responded(s, p, card); // AOE/multiple response effects.
            Assert.That(t.ThreeResponsesInTurn, Is.False);
            card.InstanceId = 2; t.Responded(s, p, card);
            s.TurnNumber++;
            card.InstanceId = 3; t.Responded(s, p, card);
            Assert.That(t.ThreeResponsesInTurn, Is.False);
            card.InstanceId = 4; t.Responded(s, p, card);
            card.InstanceId = 5; t.Responded(s, p, card);
            Assert.That(t.ThreeResponsesInTurn, Is.True);
            s.TurnNumber++; t.Responded(s, p, card);
            Assert.That(t.ThreeResponsesInTurn, Is.True);
        }

        [Test]
        public void RespondedPlayersExcludeSummonsAndMinionsAndDeduplicate()
        {
            var s = new BattleState();
            var p = Unit("p", TeamSide.Player);
            var attack = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            s.AchievementTelemetry.Responded(s, p, attack);
            s.AchievementTelemetry.Responded(s, p, attack);
            s.AchievementTelemetry.Responded(s, Unit("summon_x", TeamSide.Player), null);
            p = Unit("minion", TeamSide.Player); p.Traits.Add("minion_slime_regen");
            p.CharacterDefinitionId = "char_skeleton";
            s.AchievementTelemetry.Responded(s, p, null);
            Assert.That(s.AchievementTelemetry.RespondedPlayerIds, Is.EquivalentTo(new[] { "p" }));
        }

        [Test]
        public void ResponsesRejectStatusDefenseBorrowedAndTemporaryNpcs()
        {
            var s = new BattleState(); var p = Unit("p", TeamSide.Player);
            s.Config.Combatants.Add(new CombatantConfig { Id = p.Id, Team = TeamSide.Player, CharacterDefinitionId = p.CharacterDefinitionId });
            s.Config.Combatants.Add(new CombatantConfig { Id = "borrowed", Team = TeamSide.Enemy, CharacterDefinitionId = "char_knight" });
            var card = new CardInstanceState { InstanceId = 1, CardType = CardType.Status };
            s.AchievementTelemetry.Responded(s, p, card);
            card.CardType = CardType.Defense;
            s.AchievementTelemetry.Responded(s, p, card);
            card.CardType = CardType.Attack;
            s.AchievementTelemetry.Responded(s, Unit("borrowed", TeamSide.Player), card);
            s.AchievementTelemetry.Responded(s, Unit("temporary", TeamSide.Player), card);
            p.Id = null; s.AchievementTelemetry.Responded(s, p, card);
            Assert.That(s.AchievementTelemetry.RespondedPlayerIds, Is.Empty);
            Assert.That(s.AchievementTelemetry.RespondedEnemyAttackIds, Is.Empty);
        }

        [Test]
        public void SnapshotTelemetryAssignmentDeepCopiesCollectionsAndActiveAttack()
        {
            var original = new BattleState(); var p = Unit("p", TeamSide.Player);
            var card = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            var t = original.AchievementTelemetry;
            t.Responded(original, p, card); t.Retained(original, card);
            t.BeginCard(p, card); t.DirectKill(p, Unit("first"), 1);
            var preview = new BattleState { AchievementTelemetry = t };
            var copy = preview.AchievementTelemetry;
            Assert.That(copy, Is.Not.SameAs(t));
            copy.RespondedPlayerIds.Clear(); copy.RespondedEnemyAttackIds.Clear();
            copy.RetainedTurnEnds[1] = 3; copy.LastRetainedTurn.Clear();
            copy.DirectKill(p, Unit("second"), 1); copy.DirectKill(p, Unit("third"), 1);
            DamageRules.ApplyPeriodicDamage(preview, Unit("previewDeath"), 100, new List<BattleEvent>(), "poison", StatusCatalog.Poison);
            Assert.That(copy.ThreeKillsOneAttack, Is.True);
            Assert.That(t.ThreeKillsOneAttack, Is.False);
            Assert.That(t.PoisonKills, Is.Zero);
            Assert.That(t.DeadEnemyIds, Is.Empty);
            Assert.That(t.KilledPositions, Is.Empty);
            Assert.That(t.RespondedPlayerIds, Does.Contain("p"));
            Assert.That(t.RespondedEnemyAttackIds, Does.Contain(1));
            Assert.That(t.RetainedTurnEnds[1], Is.EqualTo(1));
            Assert.That(t.LastRetainedTurn[1], Is.EqualTo(1));
        }

        [Test]
        public void CardPreviewDoesNotEarnOrResetTelemetry()
        {
            var s = new BattleState(); var p = Unit("p", TeamSide.Player);
            var c = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            var action = new EffectActionSpec { Type = EffectActionType.DealDamage, Value = 100 };
            s.AchievementTelemetry.BeginCard(p, c);
            s.AchievementTelemetry.DirectKill(p, Unit("one"), 1);
            CardPreviewRules.ComputeExpectedDamage(s, p, c, action);
            Assert.That(s.AchievementTelemetry.ThreeKillsOneAttack, Is.False);
            s.AchievementTelemetry.DirectKill(p, Unit("two"), 1);
            s.AchievementTelemetry.DirectKill(p, Unit("three"), 1);
            Assert.That(s.AchievementTelemetry.ThreeKillsOneAttack, Is.True);
        }

        [Test]
        public void SpectralAttackIsIndependentAndUsedEndsItsAggregation()
        {
            var s = new BattleState(); var p = Unit("p", TeamSide.Player);
            var c = new CardInstanceState { InstanceId = 2, SpectralRepeatSourceId = 1, CardType = CardType.Attack };
            var t = s.AchievementTelemetry;
            t.BeginCard(p, c);
            t.DirectKill(p, Unit("a"), 2); t.DirectKill(p, Unit("b"), 2);
            t.UsedCard(p, c);
            t.DirectKill(p, Unit("later"), 2);
            Assert.That(t.ThreeKillsOneAttack, Is.False);
            t.BeginCard(p, c);
            foreach (var id in new[] { "c", "d", "e" }) t.DirectKill(p, Unit(id), 2);
            Assert.That(t.ThreeKillsOneAttack, Is.True);
        }

        [Test]
        public void TurnStartPoisonCountsPoisonKillsAndNeverAttackKills()
        {
            var s = new BattleState(); var events = new List<BattleEvent>();
            var p = Unit("p", TeamSide.Player);
            s.AchievementTelemetry.BeginCard(p, new CardInstanceState { InstanceId = 1, CardType = CardType.Attack });
            for (var i = 0; i < 3; i++)
            {
                var e = Unit("e" + i); s.Combatants.Add(e);
                StatusRules.ApplyStatus(s, e, StatusCatalog.Poison, 10, 3, events);
            }
            StatusRules.ProcessTurnStartStatuses(s, events);
            Assert.That(s.AchievementTelemetry.PoisonKills, Is.EqualTo(3));
            Assert.That(s.AchievementTelemetry.ThreePoisonKills, Is.True);
            Assert.That(s.AchievementTelemetry.ThreeKillsOneAttack, Is.False);
        }

        [Test]
        public void ResolverRecordsFirstAoeResponseAndOnlySuccessfulResponses()
        {
            var s = new BattleState(); var events = new List<BattleEvent>();
            var p = Unit("p", TeamSide.Player); var e = Unit("e");
            s.Combatants.Add(p); s.Combatants.Add(e);
            var attack = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            attack.Actions.Add(new EffectActionSpec { Type = EffectActionType.DealDamage, Target = EffectTarget.AllEnemies, Value = 1 });
            s.CardsById.Add(1, attack);
            var response = new CardInstanceState { InstanceId = 2, CardType = CardType.Defense };
            response.Actions.Add(new EffectActionSpec { Type = EffectActionType.GainBlockFromLastDamagePercent,
                Condition = ReactionConditionType.LastActionAttackOnSelf, Value = 50 });
            var context = new RespondTriggerContext(e.Id, 1);
            RespondEffectExecutor.Execute(s, p, response, context, events, new BattleRng(1));
            Assert.That(s.AchievementTelemetry.RespondedPlayerIds, Does.Contain(p.Id));
            Assert.That(s.AchievementTelemetry.RespondedEnemyAttackIds.Count, Is.EqualTo(1));
            attack.CardType = CardType.Status;
            RespondEffectExecutor.Execute(s, p, response, context, events, new BattleRng(1));
            Assert.That(s.AchievementTelemetry.RespondedEnemyAttackIds.Count, Is.EqualTo(1));
            Assert.That(s.AchievementTelemetry.ThreeResponsesInTurn, Is.False);
        }

        [Test]
        public void QuickStartMultihitAttackUsesEngineHooksAndDoesNotCountAsConsumable()
        {
            var config = new BattleConfig();
            config.Combatants.Add(new CombatantConfig { Id = "p", CharacterDefinitionId = "char_knight",
                Team = TeamSide.Player, MaxHp = 20, Slot = FormationSlot.Front });
            for (var i = 1; i <= 3; i++)
                config.Combatants.Add(new CombatantConfig { Id = "e" + i, Team = TeamSide.Enemy,
                    MaxHp = 2, Slot = (FormationSlot)i });
            var engine = new BattleEngine(config); var s = engine.State;
            s.Phase = TurnPhase.Planning; s.EnergyCurrent = 10;
            var card = new CardInstanceState { InstanceId = 100, OwnerCombatantId = "p",
                OwnerCharacterId = "char_knight", CardType = CardType.Attack };
            card.Keywords.Add("quick_start");
            card.Actions.Add(new EffectActionSpec { Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies, Value = 2, HitCount = 2, Reach = TargetReach.Any });
            s.CardsById.Add(card.InstanceId, card); s.PlayerHand.Add(card);
            s.AchievementTelemetry.RetainedTurnEnds[card.InstanceId] = 3;
            Assert.That(engine.TryBeginConsumableUse(ConsumableIds.SmallHealingPotion, 0), Is.True, "target potion");
            engine.CancelConsumableTargeting();
            Assert.That(engine.TryResolveQuickStartCard(card.InstanceId), Is.True, "quick start");
            Assert.That(s.AchievementTelemetry.ThreeKillsOneAttack, Is.True, "multi kill");
            Assert.That(s.AchievementTelemetry.RetainedThenUsed, Is.True, "retained");
            Assert.That(s.AchievementTelemetry.ConsumablesUsed, Is.Zero);
        }

        [Test]
        public void ScheduledRepeatedAndRespondResolutionAllBeginUseAndEndAggregation()
        {
            foreach (var methodName in new[] { "ResolveStep", "ResolveRepeatedCardPlay", "ResolveRespondStep" })
            {
                var config = new BattleConfig();
                config.Combatants.Add(new CombatantConfig { Id = "p", Team = TeamSide.Player,
                    CharacterDefinitionId = "char_knight", MaxHp = 20, Slot = FormationSlot.Front });
                for (var i = 1; i <= 3; i++) config.Combatants.Add(new CombatantConfig
                    { Id = "e" + i, Team = TeamSide.Enemy, MaxHp = 1, Slot = (FormationSlot)i });
                var engine = new BattleEngine(config); var s = engine.State;
                var c = new CardInstanceState { InstanceId = 100, OwnerCombatantId = "p",
                    OwnerCharacterId = "char_knight", CardType = CardType.Attack };
                c.Actions.Add(new EffectActionSpec { Type = EffectActionType.DealDamage,
                    Target = EffectTarget.AllEnemies, Value = 10, Reach = TargetReach.Any });
                s.CardsById.Add(c.InstanceId, c); s.PlayerHand.Add(c);
                var t = s.AchievementTelemetry; t.RetainedTurnEnds[c.InstanceId] = 3;
                var step = new ResolutionStep("p", c.InstanceId, 0);
                object[] args = methodName == "ResolveRepeatedCardPlay" ? new object[] { c.InstanceId, 0 }
                    : methodName == "ResolveRespondStep" ? new object[] { new ScheduledResolution { Step = step, ApplyConditionalEffects = false } }
                    : new object[] { step };
                typeof(BattleEngine).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(engine, args);
                Assert.That(t.ThreeKillsOneAttack, Is.True, methodName);
                Assert.That(t.RetainedThenUsed, Is.True, methodName);
                t.ThreeKillsOneAttack = false;
                foreach (var id in new[] { "later1", "later2", "later3" }) t.DirectKill(s.GetCombatant("p"), Unit(id), c.InstanceId);
                Assert.That(t.ThreeKillsOneAttack, Is.False, methodName + " must end aggregation");
            }
        }

        [Test]
        public void ConsumablesCountSuccessfulApplicationOnly()
        {
            var s = new BattleState(); var events = new List<BattleEvent>();
            var potion = new ConsumableDefinition { TargetKind = ConsumableTargetKind.SingleAlly,
                EffectKind = ConsumableEffectKind.HealSingle, Value = 5 };
            Assert.That(ConsumableRules.TryApply(s, potion, "missing", events, new BattleRng(1), out _), Is.False);
            Assert.That(s.AchievementTelemetry.ConsumablesUsed, Is.Zero);
            var p = Unit("p", TeamSide.Player); s.Combatants.Add(p);
            Assert.That(ConsumableRules.TryApply(s, potion, p.Id, events, new BattleRng(1), out _), Is.True);
            Assert.That(s.AchievementTelemetry.ConsumablesUsed, Is.EqualTo(1));
        }

        [Test]
        public void PoisonKillsAndDeathStatusesUseDamageHooksAndIgnoreDuplicateDeath()
        {
            var s = new BattleState(); var events = new List<BattleEvent>();
            for (var i = 0; i < 3; i++)
            {
                var e = Unit("e" + i);
                StatusRules.ApplyStatus(s, e, StatusCatalog.Poison, 1, 3, events);
                StatusRules.ApplyStatus(s, e, StatusCatalog.Burn, 1, 3, events);
                DamageRules.ApplyPeriodicDamage(s, e, 100, events, "poison", StatusCatalog.Poison);
                CombatantDeathRules.OnCharacterDied(s, e, events);
            }
            Assert.That(s.AchievementTelemetry.PoisonKills, Is.EqualTo(3));
            Assert.That(s.AchievementTelemetry.ThreePoisonKills, Is.True);
            Assert.That(s.AchievementTelemetry.PoisonBurnKill, Is.True);
        }

        [Test]
        public void OneAttackAggregatesHitsButNotSeparatePlaysOrLaterStatusKills()
        {
            var s = new BattleState(); var p = Unit("p", TeamSide.Player);
            var c = new CardInstanceState { InstanceId = 1, CardType = CardType.Attack };
            var events = new List<BattleEvent>(); var t = s.AchievementTelemetry;
            t.BeginCard(p, c);
            DamageRules.ApplyTrueDamage(s, p, Unit("a"), 100, events, 1);
            DamageRules.ApplyTrueDamage(s, p, Unit("b"), 100, events, 1);
            t.UsedCard(p, c);
            DamageRules.ApplyPeriodicDamage(s, Unit("c"), 100, events, "poison", StatusCatalog.Poison);
            Assert.That(t.ThreeKillsOneAttack, Is.False);
            t.BeginCard(p, c);
            foreach (var id in new[] { "d", "e", "f" })
                DamageRules.ApplyTrueDamage(s, p, Unit(id), 100, events, 1);
            Assert.That(t.ThreeKillsOneAttack, Is.True);
        }

        [Test]
        public void PositionsAreAtDeathAndMustShareTurn()
        {
            var s = new BattleState(); var events = new List<BattleEvent>();
            DamageRules.ApplyPeriodicDamage(s, Unit("a"), 100, events, "burn");
            s.TurnNumber++;
            var e = Unit("b", slot: FormationSlot.Back);
            DamageRules.ApplyPeriodicDamage(s, e, 100, events, "burn");
            Assert.That(s.AchievementTelemetry.FrontBackKillsInTurn, Is.False);
            DamageRules.ApplyPeriodicDamage(s, Unit("c"), 100, events, "burn");
            Assert.That(s.AchievementTelemetry.FrontBackKillsInTurn, Is.True);
        }

        [Test]
        public void DiscardAndRedrawBreakRetentionAndStatesDoNotShareProgress()
        {
            var s = new BattleState(); var c = new CardInstanceState { InstanceId = 1 };
            c.Keywords.Add("inherit"); s.PlayerHand.Add(c);
            var events = new List<BattleEvent>();
            DeckRules.DiscardHandAtEndOfTurn(s, TeamSide.Player, events);
            s.TurnNumber++;
            DeckRules.DiscardHandAtEndOfTurn(s, TeamSide.Player, events);
            DeckRules.MovePlayedCardToDiscard(s, TeamSide.Player, c, events);
            s.PlayerHand.Add(c); s.TurnNumber++;
            DeckRules.DiscardHandAtEndOfTurn(s, TeamSide.Player, events);
            s.AchievementTelemetry.UsedCard(Unit("p", TeamSide.Player), c);
            Assert.That(s.AchievementTelemetry.RetainedThenUsed, Is.False);
            s.AchievementTelemetry.ConsumablesUsed++;
            Assert.That(new BattleState().AchievementTelemetry.ConsumablesUsed, Is.Zero);
        }

        [Test]
        public void SameInheritedInstanceMustSurviveThreeDistinctTurnEndsThenBeUsed()
        {
            var s = new BattleState(); var c = new CardInstanceState { InstanceId = 1 };
            c.Keywords.Add("inherit"); s.PlayerHand.Add(c);
            var events = new List<BattleEvent>();
            for (var turn = 1; turn <= 3; turn++)
            {
                s.TurnNumber = turn;
                DeckRules.DiscardHandAtEndOfTurn(s, TeamSide.Player, events);
                DeckRules.DiscardHandAtEndOfTurn(s, TeamSide.Player, events);
            }
            Assert.That(s.AchievementTelemetry.RetainedThenUsed, Is.False);
            var p = Unit("p", TeamSide.Player);
            s.AchievementTelemetry.UsedCard(p, new CardInstanceState { InstanceId = 2 });
            Assert.That(s.AchievementTelemetry.RetainedThenUsed, Is.False);
            s.AchievementTelemetry.UsedCard(p, c);
            Assert.That(s.AchievementTelemetry.RetainedThenUsed, Is.True);
        }
    }
}
