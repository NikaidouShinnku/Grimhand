#if UNITY_EDITOR
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
    }
}
#endif
