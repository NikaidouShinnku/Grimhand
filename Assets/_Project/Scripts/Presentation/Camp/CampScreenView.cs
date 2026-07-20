using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>营地主界面：背景 + 可点击建筑（champion_camp / portal / merchant_camp）。</summary>
    [DisallowMultipleComponent]
    public sealed class CampScreenView : MonoBehaviour
    {
        const float ChampionHeight = 400f;
        const float PortalHeight = 360f;
        const float MerchantHeight = 400f;
        const float TalentAltarHeight = 340f;
        const float TrainingGroundHeight = 340f;

        [SerializeField] BattleUiIconCatalogSO uiIcons;

        bool _built;
        GameObject _toastPanel;
        Text _toastText;
        Text _accountGoldText;
        Action _onChampionCamp;
        Action _onPortal;
        Action _onTalentAltar;
        Action _onMetaShop;
        Action _onTrainingGround;
        Action<string> _onFeatureComingSoon;

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(
            Action onChampionCamp,
            Action onPortal,
            Action onTalentAltar,
            Action onMetaShop,
            Action onTrainingGround,
            BattleUiIconCatalogSO icons = null,
            Action<string> onFeatureComingSoon = null)
        {
            if (icons != null)
                uiIcons = icons;

            _onChampionCamp = onChampionCamp;
            _onPortal = onPortal;
            _onTalentAltar = onTalentAltar;
            _onMetaShop = onMetaShop;
            _onTrainingGround = onTrainingGround;
            _onFeatureComingSoon = onFeatureComingSoon;
            EnsureBuilt();
            HideToast();
        }

        public void Show(int accountGold = 0)
        {
            EnsureBuilt();
            RefreshAccountGold(accountGold);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void RefreshAccountGold(int accountGold)
        {
            if (_accountGoldText != null)
                _accountGoldText.text = accountGold.ToString();
        }

        public void Hide() => gameObject.SetActive(false);

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            ResolveArtFromCatalog();

            var rt = GetComponent<RectTransform>();
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(rt);

            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var bg = CampUiRuntime.CreateImage("Background", transform, Color.white);
            CampUiRuntime.StretchFull(bg.rectTransform);
            bg.sprite = uiIcons != null ? uiIcons.CampSiteBackground : null;
            bg.preserveAspect = false;
            if (bg.sprite == null)
                bg.color = new Color(0.05f, 0.07f, 0.12f, 1f);

            BuildTopBar(transform);
            BuildSceneBuildings(transform);
            BuildBottomBar(transform);
            BuildToast(transform);
        }

        void ResolveArtFromCatalog()
        {
            if (uiIcons == null)
            {
                Debug.LogError("[CampScreen] 未绑定 BattleUiIconCatalogSO。请执行 Grimhand → Open Battle Test Scene。");
                return;
            }

            if (uiIcons.ChampionCampBuilding == null || uiIcons.PortalBuilding == null
                || uiIcons.MerchantCampBuilding == null || uiIcons.CampSiteBackground == null)
            {
                Debug.LogWarning(
                    "[CampScreen] 营地美术未刷新。请执行 Grimhand → Content → Refresh UI Visual Catalog。");
            }
        }

        void BuildTopBar(Transform parent)
        {
            var bar = CampUiRuntime.CreateImage("TopBar", parent, new Color(0.04f, 0.05f, 0.08f, 0.72f));
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, 56f);

            var title = CampUiRuntime.CreateText(bar.transform, "Grimhand 营地", 24, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.45f, 1f);
            title.rectTransform.offsetMin = new Vector2(24f, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            _accountGoldText = CampUiRuntime.CreateText(bar.transform, "0", 18, FontStyle.Normal,
                TextAnchor.MiddleRight);
            _accountGoldText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
            _accountGoldText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _accountGoldText.rectTransform.offsetMin = Vector2.zero;
            _accountGoldText.rectTransform.offsetMax = new Vector2(-56f, 0f);
            _accountGoldText.color = new Color(0.92f, 0.88f, 0.72f, 1f);

            var goldIcon = uiIcons != null ? uiIcons.CampGoldIcon : null;
            if (goldIcon != null)
            {
                var icon = CampUiRuntime.CreateImage("CampGoldIcon", bar.transform, Color.white);
                icon.sprite = goldIcon;
                icon.preserveAspect = true;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = new Vector2(1f, 0.5f);
                iconRt.anchorMax = new Vector2(1f, 0.5f);
                iconRt.pivot = new Vector2(1f, 0.5f);
                iconRt.anchoredPosition = new Vector2(-24f, 0f);
                iconRt.sizeDelta = new Vector2(32f, 32f);
            }
        }

        void BuildSceneBuildings(Transform parent)
        {
            var zone = CampUiRuntime.CreateRect("Buildings", parent);
            var zoneRt = zone.GetComponent<RectTransform>();
            zoneRt.anchorMin = new Vector2(0f, 0.05f);
            zoneRt.anchorMax = new Vector2(1f, 0.92f);
            zoneRt.offsetMin = Vector2.zero;
            zoneRt.offsetMax = Vector2.zero;

            // 绘制顺序：后添加的在上层，射线也优先命中上层。
            CreateBuilding(zone.transform, "Portal", uiIcons?.PortalBuilding,
                new Vector2(0.5f, 0.07f), PortalHeight, _onPortal);

            CreateBuilding(zone.transform, "ChampionCamp", uiIcons?.ChampionCampBuilding,
                new Vector2(0.21f, 0.06f), ChampionHeight, _onChampionCamp);

            CreateBuilding(zone.transform, "TalentAltar", uiIcons?.TalentAltarBuilding,
                new Vector2(0.38f, 0.06f), TalentAltarHeight, _onTalentAltar);

            CreateBuilding(zone.transform, "MerchantCamp", uiIcons?.MerchantCampBuilding,
                new Vector2(0.79f, 0.06f), MerchantHeight, _onMetaShop);

            CreateBuilding(zone.transform, "TrainingGround", uiIcons?.TrainingGroundBuilding,
                new Vector2(0.62f, 0.06f), TrainingGroundHeight, _onTrainingGround);
        }

        void CreateBuilding(
            Transform parent,
            string id,
            Sprite sprite,
            Vector2 groundAnchor,
            float targetHeight,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = groundAnchor;
            rt.anchorMax = groundAnchor;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;

            if (sprite != null)
            {
                var aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
                rt.sizeDelta = new Vector2(targetHeight * aspect, targetHeight);
            }
            else
            {
                rt.sizeDelta = new Vector2(targetHeight * 0.9f, targetHeight);
                Debug.LogWarning($"[CampScreen] 缺少建筑贴图：{id}");
            }

            var img = go.AddComponent<CampShapeImage>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;

            if (sprite != null)
            {
                img.sprite = sprite;
                img.ApplyShapeHitTestIfSupported();
            }

            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void BuildBottomBar(Transform parent)
        {
            var bar = CampUiRuntime.CreateImage("BottomBar", parent, new Color(0.04f, 0.05f, 0.08f, 0.72f));
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.sizeDelta = new Vector2(0f, 52f);

            var layoutGo = CampUiRuntime.CreateRect("Nav", bar.transform);
            CampUiRuntime.StretchFull(layoutGo.GetComponent<RectTransform>());
            var h = layoutGo.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 28f;
            h.padding = new RectOffset(0, 0, 6, 6);
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            CreateNavButton(layoutGo.transform, "设置", "设置");
            CreateNavButton(layoutGo.transform, "邮件", "邮件");
            CreateNavButton(layoutGo.transform, "成就", "成就");
            CreateNavButton(layoutGo.transform, "活动", "活动");
        }

        void CreateNavButton(Transform parent, string label, string toastKey)
        {
            var btn = CampUiRuntime.CreateButton(parent, label, new Color(0.18f, 0.22f, 0.32f, 0.9f),
                new Vector2(120f, 38f));
            btn.onClick.AddListener(() => _onFeatureComingSoon?.Invoke(toastKey));
        }

        void BuildToast(Transform parent)
        {
            _toastPanel = CampUiRuntime.CreateImage("Toast", parent, new Color(0.08f, 0.1f, 0.14f, 0.94f)).gameObject;
            var toastRt = _toastPanel.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0.5f, 0.1f);
            toastRt.anchorMax = new Vector2(0.5f, 0.1f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.sizeDelta = new Vector2(520f, 52f);
            _toastText = CampUiRuntime.CreateText(_toastPanel.transform, "", 17, FontStyle.Normal);
            CampUiRuntime.Stretch(_toastText.rectTransform, 16f, 8f, -16f, -8f);
            _toastPanel.SetActive(false);
        }

        public void ShowToast(string message)
        {
            if (_toastPanel == null)
                return;

            _toastText.text = message;
            _toastPanel.SetActive(true);
            transform.SetAsLastSibling();
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.2f);
        }

        void HideToast()
        {
            if (_toastPanel != null)
                _toastPanel.SetActive(false);
        }
    }
}
