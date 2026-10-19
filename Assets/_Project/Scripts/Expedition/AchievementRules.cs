using System;
using System.Collections.Generic;
using System.Linq;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public interface IAchievementProfile
    {
        AchievementProgressState Achievements { get; }
    }

    public static class AchievementRules
    {
        // Deliberately fixed: future heroes must not make an existing achievement harder.
        public static readonly string[] OriginalSix = { "char_knight", "char_mage", "char_ranger", "char_snake_queen", "char_lich_queen", "char_berserker" };
        public static bool IsEligible(ExpeditionRunState run) => run != null && !run.IsTrainingGround && !run.IsTutorialRun && !run.AchievementsDisabled;
        public static void Unlock(IExpeditionMetaProfile profile, string id)
        {
            if(profile is IAchievementProfile p) p.Achievements?.Unlock(id);
        }
        public static void OnEngraved(ExpeditionRunState run, IExpeditionMetaProfile profile, string definitionId)
        {
            if(!IsEligible(run) || profile is not IAchievementProfile p)return;
            var a=p.Achievements;
            if(a==null)return;
            if(a.CompletedEngravings<int.MaxValue)a.CompletedEngravings++;
            if(a.CompletedEngravings>=5)a.Unlock("ACH_FIVE_ENGRAVINGS");
            if(CardRarityTable.GetOrDefault(definitionId)==CardRarity.Legendary)a.Unlock("ACH_ORANGE_ENGRAVING");
        }
        public static void ReconcilePermanent(AchievementProgressState progress, CampMetaState meta, CampCollectionState collection)
        {
            if(progress==null)return;
            var cards=new HashSet<string>(collection?.Entries.Where(c=>!string.IsNullOrEmpty(c)) ?? Enumerable.Empty<string>());
            if(cards.Count>=30)progress.Unlock("ACH_COLLECTION_30");
            if(cards.Count(c=>CardRarityTable.GetOrDefault(c)==CardRarity.Legendary)>=5)progress.Unlock("ACH_COLLECTION_FIVE_ORANGE");
            if(collection!=null)
                for(var i=0;i<collection.Entries.Count;i++)
                    if(collection.IsEngravedAt(i) && CardRarityTable.GetOrDefault(collection.Entries[i])==CardRarity.Legendary)
                    {progress.Unlock("ACH_ORANGE_ENGRAVING");break;}
            var full=0;
            foreach(var id in OriginalSix)
                if(meta!=null && meta.Characters.TryGetValue(id,out var character) && character!=null && character.OutOfRunLevel>=MetaProgressionRules.MaxOutOfRunLevel)full++;
            if(meta?.Characters.Values.Any(c=>c!=null && c.OutOfRunLevel>=MetaProgressionRules.MaxOutOfRunLevel)==true)
                progress.Unlock("ACH_ONE_CHARACTER_MAX");
            if(full==OriginalSix.Length)progress.Unlock("ACH_ALL_CHARACTERS_MAX");
            if(progress.CompletedEngravings>=5)progress.Unlock("ACH_FIVE_ENGRAVINGS");
        }
        public static void OnRunEnded(ExpeditionRunState run,IExpeditionMetaProfile profile)
        {
            if(IsEligible(run) && !run.WasAbandoned && run.Phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed)
                Unlock(profile,"ACH_FINISH_EXPEDITION");
        }
        public static void CheckRunProgress(ExpeditionRunState run,IExpeditionMetaProfile profile,ExpeditionConfig config=null)
        {
            if(!IsEligible(run))return;
            if(run.AchievementRelicEvolved)Unlock(profile,"ACH_RELIC_EVOLUTION");
            foreach(var member in run.Party)
                foreach(var card in config!=null
                    ? ExpeditionRunDeckCatalog.CollectMemberDeck(config,member)
                    : member.BonusCards)
                    if(CardUpgradeRules.GetMaxLevel(card.DisplayName)>0 && Math.Max(card.UpgradeLevel,CardUpgradeRules.GetLevel(member,card.DeckInstanceId))>=CardUpgradeRules.GetMaxLevel(card.DisplayName))
                    {Unlock(profile,"ACH_CARD_MAX_UPGRADE");return;}
        }

        static bool RealPlayer(string id) => OriginalSix.Contains(id) || id is
            "char_warrior" or "char_pharaoh" or "char_demon" or "char_viper_queen" or "char_lich";

        public static void OnBattleProgress(ExpeditionRunState run,IExpeditionMetaProfile profile,BattleState state)
        {
            if(!IsEligible(run) || state==null)return;
            var telemetry=state.AchievementTelemetry;
            if(telemetry!=null)
            {
                if(telemetry.ThreeResponsesInTurn)Unlock(profile,"ACH_THREE_RESPONSES");
                if(telemetry.ThreePoisonKills)Unlock(profile,"ACH_THREE_POISON_KILLS");
                if(telemetry.PoisonBurnKill)Unlock(profile,"ACH_POISON_AND_BURN");
                if(telemetry.ThreeKillsOneAttack)Unlock(profile,"ACH_THREE_KILLS_ONE_CARD");
                if(telemetry.FrontBackKillsInTurn)Unlock(profile,"ACH_FRONT_BACK_KILLS");
                if(telemetry.RetainedThenUsed)Unlock(profile,"ACH_RETAIN_THREE_TURNS");
            }
        }

        public static void OnBattleFinished(ExpeditionRunState run,IExpeditionMetaProfile profile,BattleState state,ExpeditionConfig config=null)
        {
            if(!IsEligible(run) || state==null)return;
            OnBattleProgress(run,profile,state);
            if(state.Outcome!=BattleOutcome.PlayerVictory)return;
            Unlock(profile,"ACH_FIRST_VICTORY");
            if(run.LastBattleWasElite)Unlock(profile,"ACH_ELITE_VICTORY");
            // Match BattleEngine's initial HP calculation. Expedition's automatic
            // revival enters with StartHp=1, so those heroes do count as alive.
            var initial=state.Config?.Combatants.Where(c=>c.Team==TeamSide.Player && RealPlayer(c.CharacterDefinitionId)
                && !c.Id.StartsWith("summon_") && c.MaxHp>0 && (c.StartHp ?? c.MaxHp)>0).ToArray();
            if(initial?.Length==3 && initial.All(c=>state.AchievementTelemetry?.RespondedPlayerIds.Contains(c.Id)==true))
                Unlock(profile,"ACH_ALL_RESPOND");
            var initialPlayers=initial?.Length ?? 0;
            var survivors=initial?.Count(c=>state.Combatants.Any(actor=>actor.Id==c.Id && actor.Team==TeamSide.Player && actor.IsAlive)) ?? 0;
            if(run.LastBattleWasBoss)
            {
                if(initialPlayers>=2 && survivors==1)Unlock(profile,"ACH_LAST_SURVIVOR");
                if(initialPlayers==3 && survivors==3)Unlock(profile,"ACH_BOSS_ALL_ALIVE");
                foreach(var enemy in state.Combatants.Where(c=>c.Team==TeamSide.Enemy && !c.IsAlive))
                {
                    var id=(run.LastBattleFloor,enemy.CharacterDefinitionId) switch
                    {
                        (20,"char_skeleton_king")=>"ACH_DEFEAT_SKELETON_KING",
                        (20,"char_ghost_queen")=>"ACH_DEFEAT_GHOST_QUEEN",
                        (40,"char_warden")=>"ACH_DEFEAT_WARDEN",
                        (40,"char_dark_knight")=>"ACH_DEFEAT_DARK_KNIGHT",
                        (60,"char_corrupted_ocean_goddess")=>"ACH_DEFEAT_OCEAN_GODDESS",
                        _=>null
                    };
                    if(id!=null)Unlock(profile,id);
                }
                if(run.AchievementBattleStartConsumableCount>=3 && !state.ConsumableUsedThisBattle)
                    Unlock(profile,"ACH_BOSS_NO_CONSUMABLES");
            }
            CheckRunProgress(run,profile,config);
        }
    }
}
