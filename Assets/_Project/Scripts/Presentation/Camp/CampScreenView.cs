using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 营地主界面。
    /// 鼠标热区对齐【背景图中画出的建筑/图标】；悬停时才显示可互动建筑精灵并高亮。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampScreenView : MonoBehaviour
    {
        /// <summary>归一化热区：原点在屏幕左下，(xmin,ymin)-(xmax,ymax)，相对整屏。</summary>
        readonly struct NormRect
        {
            public readonly float XMin;
            public readonly float YMin;
            public readonly float XMax;
            public readonly float YMax;

            public NormRect(float xMin, float yMin, float xMax, float yMax)
            {
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
            }

            public Vector2 Center => new((XMin + XMax) * 0.5f, (YMin + YMax) * 0.5f);
            public float Width => XMax - XMin;
            public float Height => YMax - YMin;
        }

        // —— 热区严格按用户标注红框（相对整屏，原点左下）——
        static readonly NormRect ZoneChampion = new(0.0605f, 0.2795f, 0.2275f, 0.5035f);
        static readonly NormRect ZoneAltar = new(0.2471f, 0.2934f, 0.3545f, 0.4757f);
        static readonly NormRect ZonePortal = new(0.3916f, 0.3073f, 0.5127f, 0.5538f);
        static readonly NormRect ZoneTarget = new(0.5439f, 0.2882f, 0.6387f, 0.4913f);
        static readonly NormRect ZoneShop = new(0.6484f, 0.2726f, 0.7783f, 0.4948f);
        static readonly NormRect ZoneLibrary = new(0.8262f, 0.2396f, 0.9785f, 0.5451f);

        // 左下角书 / 卡牌 / 设置
        static readonly NormRect ZoneBtnLibrary = new(0.0176f, 0.0156f, 0.0762f, 0.1146f);
        static readonly NormRect ZoneBtnCards = new(0.0879f, 0.0191f, 0.1445f, 0.1215f);
        static readonly NormRect ZoneBtnSettings = new(0.1582f, 0.0191f, 0.2148f, 0.1163f);

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
        Action _onLibrary;
        Action _onCollection;
        Action _onSettings;
        Action<string> _onFeatureComingSoon;

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(
            Action onChampionCamp,
            Action onPortal,
            Action onTalentAltar,
            Action onMetaShop,
            Action onTrainingGround,
            BattleUiIconCatalogSO icons = null,
            Action<string> onFeatureComingSoon = null,
            Action onLibrary = null,
            Action onSettings = null,
            Action onCollection = null)
        {
            if (icons != null)
                uiIcons = icons;

            _onChampionCamp = onChampionCamp;
            _onPortal = onPortal;
            _onTalentAltar = onTalentAltar;
            _onMetaShop = onMetaShop;
            _onTrainingGround = onTrainingGround;
            _onFeatureComingSoon = onFeatureComingSoon;
            _onLibrary = onLibrary;
            _onSettings = onSettings;
            _onCollection = onCollection;

            _built = false;
            EnsureBuilt();
        }

        public void Show(int accountGold = 0)
        {
            EnsureBuilt();
            RefreshAccountGold(accountGold);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (gameObject != null)
                gameObject.SetActive(false);
        }

        public void RefreshAccountGold(int accountGold)
        {
            if (_accountGoldText != null)
                _accountGoldText.text = accountGold.ToString();
        }

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
            bg.raycastTarget = false;
            if (bg.sprite == null)
                bg.color = new Color(0.05f, 0.07f, 0.12f, 1f);

            BuildTopBar(transform);
            BuildBuildingHotZones(transform);
            BuildCornerHotZones(transform);
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
            var bar = CampUiRuntime.CreateRect("TopBar", parent);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 1f);
            barRt.anchorMax = new Vector2(1f, 1f);
            barRt.pivot = new Vector2(0.5f, 1f);
            barRt.sizeDelta = new Vector2(0f, 56f);

            var goldRow = CampUiRuntime.CreateRect("AccountGold", bar.transform);
            var goldRt = goldRow.GetComponent<RectTransform>();
            goldRt.anchorMin = new Vector2(1f, 0.5f);
            goldRt.anchorMax = new Vector2(1f, 0.5f);
            goldRt.pivot = new Vector2(1f, 0.5f);
            goldRt.anchoredPosition = new Vector2(-20f, 0f);
            goldRt.sizeDelta = new Vector2(220f, 40f);

            _accountGoldText = CampUiRuntime.CreateText(goldRow.transform, "0", 20, FontStyle.Bold,
                TextAnchor.MiddleRight);
            _accountGoldText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _accountGoldText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _accountGoldText.rectTransform.offsetMin = Vector2.zero;
            _accountGoldText.rectTransform.offsetMax = new Vector2(-40f, 0f);
            _accountGoldText.color = new Color(0.95f, 0.9f, 0.7f, 1f);

            var goldIcon = uiIcons != null ? uiIcons.CampGoldIcon : null;
            if (goldIcon != null)
            {
                var icon = CampUiRuntime.CreateImage("CampGoldIcon", goldRow.transform, Color.white);
                icon.sprite = goldIcon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = new Vector2(1f, 0.5f);
                iconRt.anchorMax = new Vector2(1f, 0.5f);
                iconRt.pivot = new Vector2(1f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(32f, 32f);
            }
        }

        void BuildBuildingHotZones(Transform parent)
        {
            var root = CampUiRuntime.CreateRect("BuildingHotZones", parent);
            CampUiRuntime.StretchFull(root.GetComponent<RectTransform>());

            // 后添加的在上层；右侧重叠时优先右侧建筑
            CreateBuildingHotZone(root.transform, "ChampionCamp", ZoneChampion,
                uiIcons?.ChampionCampBuilding, _onChampionCamp);
            CreateBuildingHotZone(root.transform, "TalentAltar", ZoneAltar,
                uiIcons?.TalentAltarBuilding, _onTalentAltar);
            CreateBuildingHotZone(root.transform, "Portal", ZonePortal,
                uiIcons?.PortalBuilding, _onPortal);
            CreateBuildingHotZone(root.transform, "TrainingGround", ZoneTarget,
                uiIcons?.TrainingGroundBuilding, _onTrainingGround);
            CreateBuildingHotZone(root.transform, "MerchantCamp", ZoneShop,
                uiIcons?.MerchantCampBuilding, _onMetaShop);
            CreateBuildingHotZone(root.transform, "Library", ZoneLibrary,
                uiIcons?.LibraryBuilding,
                () =>
                {
                    if (_onLibrary != null)
                        _onLibrary.Invoke();
                    else
                        _onFeatureComingSoon?.Invoke("图书馆");
                });
        }

        /// <summary>
        /// 热区 = 背景图建筑包围盒（隐形矩形）；
        /// 子节点 Visual = 可互动建筑精灵，默认隐藏，悬停弹出盖住背景建筑。
        /// </summary>
        void CreateBuildingHotZone(
            Transform parent,
            string id,
            NormRect zone,
            Sprite visualSprite,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyNormRect(rt, zone);

            // 完全透明热区：只负责鼠标检测，玩家不可见
            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var visualGo = CampUiRuntime.CreateRect("Visual", go.transform);
            var visualRt = visualGo.GetComponent<RectTransform>();
            // 与热区同范围，略向外扩，悬停放大后盖住背景建筑
            visualRt.anchorMin = Vector2.zero;
            visualRt.anchorMax = Vector2.one;
            visualRt.offsetMin = new Vector2(-8f, -4f);
            visualRt.offsetMax = new Vector2(8f, 12f);
            visualRt.pivot = new Vector2(0.5f, 0f);

            var visualImg = visualGo.AddComponent<Image>();
            visualImg.color = Color.white;
            visualImg.raycastTarget = false;
            visualImg.preserveAspect = true;
            if (visualSprite != null)
                visualImg.sprite = visualSprite;
            else
                Debug.LogWarning($"[CampScreen] 缺少建筑贴图：{id}");

            var group = visualGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(visualRt, group);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void BuildCornerHotZones(Transform parent)
        {
            // 独立置顶，确保不被建筑热区挡住
            var bar = CampUiRuntime.CreateRect("CornerHotZones", parent);
            CampUiRuntime.StretchFull(bar.GetComponent<RectTransform>());
            var canvas = bar.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 80;
            bar.AddComponent<GraphicRaycaster>();

            CreateSimpleHotZone(bar.transform, "LibraryHotspot", ZoneBtnLibrary,
                () =>
                {
                    if (_onLibrary != null)
                        _onLibrary.Invoke();
                    else
                        _onFeatureComingSoon?.Invoke("图书馆");
                });
            CreateSimpleHotZone(bar.transform, "CardsHotspot", ZoneBtnCards,
                () =>
                {
                    if (_onCollection != null)
                        _onCollection.Invoke();
                    else
                        _onFeatureComingSoon?.Invoke("收藏");
                });
            CreateSimpleHotZone(bar.transform, "SettingsHotspot", ZoneBtnSettings,
                () =>
                {
                    if (_onSettings != null)
                        _onSettings.Invoke();
                    else
                        _onFeatureComingSoon?.Invoke("设置");
                });
        }

        void CreateSimpleHotZone(Transform parent, string id, NormRect zone, Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyNormRect(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        static void ApplyNormRect(RectTransform rt, NormRect zone)
        {
            rt.anchorMin = new Vector2(zone.XMin, zone.YMin);
            rt.anchorMax = new Vector2(zone.XMax, zone.YMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
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
