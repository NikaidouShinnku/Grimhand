using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class SummonRules
    {
        public const string ExplosiveSkullCharacterId = "char_explosive_skull";

        public static bool TrySummonExplosiveSkull(
            BattleState state,
            CombatantState summoner,
            List<BattleEvent> events)
        {
            if (state == null || summoner == null || !summoner.IsAlive)
                return false;

            if (!StatusRules.HasStatus(summoner, StatusCatalog.BoneWorkshop))
                return false;

            var slot = FindNextEnemySummonSlot(state, summoner.Team);
            if (slot == null)
                return false;

            if (!state.Config.SummonTemplates.TryGetValue(ExplosiveSkullCharacterId, out var template))
                return false;

            SpawnFromTemplate(state, template, slot.Value, events);
            return true;
        }

        public static void GrantSkullSelfDestructHands(BattleState state, List<BattleEvent> events)
        {
            if (state?.Config == null)
                return;

            if (!state.Config.SummonTemplates.TryGetValue(ExplosiveSkullCharacterId, out var template))
                return;

            CardTemplate explodeTemplate = null;
            foreach (var card in template.DeckTemplates)
            {
                if (card?.DefinitionId == CharacterTraitCatalog.SkullExplodeCardId)
                {
                    explodeTemplate = card;
                    break;
                }
            }

            if (explodeTemplate == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive || combatant.Team != TeamSide.Enemy)
                    continue;

                if (!BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.SkullSelfDestructHand))
                    continue;

                if (HandAlreadyHasBoundCard(state, combatant.Id, explodeTemplate.DefinitionId))
                    continue;

                var card = CreateBoundCard(state, explodeTemplate, combatant.Id);
                state.EnemyHand.Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDrawn, card.DisplayName)
                {
                    CardInstanceId = card.InstanceId,
                    CombatantId = combatant.Id
                });
            }
        }

        public static void SpawnFromTemplate(
            BattleState state,
            CombatantConfig template,
            FormationSlot slot,
            List<BattleEvent> events)
        {
            if (state == null || template == null)
                return;

            var id = $"summon_{template.CharacterDefinitionId}_{state.NextSummonInstanceId++}";
            var combatant = new CombatantState
            {
                Id = id,
                DisplayName = template.DisplayName,
                Team = template.Team,
                Slot = slot,
                CharacterDefinitionId = template.CharacterDefinitionId,
                Level = template.Level,
                Xp = template.Xp,
                MaxHp = template.MaxHp,
                BaseAttack = template.BaseAttack,
                BaseDefense = template.BaseDefense,
                Speed = template.Speed,
                Hp = template.MaxHp
            };

            combatant.Traits.AddRange(template.Traits);
            if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.BossFirstHitBlock))
                combatant.BossFirstHitBlockPending = true;

            state.Combatants.Add(combatant);
            RelicBattleRules.RefreshDerivedStats(state, combatant, state.Config?.RunModifiers);

            events.Add(new BattleEvent(BattleEventKind.CombatantSpawned, combatant.DisplayName)
            {
                CombatantId = combatant.Id,
                TargetId = slot.ToString()
            });
        }

        public static FormationSlot? FindNextEnemySummonSlot(BattleState state, TeamSide team) =>
            FindEmptyTeamSlot(state, team);

        public static FormationSlot? FindEmptyTeamSlot(BattleState state, TeamSide team)
        {
            var occupied = new HashSet<FormationSlot>();
            foreach (var unit in state.GetTeam(team))
            {
                if (unit.IsAlive)
                    occupied.Add(unit.Slot);
            }

            if (!occupied.Contains(FormationSlot.Front))
                return FormationSlot.Front;
            if (!occupied.Contains(FormationSlot.Middle))
                return FormationSlot.Middle;
            if (!occupied.Contains(FormationSlot.Back))
                return FormationSlot.Back;

            return null;
        }

        public static void SpawnRatSwarmClone(
            BattleState state,
            CombatantState dead,
            List<BattleEvent> events)
        {
            if (state == null || dead == null || dead.CharacterDefinitionId != MinionTraitCatalog.RatCharacterId)
                return;

            var maxHp = System.Math.Max(1, dead.MaxHp / 2);
            var id = $"summon_{MinionTraitCatalog.RatCharacterId}_{state.NextSummonInstanceId++}";
            var combatant = new CombatantState
            {
                Id = id,
                DisplayName = dead.DisplayName,
                Team = dead.Team,
                Slot = dead.Slot,
                CharacterDefinitionId = MinionTraitCatalog.RatCharacterId,
                Level = dead.Level,
                Xp = dead.Xp,
                MaxHp = maxHp,
                BaseAttack = dead.BaseAttack,
                BaseDefense = dead.BaseDefense,
                Speed = dead.Speed,
                Hp = maxHp
            };

            combatant.Traits.AddRange(dead.Traits);
            state.Combatants.Add(combatant);
            RelicBattleRules.RefreshDerivedStats(state, combatant, state.Config?.RunModifiers);

            events.Add(new BattleEvent(BattleEventKind.CombatantSpawned, $"{combatant.DisplayName}（鼠群呼唤）")
            {
                CombatantId = combatant.Id,
                TargetId = dead.Slot.ToString()
            });
        }

        public static void MergeSummonedSkillPoolIntoTeamDeck(
            BattleState state,
            CombatantConfig template,
            TeamSide team,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (state == null || template == null || template.SkillPoolCandidates.Count == 0)
                return;

            var deck = new List<CardTemplate>();
            EnemyDeckBuilder.ApplySkillPoolEntries(deck, template.SkillPoolCandidates);
            var drawPile = state.GetDrawPile(team);
            foreach (var cardTemplate in deck)
            {
                var instance = CreateDeckCardInstance(state, cardTemplate);
                drawPile.Add(instance);
            }

            DeckRules.ShuffleDrawPile(state, team, rng, events);
        }

        public static CardInstanceState CreateDeckCardInstance(BattleState state, CardTemplate template)
        {
            var id = state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = template.DefinitionId,
                OwnerCharacterId = template.OwnerCharacterId,
                Cost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                IsUsable = true
            };

            card.Keywords.AddRange(template.Keywords);
            foreach (var action in template.Actions)
                card.Actions.Add(EffectActionSpec.Clone(action));

            state.CardsById[id] = card;
            return card;
        }

        public static void SelfDestruct(BattleState state, CombatantState actor, List<BattleEvent> events)
        {
            if (actor == null || !actor.IsAlive)
                return;

            actor.Hp = 0;
            events.Add(new BattleEvent(BattleEventKind.CharacterDied, actor.DisplayName)
            {
                CombatantId = actor.Id
            });
            CombatantDeathRules.OnCharacterDied(state, actor, events);
        }

        public static CardInstanceState CreateBoundCard(
            BattleState state,
            CardTemplate template,
            string ownerCombatantId)
        {
            var id = state.NextCardInstanceId++;
            var card = new CardInstanceState
            {
                InstanceId = id,
                DefinitionId = template.DefinitionId,
                OwnerCharacterId = template.OwnerCharacterId,
                OwnerCombatantId = ownerCombatantId,
                Cost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                IsUsable = true,
                IsBonusHandCard = true
            };

            card.Keywords.AddRange(template.Keywords);
            if (!card.Keywords.Contains("bonus_hand"))
                card.Keywords.Add("bonus_hand");

            foreach (var action in template.Actions)
            {
                card.Actions.Add(new EffectActionSpec
                {
                    Type = action.Type,
                    Target = action.Target,
                    Value = action.Value,
                    StatusId = action.StatusId,
                    Stacks = action.Stacks,
                    Duration = action.Duration,
                    ScaleWithAttack = action.ScaleWithAttack,
                    ScaleWithDefense = action.ScaleWithDefense,
                    AttackScalePercent = action.AttackScalePercent,
                    DefenseScalePercent = action.DefenseScalePercent,
                    Condition = action.Condition,
                    Reach = action.Reach,
                    SplashBehindTarget = action.SplashBehindTarget,
                    SplashPowerPercent = action.SplashPowerPercent,
                    BackRowPowerPercent = action.BackRowPowerPercent,
                    IgnoreDefPercent = action.IgnoreDefPercent,
                    BonusIfTargetHpBelowPercent = action.BonusIfTargetHpBelowPercent,
                    BonusIfTargetHpBelowFlat = action.BonusIfTargetHpBelowFlat,
                    BonusIfTargetHitThisTurnPercent = action.BonusIfTargetHitThisTurnPercent,
                    BonusIfTargetHasStatusId = action.BonusIfTargetHasStatusId,
                    BonusIfTargetHasStatusFlat = action.BonusIfTargetHasStatusFlat,
                    LifestealPercent = action.LifestealPercent,
                    HealMaxHpPercent = action.HealMaxHpPercent,
                    OnKillHealAmount = action.OnKillHealAmount
                });
            }

            state.CardsById[id] = card;
            return card;
        }

        static bool HandAlreadyHasBoundCard(BattleState state, string combatantId, string definitionId)
        {
            foreach (var card in state.EnemyHand)
            {
                if (card.OwnerCombatantId == combatantId && card.DefinitionId == definitionId)
                    return true;
            }

            return false;
        }
    }
}
