#if UNITY_EDITOR
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using UnityEditor;
using UnityEngine;

namespace Grimhand.Content.Editor
{
    public static partial class MonsterContentGenerator
    {
        public static void UpdateBossVisualCatalog(CharacterVisualCatalogSO catalog)
        {
            if (catalog == null)
                return;

            var wardenArt = ArtRoot + "/warden";
            UpsertVisualFull(catalog, WardenBossEncounterBuilder.CharacterId,
                idle: wardenArt + "/warden_idle_1024.png",
                attack: wardenArt + "/warden_attack_1024.png",
                hit: wardenArt + "/warden_hit_1024.png",
                death: wardenArt + "/warden_defeat_1024.png",
                defend: wardenArt + "/warden_defend_1024.png",
                profile: wardenArt + "/warden_profile.png",
                gifPath: "The Grimhands Asset/monsters/warden/warden_idle_anime.gif",
                preserveOriginalFacing: true,
                portraitScaleMultiplier: 1.15f);

            UpsertVisual(catalog, CharacterTraitCatalog.PrisonCageCharacterId,
                ArtRoot + "/prisoner_cage.png");

            var knightArt = ArtRoot + "/dark knight";
            UpsertVisualFull(catalog, DarkKnightBossEncounterBuilder.CharacterId,
                idle: knightArt + "/darkknight_idle_1024.png",
                attack: knightArt + "/darkknight_attack_1024.png",
                hit: knightArt + "/darkknight_hit_1024.png",
                death: knightArt + "/darkknight_defeat_1024.png",
                defend: knightArt + "/darkknight_defend_1024.png",
                profile: knightArt + "/darkknight_profile.png",
                gifPath: "The Grimhands Asset/monsters/dark knight/darkknight_idle_anime.gif",
                preserveOriginalFacing: true,
                portraitScaleMultiplier: 1.15f);

            var goddessArt = ArtRoot + "/corrupted oceangoddess";
            UpsertVisualFull(catalog, CorruptedOceanGoddessBossEncounterBuilder.CharacterId,
                idle: goddessArt + "/corrupted_oceangoddess_idle_1024.png",
                attack: goddessArt + "/corrupted_oceangoddess_attack_1024.png",
                hit: goddessArt + "/corrupted_oceangoddess_hit_1024.png",
                death: goddessArt + "/corrupted_oceangoddess_defeated_1024.png",
                defend: goddessArt + "/corrupted_oceangoddess_defend_1024.png",
                profile: goddessArt + "/corrupted_oceangoddess_idle_1024.png",
                gifPath: "The Grimhands Asset/monsters/corrupted oceangoddess/corrupted_oceangoddess_idle_anime.gif",
                preserveOriginalFacing: true);

            EditorUtility.SetDirty(catalog);
        }

        /// <summary>生成典狱长 / 黑暗骑士 / 腐化海洋女神卡牌 SO，供图鉴与假人出牌使用。</summary>
        public static void GenerateV09BossCards()
        {
            foreach (var template in V09BossCardCatalog.AllCanonicalCards())
                SaveCardFromTemplate(template, CardRarity.Epic);

            SaveBoss("Character_Warden", WardenBossEncounterBuilder.CharacterId, "典狱长",
                FormationSlot.Back, 250, 22, 8, 5,
                new[] { CharacterTraitCatalog.WardenCageMaster },
                System.Array.Empty<CardDefinitionSO>());
            SaveBoss("Character_Dark_Knight", DarkKnightBossEncounterBuilder.CharacterId, "黑暗骑士",
                FormationSlot.Front, 350, 25, 10, 8,
                new[] { CharacterTraitCatalog.DarkKnightPoisonAura },
                System.Array.Empty<CardDefinitionSO>());
            SaveBoss("Character_Corrupted_Ocean_Goddess",
                CorruptedOceanGoddessBossEncounterBuilder.CharacterId, "腐化海洋女神",
                FormationSlot.Front, 400, 20, 10, 6,
                new[] { CharacterTraitCatalog.OceanGoddessTide },
                System.Array.Empty<CardDefinitionSO>());
            SaveBoss("Character_Prison_Cage", CharacterTraitCatalog.PrisonCageCharacterId, "囚笼",
                FormationSlot.Middle, 150, 0, 5, 5,
                new[] { CharacterTraitCatalog.PrisonCage },
                System.Array.Empty<CardDefinitionSO>());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SaveCardFromTemplate(CardTemplate template, CardRarity rarity)
        {
            if (template == null)
                return;

            var actions = new List<EffectActionDefinition>();
            foreach (var action in template.Actions)
                actions.Add(EffectActionDefinition.FromSpec(action));

            SaveCard(
                template.DefinitionId,
                template.DisplayName,
                template.OwnerCharacterId,
                template.Cost,
                template.CardType,
                template.Keywords.Count > 0 ? template.Keywords.ToArray() : null,
                rarity,
                actions.ToArray());
        }
    }
}
#endif
