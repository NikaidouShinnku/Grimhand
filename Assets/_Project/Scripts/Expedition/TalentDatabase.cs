using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>天赋配置 → 战斗修正与远征级效果。</summary>
    public static class TalentDatabase
    {
        public static void ApplyRunStartEffects(ExpeditionRunState run, ExpeditionConfig config)
        {
            if (run?.Party == null)
                return;

            // 战士（恶魔）s2_lv6：无尽血刃
            if (!run.TalentRun.EndlessBladeInjected
                && HasAnyMemberTalent(run, "talent_ranger_s2_lv6"))
            {
                InjectStartingCard(run, config, "talent_ranger_s2_lv6",
                    PassiveCardMechanicsRules.EndlessBladeCardId,
                    TalentCatalog.RangerId,
                    () => run.TalentRun.EndlessBladeInjected = true);
            }

            // 毒蛇女王 s2_lv7：引爆毒囊
            if (!run.TalentRun.SnakeDetonateVenomInjected
                && HasAnyMemberTalent(run, "talent_snake_s2_lv7"))
            {
                InjectStartingCard(run, config, "talent_snake_s2_lv7",
                    "v_detonate_venom",
                    TalentCatalog.SnakeQueenId,
                    () => run.TalentRun.SnakeDetonateVenomInjected = true);
            }

            // 巫妖女王 s2_lv10：灵界封印
            if (!run.TalentRun.LichRealmSealInjected
                && HasAnyMemberTalent(run, "talent_lich_s2_lv10"))
            {
                InjectStartingCard(run, config, "talent_lich_s2_lv10",
                    "l_realm_seal",
                    TalentCatalog.LichQueenId,
                    () => run.TalentRun.LichRealmSealInjected = true);
            }
        }

        static bool HasAnyMemberTalent(ExpeditionRunState run, string talentId)
        {
            foreach (var member in run.Party)
            {
                if (member != null && HasMemberTalent(member, talentId))
                    return true;
            }
            return false;
        }

        static void InjectStartingCard(
            ExpeditionRunState run,
            ExpeditionConfig config,
            string talentId,
            string cardDefinitionId,
            string ownerCharacterId,
            System.Action markInjected)
        {
            PartyMemberSnapshot target = null;
            foreach (var member in run.Party)
            {
                if (member != null && HasMemberTalent(member, talentId))
                {
                    target = member;
                    break;
                }
            }
            if (target == null)
                return;

            var card = TryFindCatalogCard(config, cardDefinitionId);
            if (card == null)
                return;

            var clone = ExpeditionBattleConfigBuilder.CloneTemplate(card);
            clone.OwnerCharacterId = ownerCharacterId;
            ExpeditionDeckInstanceRules.PrepareNewDeckCard(target, clone);
            target.BonusCards.Add(clone);
            markInjected();
        }

        static CardTemplate TryFindCatalogCard(ExpeditionConfig config, string definitionId)
        {
            if (config?.PlayerCardCatalog == null || string.IsNullOrEmpty(definitionId))
                return null;

            foreach (var template in config.PlayerCardCatalog)
            {
                if (template?.DefinitionId == definitionId)
                    return template;
            }

            return null;
        }

        public static void MergeIntoBattleConfig(
            BattleConfig config,
            IReadOnlyList<PartyMemberSnapshot> party,
            ExpeditionTalentRunState talentRun,
            bool isBossBattle)
        {
            if (config == null)
                return;

            config.Talents ??= new TalentBattleContext();
            config.Talents.ActiveTalentIds.Clear();

            if (party != null)
            {
                foreach (var member in party)
                {
                    if (member == null)
                        continue;

                    CollectTalentId(config.Talents, member.SelectedTalentSlot1Id, member.CharacterDefinitionId);
                    CollectTalentId(config.Talents, member.SelectedTalentSlot2Id, member.CharacterDefinitionId);
                }
            }

            if (talentRun != null)
            {
                config.Talents.MageReviveAvailable = HasTalent(config.Talents, "talent_mage_s1_lv5")
                    && !talentRun.MageReviveUsed;
                config.Talents.RangerBloodDebtAttackBonus = HasTalent(config.Talents, "talent_ranger_s1_lv10")
                    ? ComputeBloodDebtAttackBonus(talentRun)
                    : 0;
            }

            config.Talents.NonBossSoloEnemyBattle = !isBossBattle && CountAliveEnemies(config) == 1;
            ApplyModifierFlags(config.RunModifiers ??= new RunModifierSnapshot(), config.Talents);
        }

        public static void SyncRunStateFromBattle(BattleState state, ExpeditionTalentRunState talentRun)
        {
            if (state == null || talentRun == null)
                return;

            if (state.Config?.Talents?.MageReviveAvailable == true && !state.TalentMageReviveAvailable)
                talentRun.MageReviveUsed = true;

            if (state.TalentSacrificeHpAccumulatedBattle > 0)
            {
                talentRun.RangerSacrificeHpTotal += state.TalentSacrificeHpAccumulatedBattle;
                state.TalentSacrificeHpAccumulatedBattle = 0;
            }
        }

        static void CollectTalentId(TalentBattleContext ctx, string talentId, string ownerCharacterId)
        {
            if (ctx == null || string.IsNullOrEmpty(talentId))
                return;

            var talent = TalentCatalog.Get(talentId);
            if (talent == null || !TalentRules.BelongsToCharacter(talent, ownerCharacterId))
                return;

            ctx.ActiveTalentIds.Add(talentId);
        }

        static void ApplyModifierFlags(RunModifierSnapshot mods, TalentBattleContext ctx)
        {
            if (mods == null || ctx == null)
                return;

            if (ctx.Has("talent_ranger_s1_lv1"))
                mods.SacrificeHpCostReductionPercent += 25f;

            if (ctx.Has("talent_ranger_s1_lv5"))
            {
                mods.SacrificeDamageBonusPercent += 30f;
                mods.SacrificeHpCostIncreasePercent += 40f;
            }
        }

        static int ComputeBloodDebtAttackBonus(ExpeditionTalentRunState talentRun)
        {
            if (talentRun == null || talentRun.RangerSacrificeHpTotal < 50)
                return 0;

            var stacks = talentRun.RangerSacrificeHpTotal / 50;
            return System.Math.Min(10, stacks);
        }

        static int CountAliveEnemies(BattleConfig config)
        {
            if (config?.Combatants == null)
                return 0;

            var count = 0;
            foreach (var cc in config.Combatants)
            {
                if (cc?.Team == TeamSide.Enemy)
                    count++;
            }

            return count;
        }

        static bool HasMemberTalent(PartyMemberSnapshot member, string talentId) =>
            member != null
            && (member.SelectedTalentSlot1Id == talentId || member.SelectedTalentSlot2Id == talentId);

        public static bool HasTalent(BattleState state, string talentId) =>
            state?.Config?.Talents?.Has(talentId) == true;

        public static bool HasTalent(TalentBattleContext ctx, string talentId) =>
            ctx?.Has(talentId) == true;
    }
}
