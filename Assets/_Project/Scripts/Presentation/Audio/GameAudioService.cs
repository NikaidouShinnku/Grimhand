using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Presentation.Camp;
using UnityEngine;

namespace Grimhand.Presentation.Audio
{
    /// <summary>全局 BGM / SFX 播放（Master × Music/Sfx）。</summary>
    [DisallowMultipleComponent]
    public sealed class GameAudioService : MonoBehaviour
    {
        const string ServiceName = "GrimhandAudio";
        const float BattleSfxScale = 1.5f;
        const float RewardSfxScale = 1.5f;
        const float ChestOpenSfxScale = 3f;
        // 点选卡 / 获取卡牌音效原本偏轻，按设计再放大。
        const float CardRewardSfxScale = 10f;
        const float BattleCardClickSfxScale = 10f;

        static GameAudioService _instance;

        AudioCatalogSO _catalog;
        AudioSource _bgmSource;
        AudioSource _sfxSource;
        string _bgmKey = "";
        AudioClip _battleBgmClip;

        public static GameAudioService Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var existing = FindAnyObjectByType<GameAudioService>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    _instance = existing;
                    return _instance;
                }

                var go = new GameObject(ServiceName);
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<GameAudioService>();
                _instance.EnsureSources();
                return _instance;
            }
        }

        public AudioCatalogSO Catalog => _catalog;

        public void BindCatalog(AudioCatalogSO catalog)
        {
            if (catalog != null)
                _catalog = catalog;
            EnsureSources();
            ApplyVolumes();
        }

        public static void Ensure(AudioCatalogSO catalog)
        {
            var service = Instance;
            if (catalog != null)
                service.BindCatalog(catalog);
            service.ApplyVolumes();
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSources();
            ApplyVolumes();
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        void EnsureSources()
        {
            if (_bgmSource == null)
            {
                var bgmGo = new GameObject("BgmSource");
                bgmGo.transform.SetParent(transform, false);
                _bgmSource = bgmGo.AddComponent<AudioSource>();
                _bgmSource.playOnAwake = false;
                _bgmSource.loop = true;
                _bgmSource.spatialBlend = 0f;
            }

            if (_sfxSource == null)
            {
                var sfxGo = new GameObject("SfxSource");
                sfxGo.transform.SetParent(transform, false);
                _sfxSource = sfxGo.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.loop = false;
                _sfxSource.spatialBlend = 0f;
            }
        }

        public void ApplyVolumes()
        {
            EnsureSources();
            if (_bgmSource != null)
                _bgmSource.volume = Mathf.Clamp01(GameSettings.MasterVolume * GameSettings.MusicVolume);
            if (_sfxSource != null)
                _sfxSource.volume = Mathf.Clamp01(GameSettings.MasterVolume * GameSettings.SfxVolume);
            AudioListener.volume = 1f;
        }

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
                return;

            EnsureSources();
            ApplyVolumes();
            // PlayOneShot / AudioSource.volume 都被夹在 0–1；volumeScale>1 时叠播近似放大。
            var baseVol = Mathf.Clamp01(GameSettings.MasterVolume * GameSettings.SfxVolume);
            _sfxSource.spatialBlend = 0f;
            _sfxSource.ignoreListenerPause = true;
            _sfxSource.volume = baseVol;

            var layers = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1f, volumeScale)), 1, 12);
            for (var i = 0; i < layers; i++)
                _sfxSource.PlayOneShot(clip, 1f);
        }

        public void PlayBgm(AudioClip clip, string key, bool restartIfSame = false)
        {
            if (clip == null)
                return;

            EnsureSources();
            _bgmSource.loop = true;

            if (!restartIfSame && _bgmKey == key && _bgmSource.isPlaying && _bgmSource.clip == clip)
            {
                ApplyVolumes();
                return;
            }

            _bgmKey = key ?? "";
            _bgmSource.clip = clip;
            ApplyVolumes();
            _bgmSource.Play();
        }

        public void StopBgm()
        {
            if (_bgmSource == null)
                return;

            _bgmSource.Stop();
            _bgmSource.clip = null;
            _bgmKey = "";
            _battleBgmClip = null;
        }

        public void PlayCampBgm()
        {
            if (_catalog == null)
                return;
            _battleBgmClip = null;
            PlayBgm(_catalog.BgmCamp, "camp");
        }

        public void PlayMapBgm(int layerNumber)
        {
            if (_catalog == null)
                return;

            _battleBgmClip = null;

            AudioClip clip;
            string key;
            if (ExpeditionRegionRules.IsAbyssLayer(layerNumber))
            {
                clip = _catalog.BgmOceanRuin;
                key = "map_abyss";
            }
            else if (ExpeditionRegionRules.IsDungeonLayer(layerNumber))
            {
                clip = _catalog.BgmDungeon;
                key = "map_dungeon";
            }
            else
            {
                clip = _catalog.BgmCave;
                key = "map_cave";
            }

            PlayBgm(clip, key);
        }

        /// <summary>
        /// 战斗 BGM：开战时随机一首并循环该曲；同场战斗内不换曲。
        /// </summary>
        /// <param name="reselect">true 表示新开一场战斗，重新抽曲。</param>
        public void PlayBattleBgm(bool reselect = false)
        {
            if (_catalog == null)
                return;

            EnsureSources();
            _bgmSource.loop = true;

            if (reselect || _battleBgmClip == null)
                _battleBgmClip = _catalog.PickRandomBattleBgm();

            if (_battleBgmClip == null)
                return;

            if (_bgmKey == "battle"
                && _bgmSource.clip == _battleBgmClip
                && _bgmSource.isPlaying)
            {
                ApplyVolumes();
                return;
            }

            PlayBgm(_battleBgmClip, "battle", restartIfSame: true);
        }

        public void PlayUiMenuPress() => PlaySfx(_catalog?.UiMenuButtonPress);

        public void PlayUiButtonHover() => PlaySfx(_catalog?.UiButtonHover);

        public void PlayUiButtonPress() => PlaySfx(_catalog?.UiButtonPress);

        public void PlayUiUpgradeCard() =>
            PlaySfx(_catalog?.PickRandom(_catalog.UiButtonUpgradeCard, _catalog.UiButtonUpgradeCard2));

        public void PlayUiUpgradePower() => PlaySfx(_catalog?.UiButtonUpgradePower);

        public void PlayUiChestOpen() => PlaySfx(_catalog?.UiChestOpen, ChestOpenSfxScale);

        public void PlayUiShopEnter() => PlaySfx(_catalog?.UiShopEnter);

        public void PlayUiGoldAcquire() => PlaySfx(_catalog?.UiGoldAcquire, RewardSfxScale);

        public void PlayUiRelicsAcquire() => PlaySfx(_catalog?.UiRelicsAcquire, RewardSfxScale);

        /// <summary>获取消耗品（奖励/商店）暂与遗物相同。</summary>
        public void PlayUiConsumableAcquire() => PlayUiRelicsAcquire();

        public void PlayBattleCardSelect()
        {
            EnsureCatalogFallback();
            var clip = _catalog?.BattleCardSelect ?? _catalog?.UiButtonPress ?? _catalog?.UiMenuButtonPress;
            PlaySfx(clip, BattleCardClickSfxScale);
        }

        public void PlayBattleCardHover()
        {
            EnsureCatalogFallback();
            PlaySfx(_catalog?.BattleCardHover ?? _catalog?.UiButtonHover);
        }

        public void PlayUiCardAcquire()
        {
            EnsureCatalogFallback();
            PlaySfx(_catalog?.UiCardAcquire ?? _catalog?.BattleCardSelect, CardRewardSfxScale);
        }

        public void PlayUiCardPackOpen() => PlaySfx(_catalog?.UiCardPackOpen, CardRewardSfxScale);

        public void PlayUiInventoryOpen() => PlaySfx(_catalog?.UiInventoryOpen);

        public void PlayUiInventoryClose() => PlaySfx(_catalog?.UiInventoryClose);

        void EnsureCatalogFallback()
        {
            if (_catalog != null)
                return;

            _catalog = Resources.Load<AudioCatalogSO>("AudioCatalog_Demo");
        }

        public void PlayBattleCardDraw() => PlaySfx(_catalog?.BattleCardDraw, BattleSfxScale);

        public void PlayBattleUseConsumable(bool isPotion) =>
            PlaySfx(isPotion ? _catalog?.BattleUsePotion : _catalog?.BattleUseConsumable, BattleSfxScale);

        public void PlayBattleAttack(string characterDefinitionId, bool isEnemy) =>
            PlaySfx(_catalog?.ResolveAttackClip(characterDefinitionId, isEnemy), BattleSfxScale);

        public void PlayBattleCast() => PlaySfx(_catalog?.BattleCast, BattleSfxScale);

        public void PlayBattleBlocking() => PlaySfx(_catalog?.BattleBlocking, BattleSfxScale);

        public void PlayBattleGainArmor() => PlaySfx(_catalog?.BattleGainArmor, BattleSfxScale);

        public void PlayBattleHealing() => PlaySfx(_catalog?.BattleHealing, BattleSfxScale);

        public void PlayBattleHit(bool absorbedByArmor) =>
            PlaySfx(absorbedByArmor ? _catalog?.BattleHitArmor : _catalog?.BattleHit, BattleSfxScale);

        public void PlayBattleStatusEffect(string statusId)
        {
            if (string.IsNullOrEmpty(statusId) || _catalog == null)
                return;

            if (statusId is "poison" or "necrotic_poison")
                PlaySfx(_catalog.BattleEffectPoison, BattleSfxScale);
            else if (statusId == "burn")
                PlaySfx(_catalog.BattleEffectBurn, BattleSfxScale);
        }
    }
}
