using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>背包角色卡：拖到另一角色上交换站位（仅非战斗）。</summary>
    public sealed class InventoryPartyMemberDragDrop : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        BattleInventoryPanelView _host;
        int _partyIndex = -1;
        CanvasGroup _group;
        RectTransform _dragGhost;
        Canvas _rootCanvas;

        public int PartyIndex => _partyIndex;

        public void Bind(BattleInventoryPanelView host, int partyIndex)
        {
            _host = host;
            _partyIndex = partyIndex;

            // 子节点 Text/Image 默认会拦截射线，拖放到卡牌本身
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null)
                    continue;
                graphic.raycastTarget = graphic.transform == transform;
            }

            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_host == null || _partyIndex < 0)
                return;

            _rootCanvas = GetComponentInParent<Canvas>();
            if (_group != null)
                _group.blocksRaycasts = false;

            CreateGhost();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragGhost == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragGhost.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var local);
            _dragGhost.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_group != null)
                _group.blocksRaycasts = true;

            DestroyGhost();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (_host == null || _partyIndex < 0 || eventData?.pointerDrag == null)
                return;

            var source = eventData.pointerDrag.GetComponent<InventoryPartyMemberDragDrop>();
            if (source == null || source._partyIndex < 0 || source._partyIndex == _partyIndex)
                return;

            _host.HandlePartyMemberDrop(source._partyIndex, _partyIndex);
        }

        void CreateGhost()
        {
            DestroyGhost();
            if (_rootCanvas == null)
                return;

            var go = new GameObject("PartyDragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(_rootCanvas.transform, false);
            _dragGhost = go.GetComponent<RectTransform>();
            _dragGhost.sizeDelta = ((RectTransform)transform).rect.size;

            var sourceImage = GetComponent<Image>();
            var ghostImage = go.GetComponent<Image>();
            ghostImage.sprite = sourceImage != null ? sourceImage.sprite : null;
            ghostImage.color = new Color(0.2f, 0.22f, 0.28f, 0.72f);
            ghostImage.raycastTarget = false;

            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 0.85f;
        }

        void DestroyGhost()
        {
            if (_dragGhost != null)
            {
                Destroy(_dragGhost.gameObject);
                _dragGhost = null;
            }
        }

        void OnDisable()
        {
            DestroyGhost();
            if (_group != null)
                _group.blocksRaycasts = true;
        }
    }
}
