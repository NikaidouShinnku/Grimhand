using System;
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

            var slot = FindSlotBehindSummoner(state, summoner);
            if (slot == null)
                return false;

            var template = EnsureExplosiveSkullTemplate(state);
            if (template == null)
                return false;

            SpawnFromTemplate(state, template, slot.Value, events);
            return true;
        }

        /// <summary>
        /// 训练场/缺 SummonTemplates 的战斗也能召唤：写入默认易爆骷髅头模板（含自爆牌）。
        /// </summary>
        public static CombatantConfig EnsureExplosiveSkullTemplate(BattleState state)
        {
            if (state?.Config == null)
                return null;

            if (state.Config.SummonTemplates.TryGetValue(ExplosiveSkullCharacterId, out var existing)
                && existing != null)
                return existing;

            var template = CreateDefaultExplosiveSkullTemplate();
            state.Config.SummonTemplates[ExplosiveSkullCharacterId] = template;
            return template;
        }

        public static CombatantConfig CreateDefaultExplosiveSkullTemplate()
        {
            var skull = new CombatantConfig
            {
                Id = "Character_Explosive_Skull",
                DisplayName = "易爆骷髅头",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = ExplosiveSkullCharacterId,
                MaxHp = 20,
                StartHp = 20,
                BaseAttack = 0,
                BaseDefense = 5,
                Speed = 2
            };

            skull.Traits.Add(CharacterTraitCatalog.SkullSelfDestructHand);
            skull.DeckTemplates.Add(CreateSkullExplodeTemplate());
            return skull;
        }

        static CardTemplate CreateSkullExplodeTemplate()
        {
            var card = new CardTemplate
            {
                DefinitionId = CharacterTraitCatalog.SkullExplodeCardId,
                DisplayName = "骷髅自爆",
                OwnerCharacterId = ExplosiveSkullCharacterId,
                Cost = 0,
                CardType = CardType.Attack
            };
            card.Keywords.Add("self_destruct");
            card.Keywords.Add("bonus_hand");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.RandomEnemy,
                Value = 24,
                Reach = TargetReach.Any
            });
            return card;
        }

        /// <summary>优先在召唤者身后（更后排）空位生成；无身后空位时回退任意空位。</summary>
        public static FormationSlot? FindSlotBehindSummoner(BattleState state, CombatantState summoner)
        {
            if (state == null || summoner == null)
                return null;

            var occupied = new HashSet<FormationSlot>();
            foreach (var unit in state.GetTeam(summoner.Team))
            {
                if (unit.IsAlive)
                    occupied.Add(unit.Slot);
            }

            FormationSlot[] preferred = summoner.Slot switch
            {
                FormationSlot.Front => new[] { FormationSlot.Middle, FormationSlot.Back },
                FormationSlot.Middle => new[] { FormationSlot.Back },
                _ => System.Array.Empty<FormationSlot>()
            };

            foreach (var slot in preferred)
            {
                if (!occupied.Contains(slot))
                    return slot;
            }

            return FindEmptyTeamSlot(state, summoner.Team);
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
            List<BattleEvent> events,
            int bonusMaxHp = 0)
        {
            if (state == null || template == null)
                return;

            var id = $"summon_{template.CharacterDefinitionId}_{state.NextSummonInstanceId++}";
            var maxHp = template.MaxHp + Math.Max(0, bonusMaxHp);
            var combatant = new CombatantState
            {
                Id = id,
                DisplayName = template.DisplayName,
                Team = template.Team,
                Slot = slot,
                CharacterDefinitionId = template.CharacterDefinitionId,
                Level = template.Level,
                Xp = template.Xp,
                MaxHp = maxHp,
                BaseAttack = template.BaseAttack,
                BaseDefense = template.BaseDefense,
                Speed = template.Speed,
                Hp = maxHp
            };

            combatant.Traits.AddRange(template.Traits);
            if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.BossFirstHitBlock))
                combatant.BossFirstHitBlockPending = true;

            state.Combatants.Add(combatant);
            RelicBattleRules.RefreshDerivedStats(state, combatant, state.Config?.RunModifiers);

            events.Add(new BattleEvent(BattleEventKind.CombatantSpawned, combatant.DisplayName)
            {
                CombatantId = combatant.Id,
                TargetId = slot.ToString(),
                Amount = bonusMaxHp > 0 ? bonusMaxHp : 0
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
            if (state == null || dead == null)
                return;

            // 持有鼠群呼唤的单位死亡时，在原位召唤 50% 血量的鼠人（不要求死者本身是鼠人，便于假人测试）
            var maxHp = System.Math.Max(1, dead.MaxHp / 2);
            var id = $"summon_{MinionTraitCatalog.RatCharacterId}_{state.NextSummonInstanceId++}";
            var combatant = new CombatantState
            {
                Id = id,
                DisplayName = "鼠人",
                Team = dead.Team,
                Slot = dead.Slot,
                CharacterDefinitionId = MinionTraitCatalog.RatCharacterId,
                Level = dead.Level,
                Xp = dead.Xp,
                MaxHp = maxHp,
                BaseAttack = System.Math.Max(1, dead.BaseAttack),
                BaseDefense = System.Math.Max(0, dead.BaseDefense),
                Speed = System.Math.Max(1, dead.Speed),
                Hp = maxHp
            };

            combatant.Traits.Add(MinionTraitCatalog.RatPackAttackOnAllyDeath);
            // 继承本场已累计的鼠群狂怒
            combatant.RatPackAttackBonusPercent =
                state.RatDeathsThisBattle * MinionTraitCatalog.RatPackAttackBonusPercentPerDeath;
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
                BaseCost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                UpgradeLevel = template.UpgradeLevel,
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
                BaseCost = template.Cost,
                CardType = template.CardType,
                DisplayName = template.DisplayName,
                UpgradeLevel = template.UpgradeLevel,
                IsUsable = true,
                IsBonusHandCard = true
            };

            card.Keywords.AddRange(template.Keywords);
            if (!card.Keywords.Contains("bonus_hand"))
                card.Keywords.Add("bonus_hand");

            foreach (var action in template.Actions)
                card.Actions.Add(EffectActionSpec.Clone(action));

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
