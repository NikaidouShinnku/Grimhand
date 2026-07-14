using UnityEngine;

namespace Grimhand.Content
{
    /// <summary>音效与 BGM 目录（对应总览表「音效与音乐」sheet）。</summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Grimhand/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        [Header("BGM")]
        public AudioClip BgmCave;
        public AudioClip BgmDungeon;
        public AudioClip BgmOceanRuin;
        public AudioClip BgmCamp;
        public AudioClip BgmBattle;
        public AudioClip BgmBattle2;
        public AudioClip BgmBattle3;

        [Header("UI")]
        public AudioClip UiMenuButtonPress;
        public AudioClip UiButtonHover;
        public AudioClip UiButtonPress;
        public AudioClip UiButtonPress2;
        public AudioClip UiButtonUpgradeCard;
        public AudioClip UiButtonUpgradeCard2;
        public AudioClip UiButtonUpgradePower;
        public AudioClip UiChestOpen;
        public AudioClip UiShopEnter;
        public AudioClip UiGoldAcquire;
        public AudioClip UiRelicsAcquire;
        public AudioClip UiCardAcquire;
        public AudioClip UiCardPackOpen;
        public AudioClip UiInventoryOpen;
        public AudioClip UiInventoryClose;

        [Header("Battle")]
        public AudioClip BattleUsePotion;
        public AudioClip BattleUseConsumable;
        public AudioClip BattleAttackEnemy;
        public AudioClip BattleAttackSnakeQueen;
        public AudioClip BattleAttackWarrior;
        public AudioClip BattleAttackPharaoh;
        public AudioClip BattleAttackDevil;
        public AudioClip BattleAttackLichQueen;
        public AudioClip BattleCast;
        public AudioClip BattleBlocking;
        public AudioClip BattleCardHover;
        public AudioClip BattleCardDraw;
        public AudioClip BattleCardSelect;
        public AudioClip BattleEffectPoison;
        public AudioClip BattleEffectBurn;
        public AudioClip BattleGainArmor;
        public AudioClip BattleHealing;
        public AudioClip BattleHitArmor;
        public AudioClip BattleHit;

        public AudioClip ResolveAttackClip(string characterDefinitionId, bool isEnemy)
        {
            if (isEnemy)
                return BattleAttackEnemy;

            return characterDefinitionId switch
            {
                "char_knight" or "char_warrior" => BattleAttackWarrior,
                "char_mage" or "char_pharaoh" => BattleAttackPharaoh,
                "char_ranger" or "char_demon" => BattleAttackDevil,
                "char_snake_queen" => BattleAttackSnakeQueen,
                "char_lich_queen" => BattleAttackLichQueen,
                _ => BattleAttackEnemy
            };
        }

        public AudioClip PickRandomBattleBgm(System.Random rng = null)
        {
            var clips = new[] { BgmBattle, BgmBattle2, BgmBattle3 };
            var valid = 0;
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                    valid++;
            }

            if (valid == 0)
                return null;

            var pick = rng?.Next(valid) ?? Random.Range(0, valid);
            for (var i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;
                if (pick == 0)
                    return clips[i];
                pick--;
            }

            return BgmBattle;
        }

        public AudioClip PickRandom(AudioClip a, AudioClip b)
        {
            if (a == null)
                return b;
            if (b == null)
                return a;
            return Random.value < 0.5f ? a : b;
        }
    }
}
