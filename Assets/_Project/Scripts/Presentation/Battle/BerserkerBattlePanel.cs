using System.Collections.Generic;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>Real-discard recall choices. Banner payment is automatic.</summary>
    public sealed class BerserkerBattlePanel : MonoBehaviour
    {
        GameObject _recall;
        Text _title;
        Button _finish;
        RectTransform _row;
        readonly List<CardView> _cards=new();
        BattleSession _session;
        CardView _prefab;
        CardVisualCatalogSO _visuals;
        CharacterVisualCatalogSO _characters;
        BattleUiIconCatalogSO _icons;
        Dictionary<string,CardDefinitionSO> _definitions;

        public void Initialize(BattleSession session,CardView prefab,CardVisualCatalogSO visuals,CharacterVisualCatalogSO characters,BattleUiIconCatalogSO icons,Dictionary<string,CardDefinitionSO> definitions)
        {
            _session=session;_prefab=prefab;_visuals=visuals;_characters=characters;_icons=icons;_definitions=definitions;
            _recall=Panel("BerserkerRecall",transform,Vector2.zero,Vector2.one,new Color(0,0,0,.88f));
            ExpeditionMapOverlayLayer.MountToFront(_recall.GetComponent<RectTransform>(),transform);
            var frame=Panel("RecallFrame",_recall.transform,new Vector2(.06f,.08f),new Vector2(.94f,.92f),Color.white);
            Skin(frame,_icons.UiInformationPlate);
            _title=Text(frame.transform,"",24);SetRect(_title.rectTransform,new Vector2(.08f,.81f),new Vector2(.92f,.92f));
            var scroll=Panel("Scroll",frame.transform,new Vector2(.08f,.21f),new Vector2(.92f,.78f),Color.clear);
            var mask=scroll.AddComponent<RectMask2D>();var rect=scroll.AddComponent<ScrollRect>();rect.vertical=false;rect.horizontal=true;
            var content=new GameObject("Cards",typeof(RectTransform),typeof(HorizontalLayoutGroup),typeof(ContentSizeFitter));content.transform.SetParent(scroll.transform,false);_row=content.GetComponent<RectTransform>();
            _row.anchorMin=Vector2.zero;_row.anchorMax=new Vector2(0,1);_row.pivot=new Vector2(0,.5f);_row.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<HorizontalLayoutGroup>();layout.padding=new RectOffset(28,28,24,24);layout.spacing=24;layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.childAlignment=TextAnchor.MiddleLeft;
            content.GetComponent<ContentSizeFitter>().horizontalFit=ContentSizeFitter.FitMode.PreferredSize;rect.viewport=scroll.GetComponent<RectTransform>();rect.content=_row;
            _finish=Button(frame.transform,"完成选择",()=>_session.ChooseBerserkerRecall(0));SetRect(_finish.GetComponent<RectTransform>(),new Vector2(.39f,.08f),new Vector2(.61f,.18f));
            _recall.SetActive(false);
        }
        public void Refresh()
        {
            var state=_session?.Engine?.State;
            if(state==null||_session.PresentationLocked||state.Outcome!=Grimhand.Battle.Model.BattleOutcome.Ongoing)
            {_recall.SetActive(false);return;}
            var pending=BerserkerRules.PendingRecall(state);_recall.SetActive(pending!=null);
            if(pending==null)return;
            _recall.transform.SetAsLastSibling();ExpeditionMapOverlayLayer.BringLayerToFront(transform);
            _finish.gameObject.SetActive(pending.AllowSkip);
            _title.text=$"{pending.Label}：选择取回的卡牌（还可选{pending.Remaining}张，手牌空位{state.Config.HandLimit-state.PlayerHand.Count}）";
            var candidates=BerserkerRules.RecallCandidates(state,pending);
            while(_cards.Count<candidates.Count){var view=Instantiate(_prefab,_row);CardView.ApplyHandPresentationScale(view,1);_cards.Add(view);}
            for(var i=0;i<_cards.Count;i++)
            {
                var view=_cards[i];view.gameObject.SetActive(i<candidates.Count);if(i>=candidates.Count)continue;
                var card=candidates[i];var visual=CardVisualResolver.Resolve(card,_visuals,_characters,_definitions);
                view.BindWithCard(card,visual,false,false,true,null,BattleUiFormatters.BuildCardStatsLine(null,null,card),_icons,_characters,id=>_session.ChooseBerserkerRecall(id),null,null,pending.FreeInHand?0:card.Cost);
            }
        }
        static GameObject Panel(string name,Transform parent,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);SetRect(go.GetComponent<RectTransform>(),min,max);go.GetComponent<Image>().color=color;return go;}
        static void SetRect(RectTransform rt,Vector2 min,Vector2 max){rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=rt.offsetMax=Vector2.zero;}
        static Text Text(Transform parent,string value,int size)
        {var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.text=value;t.raycastTarget=false;return t;}
        static void Skin(GameObject go,Sprite sprite)
        {var image=go.GetComponent<Image>();image.sprite=sprite;image.color=Color.white;image.type=sprite!=null&&sprite.border.sqrMagnitude>0?Image.Type.Sliced:Image.Type.Simple;}
        Button Button(Transform parent,string value,UnityEngine.Events.UnityAction click)
        {var go=Panel(value,parent,Vector2.zero,Vector2.one,Color.white);Skin(go,_icons.UiButton3);var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<Image>();b.onClick.AddListener(click);var t=Text(go.transform,value,17);SetRect(t.rectTransform,new Vector2(.10f,.16f),new Vector2(.90f,.84f));return b;}
    }
}
