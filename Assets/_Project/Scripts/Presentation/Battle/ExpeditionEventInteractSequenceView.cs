using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>远征事件选项后的分步视觉反馈：扣血、选人、选牌、提示消息。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionEventInteractSequenceView : MonoBehaviour
    {
        const float CardScale = 0.92f;
        const int CardsPerRow = 5;
        const float CharacterPortraitSize = 168f;
        const float CharacterCardWidth = 228f;
        const float CharacterCardHeight = 318f;
        const float MessageAutoAdvanceSeconds = 2.2f;
        const float HpLossAnimSeconds = 1.4f;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _icons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        RectTransform _contentArea;
        Text _promptText;
        RectTransform _characterRow;
        RectTransform _cardScrollArea;
        ScrollRect _cardScroll;
        RectTransform _cardGrid;
        Button _confirmButton;
        Text _confirmLabel;

        bool _built;
        int _displayedStepIndex = -1;
        string _selectedCharacterId = "";
        string _selectedCardKey = "";
        readonly HashSet<string> _selectedCardKeys = new();
        bool _stepBusy;
        bool _awaitingContinue;
        readonly List<GameObject> _dynamicObjects = new();
        readonly Dictionary<string, CardView> _cardViews = new();
        readonly Dictionary<string, CardType> _cardTypesByKey = new();
        readonly Dictionary<string, Text> _portraitHpTexts = new();
        readonly Dictionary<string, Text> _portraitFloaters = new();

        public void Initialize(
            BattleSession session,
            Transform parent,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO icons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _icons = icons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var interaction = _session.Expedition.Run.EventInteraction;
            var show = _session.Expedition.Run.Phase == ExpeditionPhase.EventInteraction
                       && interaction != null
                       && interaction.StepIndex < interaction.Steps.Count;

            SetVisible(show);
            if (!show)
            {
                ResetTransientState();
                return;
            }

            _root.SetAsLastSibling();

            if (_stepBusy && !_awaitingContinue)
                return;

            if (interaction.StepIndex != _displayedStepIndex)
            {
                _displayedStepIndex = interaction.StepIndex;
                _selectedCharacterId = "";
                _selectedCardKey = "";
                _selectedCardKeys.Clear();
                RebuildForCurrentStep(interaction);
            }
            else
            {
                UpdateConfirmButton(interaction);
            }
        }

        void RebuildForCurrentStep(ExpeditionEventInteractionState interaction)
        {
            ClearContent();
            _portraitHpTexts.Clear();
            _portraitFloaters.Clear();
            _cardViews.Clear();
            _cardTypesByKey.Clear();

            var step = interaction.Steps[interaction.StepIndex];
            switch (step.Kind)
            {
                case ExpeditionEventStepKind.ShowTeamHpLoss:
                    _promptText.text = step.PercentHpDelta > 0 || step.FlatHpDelta > 0
                        ? "全队恢复生命"
                        : "全队受到伤害";
                    BuildCharacterRow(selectable: false);
                    StartCoroutine(PlayTeamHpLossSequence(step));
                    break;
                case ExpeditionEventStepKind.PickMemberHpLoss:
                    _promptText.text = "选择一名队员承受伤害";
                    BuildCharacterRow(selectable: true, onPick: PickMemberForHpLoss);
                    break;
                case ExpeditionEventStepKind.PickMemberForBuff:
                    if (interaction.StepIndex > 0 &&
                        interaction.Steps[interaction.StepIndex - 1].Kind == ExpeditionEventStepKind.PickMemberHpLoss &&
                        !string.IsNullOrEmpty(interaction.SelectedCharacterId))
                    {
                        _promptText.text = step.PersonalAttackBonus > 0
                            ? $"特训完成：{FindMember(interaction.SelectedCharacterId)?.DisplayName} 攻击 +{step.PersonalAttackBonus}"
                            : $"特训完成：{FindMember(interaction.SelectedCharacterId)?.DisplayName} 攻击 +2";
                        BuildCharacterRow(selectable: false);
                        StartCoroutine(PlayMemberBuffAcknowledgeSequence(interaction.SelectedCharacterId));
                        break;
                    }

                    _promptText.text = step.PersonalAttackBonus > 0
                        ? $"选择一名队员获得攻击 +{step.PersonalAttackBonus}"
                        : "选择一名队员获得攻击 +2";
                    BuildCharacterRow(selectable: true, onPick: PickMemberForBuff);
                    break;
                case ExpeditionEventStepKind.PickCardRemove:
                    _promptText.text = "选择一张卡牌移除";
                    BuildCardGrid(interaction, step);
                    break;
                case ExpeditionEventStepKind.PickCardUpgrade:
                    _promptText.text = "选择一张卡牌强化";
                    BuildCardGrid(interaction, step);
                    break;
                case ExpeditionEventStepKind.PickTwoCardsForFusion:
                    _promptText.text = "选择两张同类型卡牌进行融合（可跨角色）";
                    BuildCardGrid(interaction, step);
                    break;
                case ExpeditionEventStepKind.ShowMessage:
                    _promptText.text = string.IsNullOrEmpty(step.Message) ? "……" : step.Message;
                    _characterRow.gameObject.SetActive(false);
                    _cardScrollArea.gameObject.SetActive(false);
                    _awaitingContinue = true;
                    break;
            }

            UpdateConfirmButton(interaction);
        }

        void PickMemberForHpLoss(string characterId)
        {
            if (_stepBusy || string.IsNullOrEmpty(characterId))
                return;

            var interaction = _session.Expedition.Run.EventInteraction;
            if (interaction == null)
                return;

            _selectedCharacterId = characterId;
            _stepBusy = true;
            HighlightSelectedCharacter();
            StartCoroutine(PlayMemberHpLossSequence(interaction.Steps[interaction.StepIndex], characterId));
        }

        void PickMemberForBuff(string characterId)
        {
            if (_stepBusy || string.IsNullOrEmpty(characterId))
                return;

            _selectedCharacterId = characterId;
            _stepBusy = true;
            HighlightSelectedCharacter();
            CompleteStep(_selectedCharacterId, null);
        }

        void BuildCharacterRow(bool selectable, System.Action<string> onPick = null)
        {
            _characterRow.gameObject.SetActive(true);
            _cardScrollArea.gameObject.SetActive(false);
            _confirmButton.gameObject.SetActive(false);

            foreach (var member in _session.Expedition.Run.Party)
            {
                var card = CreateCharacterCard(_characterRow, member);
                var memberId = member.CharacterDefinitionId;

                if (selectable)
                {
                    var btn = card.gameObject.GetComponent<Button>() ?? card.gameObject.AddComponent<Button>();
                    btn.targetGraphic = card.GetComponent<Image>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => onPick?.Invoke(memberId));
                }
            }
        }

        RectTransform CreateCharacterCard(RectTransform parent, PartyMemberSnapshot member)
        {
            var go = new GameObject("MemberCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CharacterCardWidth, CharacterCardHeight);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = CharacterCardWidth;
            le.preferredHeight = CharacterCardHeight;

            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            bg.raycastTarget = true;

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go.transform, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0.5f, 1f);
            portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.anchoredPosition = new Vector2(0f, -10f);
            portraitRt.sizeDelta = new Vector2(CharacterPortraitSize, CharacterPortraitSize);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = _characterVisuals?.GetPortraitReference(member.CharacterDefinitionId)
                ?? _characterVisuals?.GetPortrait(member.CharacterDefinitionId);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.35f, 0.38f, 0.45f, 1f);

            var hpGo = new GameObject("Hp", typeof(RectTransform), typeof(Text));
            hpGo.transform.SetParent(go.transform, false);
            var hpRt = hpGo.GetComponent<RectTransform>();
            hpRt.anchorMin = new Vector2(0.08f, 0.14f);
            hpRt.anchorMax = new Vector2(0.92f, 0.24f);
            hpRt.offsetMin = Vector2.zero;
            hpRt.offsetMax = Vector2.zero;
            var hpText = hpGo.GetComponent<Text>();
            StyleText(hpText, 16, TextAnchor.MiddleCenter);
            GetMemberDisplayHp(member, out var displayHp, out var displayMaxHp);
            hpText.text = $"生命 {displayHp}/{displayMaxHp}";
            _portraitHpTexts[member.CharacterDefinitionId] = hpText;

            var floaterGo = new GameObject("DamageFloater", typeof(RectTransform), typeof(Text), typeof(Outline));
            floaterGo.transform.SetParent(portraitGo.transform, false);
            var floaterRt = floaterGo.GetComponent<RectTransform>();
            floaterRt.anchorMin = new Vector2(0.5f, 0.5f);
            floaterRt.anchorMax = new Vector2(0.5f, 0.5f);
            floaterRt.pivot = new Vector2(0.5f, 0.5f);
            floaterRt.anchoredPosition = new Vector2(0f, 24f);
            floaterRt.sizeDelta = new Vector2(120f, 44f);
            var floater = floaterGo.GetComponent<Text>();
            StyleText(floater, 28, TextAnchor.MiddleCenter);
            floater.color = new Color(1f, 0.28f, 0.28f, 1f);
            floater.gameObject.SetActive(false);
            var outline = floaterGo.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            _portraitFloaters[member.CharacterDefinitionId] = floater;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.06f, 0f);
            nameRt.anchorMax = new Vector2(0.94f, 0.12f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 16, TextAnchor.MiddleCenter);
            nameText.text = member.DisplayName;

            _dynamicObjects.Add(go);
            return rt;
        }

        void HighlightSelectedCharacter()
        {
            foreach (Transform child in _characterRow)
            {
                var img = child.GetComponent<Image>();
                if (img == null)
                    continue;

                var isSelected = false;
                foreach (var member in _session.Expedition.Run.Party)
                {
                    if (member.CharacterDefinitionId == _selectedCharacterId)
                    {
                        var nameText = child.Find("Name")?.GetComponent<Text>();
                        isSelected = nameText != null && nameText.text == member.DisplayName;
                        break;
                    }
                }

                img.color = isSelected
                    ? new Color(0.22f, 0.24f, 0.32f, 0.98f)
                    : new Color(0.12f, 0.13f, 0.18f, 0.96f);
            }
        }

        void BuildCardGrid(ExpeditionEventInteractionState interaction, ExpeditionEventInteractionStep step)
        {
            _characterRow.gameObject.SetActive(false);
            _cardScrollArea.gameObject.SetActive(true);
            _confirmButton.gameObject.SetActive(true);

            ClearChildren(_cardGrid);

            if (_cardPrefab == null)
                return;

            var cards = ExpeditionRunDeckMutations.ListSelectableCards(_session.Expedition.Config, _session.Expedition.Run);
            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;

            foreach (var entry in cards)
            {
                if (entry?.Template == null)
                    continue;

                _definitions.TryGetValue(entry.Template.DefinitionId, out var definition);
                var holder = new GameObject("CardHolder", typeof(RectTransform), typeof(LayoutElement));
                holder.transform.SetParent(_cardGrid, false);
                var holderLe = holder.GetComponent<LayoutElement>();
                holderLe.preferredWidth = cardWidth + 8f;
                holderLe.preferredHeight = cardHeight + 8f;

                var view = Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                var preview = CardVisualResolver.CreatePreviewInstance(
                    entry.Template.DefinitionId,
                    entry.Template.OwnerCharacterId,
                    entry.Template.DisplayName,
                    definition);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var key = entry.Key;
                var selected = step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion
                    ? _selectedCardKeys.Contains(key)
                    : key == _selectedCardKey;
                view.BindWithCard(
                    preview,
                    visual,
                    selected: selected,
                    polluted: false,
                    interactable: true,
                    orderBadge: "",
                    statsLine: BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions),
                    uiIcons: _icons,
                    characterVisuals: _characterVisuals,
                    onClick: _ => SelectCard(key),
                    onHoverEnter: null,
                    onHoverExit: null);

                _cardViews[key] = view;
                _cardTypesByKey[key] = entry.Template.CardType;
                _dynamicObjects.Add(holder);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardGrid);
        }

        void SelectCard(string key)
        {
            if (_stepBusy || string.IsNullOrEmpty(key))
                return;

            var interaction = _session.Expedition.Run.EventInteraction;
            if (interaction == null)
                return;

            var step = interaction.Steps[interaction.StepIndex];
            if (step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion)
            {
                if (_selectedCardKeys.Contains(key))
                    _selectedCardKeys.Remove(key);
                else if (_selectedCardKeys.Count < 2)
                    _selectedCardKeys.Add(key);
            }
            else
            {
                _selectedCardKey = _selectedCardKey == key ? "" : key;
            }

            foreach (var pair in _cardViews)
            {
                var selected = step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion
                    ? _selectedCardKeys.Contains(pair.Key)
                    : pair.Key == _selectedCardKey;
                pair.Value.SetSelected(selected);
            }

            UpdateConfirmButton(interaction);
        }

        void UpdateConfirmButton(ExpeditionEventInteractionState interaction)
        {
            if (_confirmButton == null || interaction == null)
                return;

            var step = interaction.Steps[interaction.StepIndex];
            if (_awaitingContinue || step.Kind == ExpeditionEventStepKind.ShowMessage)
            {
                _confirmButton.gameObject.SetActive(true);
                _confirmLabel.text = "继续";
                _confirmButton.interactable = !_stepBusy;
                return;
            }

            var needsCard = step.Kind is ExpeditionEventStepKind.PickCardRemove
                or ExpeditionEventStepKind.PickCardUpgrade
                or ExpeditionEventStepKind.PickTwoCardsForFusion;

            if (!needsCard)
            {
                _confirmButton.gameObject.SetActive(false);
                return;
            }

            _confirmButton.gameObject.SetActive(true);
            _confirmLabel.text = step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion
                ? "确认融合"
                : "确认";

            if (step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion)
            {
                _confirmButton.interactable = !_stepBusy
                                              && _selectedCardKeys.Count == 2
                                              && TryGetSelectedFusionType(out _);
                return;
            }

            _confirmButton.interactable = !string.IsNullOrEmpty(_selectedCardKey) && !_stepBusy;
        }

        bool TryGetSelectedFusionType(out CardType cardType)
        {
            cardType = default;
            if (_selectedCardKeys.Count != 2)
                return false;

            CardType? firstType = null;
            foreach (var key in _selectedCardKeys)
            {
                if (!_cardTypesByKey.TryGetValue(key, out var type))
                    continue;

                if (firstType == null)
                    firstType = type;
                else if (firstType.Value != type)
                    return false;
            }

            if (firstType == null)
                return false;

            cardType = firstType.Value;
            return true;
        }

        IEnumerator PlayTeamHpLossSequence(ExpeditionEventInteractionStep step)
        {
            _stepBusy = true;
            _awaitingContinue = false;
            UpdateConfirmButton(_session.Expedition.Run.EventInteraction);
            yield return new WaitForSeconds(0.35f);

            if (!string.IsNullOrEmpty(step.TargetCharacterId))
            {
                var member = FindMember(step.TargetCharacterId);
                if (member != null)
                {
                    var delta = CalcHpChange(member, step);
                    ShowPortraitHpChange(member.CharacterDefinitionId, delta, step);
                }
            }
            else
            {
                foreach (var member in _session.Expedition.Run.Party)
                {
                    var delta = CalcHpChange(member, step);
                    ShowPortraitHpChange(member.CharacterDefinitionId, delta, step);
                }
            }

            yield return new WaitForSeconds(HpLossAnimSeconds);
            RevealContinueButton();
        }

        IEnumerator PlayMemberHpLossSequence(ExpeditionEventInteractionStep step, string characterId)
        {
            _stepBusy = true;
            _awaitingContinue = false;
            UpdateConfirmButton(_session.Expedition.Run.EventInteraction);
            yield return new WaitForSeconds(0.15f);

            var member = FindMember(characterId);
            if (member != null)
            {
                var delta = CalcHpChange(member, step);
                ShowPortraitHpChange(characterId, delta, step);
            }

            yield return new WaitForSeconds(HpLossAnimSeconds);
            RevealContinueButton();
        }

        IEnumerator PlayMemberBuffAcknowledgeSequence(string characterId)
        {
            _stepBusy = true;
            _awaitingContinue = false;
            if (!string.IsNullOrEmpty(characterId))
                _selectedCharacterId = characterId;
            UpdateConfirmButton(_session.Expedition.Run.EventInteraction);
            yield return new WaitForSeconds(0.8f);
            RevealContinueButton();
        }

        void RevealContinueButton()
        {
            _stepBusy = false;
            _awaitingContinue = true;
            var interaction = _session.Expedition.Run.EventInteraction;
            if (interaction != null)
                UpdateConfirmButton(interaction);
        }

        void ShowPortraitHpChange(string characterId, int delta, ExpeditionEventInteractionStep step)
        {
            if (delta <= 0)
                return;

            var isHeal = step.PercentHpDelta > 0 || step.FlatHpDelta > 0;

            if (_portraitFloaters.TryGetValue(characterId, out var floater) && floater != null)
            {
                floater.text = isHeal ? $"+{delta}" : $"-{delta}";
                floater.color = isHeal
                    ? new Color(0.35f, 0.95f, 0.45f, 1f)
                    : new Color(0.95f, 0.35f, 0.35f, 1f);
                floater.gameObject.SetActive(true);
                StartCoroutine(AnimateFloater(floater));
            }

            if (_portraitHpTexts.TryGetValue(characterId, out var hpText) && hpText != null)
            {
                var member = FindMember(characterId);
                if (member != null)
                {
                    GetMemberDisplayHp(member, out var currentHp, out var maxHp);
                    var previewHp = isHeal
                        ? Mathf.Min(maxHp, currentHp + delta)
                        : Mathf.Max(1, currentHp - delta);
                    hpText.text = $"生命 {previewHp}/{maxHp}";
                }
            }
        }

        void GetMemberDisplayHp(PartyMemberSnapshot member, out int hp, out int maxHp)
        {
            var run = _session.Expedition.Run;
            ExpeditionPartyStatsRules.GetDisplayHp(
                member,
                run.Party,
                run.Relics,
                run.RelicGrowthTiers,
                out hp,
                out maxHp);
        }

        IEnumerator AnimateFloater(Text floater)
        {
            var rt = floater.rectTransform;
            var start = rt.anchoredPosition;
            var end = start + new Vector2(0f, 48f);
            var duration = 0.85f;
            var elapsed = 0f;
            var startColor = floater.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, t);
                floater.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t * 0.35f);
                yield return null;
            }

            floater.gameObject.SetActive(false);
            rt.anchoredPosition = start;
            floater.color = startColor;
        }

        int CalcHpChange(PartyMemberSnapshot member, ExpeditionEventInteractionStep step)
        {
            if (member == null)
                return 0;

            GetMemberDisplayHp(member, out var currentHp, out var maxHp);

            if (step.FlatHpDelta != 0)
                return Mathf.Max(1, Mathf.Abs(step.FlatHpDelta));

            if (step.PercentHpDelta != 0)
            {
                var basis = step.PercentHpDelta > 0 ? maxHp : currentHp;
                return Mathf.Max(1, basis * Mathf.Abs(step.PercentHpDelta) / 100);
            }

            return 0;
        }

        PartyMemberSnapshot FindMember(string characterId)
        {
            foreach (var member in _session.Expedition.Run.Party)
            {
                if (member.CharacterDefinitionId == characterId)
                    return member;
            }

            return null;
        }

        void OnConfirmClicked()
        {
            if (_stepBusy)
                return;

            var interaction = _session.Expedition.Run.EventInteraction;
            if (interaction == null)
                return;

            var step = interaction.Steps[interaction.StepIndex];
            if (_awaitingContinue || step.Kind == ExpeditionEventStepKind.ShowMessage)
            {
                var characterId = !string.IsNullOrEmpty(_selectedCharacterId)
                    ? _selectedCharacterId
                    : interaction.SelectedCharacterId;
                _awaitingContinue = false;
                _stepBusy = true;
                CompleteStep(string.IsNullOrEmpty(characterId) ? null : characterId, null);
                return;
            }

            if (step.Kind == ExpeditionEventStepKind.PickTwoCardsForFusion)
            {
                if (_selectedCardKeys.Count != 2 || !TryGetSelectedFusionType(out _))
                    return;

                var keys = new List<string>(_selectedCardKeys);
                _stepBusy = true;
                CompleteStep(null, keys[0], keys[1]);
                return;
            }

            if (string.IsNullOrEmpty(_selectedCardKey))
                return;

            _stepBusy = true;
            CompleteStep(null, _selectedCardKey);
        }

        void CompleteStep(string selectedCharacterId, string selectedCardKey, string selectedSecondCardKey = null)
        {
            if (_session == null)
                return;

            _stepBusy = false;
            _awaitingContinue = false;

            var ok = _session.CompleteEventInteractionStep(
                selectedCharacterId,
                selectedCardKey,
                selectedSecondCardKey);
            if (!ok)
                return;

            StopAllCoroutines();
            _displayedStepIndex = -1;
            _selectedCharacterId = "";
            _selectedCardKey = "";
            _selectedCardKeys.Clear();
            Refresh();
        }

        void ResetTransientState()
        {
            StopAllCoroutines();
            _stepBusy = false;
            _awaitingContinue = false;
            _displayedStepIndex = -1;
            _selectedCharacterId = "";
            _selectedCardKey = "";
            _selectedCardKeys.Clear();
        }

        void ClearContent()
        {
            StopAllCoroutines();
            _stepBusy = false;
            _awaitingContinue = false;

            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
            _cardViews.Clear();
            _cardTypesByKey.Clear();
            _portraitHpTexts.Clear();
            _portraitFloaters.Clear();
            ClearChildren(_characterRow);
            ClearChildren(_cardGrid);
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);

            if (!visible)
                ClearContent();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;

            var go = new GameObject("ExpeditionEventInteractSequence", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(920f, 640f);
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
            promptGo.transform.SetParent(panelGo.transform, false);
            var promptRt = promptGo.GetComponent<RectTransform>();
            promptRt.anchorMin = new Vector2(0.06f, 0.88f);
            promptRt.anchorMax = new Vector2(0.94f, 0.98f);
            promptRt.offsetMin = Vector2.zero;
            promptRt.offsetMax = Vector2.zero;
            _promptText = promptGo.GetComponent<Text>();
            StyleText(_promptText, 24, TextAnchor.MiddleCenter);
            _promptText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _contentArea = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _contentArea.SetParent(panelGo.transform, false);
            _contentArea.anchorMin = new Vector2(0.04f, 0.14f);
            _contentArea.anchorMax = new Vector2(0.96f, 0.86f);
            _contentArea.offsetMin = Vector2.zero;
            _contentArea.offsetMax = Vector2.zero;

            var charRowGo = new GameObject("CharacterRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            charRowGo.transform.SetParent(_contentArea, false);
            _characterRow = charRowGo.GetComponent<RectTransform>();
            _characterRow.anchorMin = new Vector2(0.5f, 0.5f);
            _characterRow.anchorMax = new Vector2(0.5f, 0.5f);
            _characterRow.pivot = new Vector2(0.5f, 0.5f);
            _characterRow.sizeDelta = new Vector2(760f, CharacterCardHeight + 8f);
            var charLayout = charRowGo.GetComponent<HorizontalLayoutGroup>();
            charLayout.spacing = 20f;
            charLayout.childAlignment = TextAnchor.MiddleCenter;
            charLayout.childControlWidth = false;
            charLayout.childControlHeight = false;
            charLayout.childForceExpandWidth = false;
            charLayout.childForceExpandHeight = false;

            _cardScrollArea = new GameObject("CardScrollArea", typeof(RectTransform)).GetComponent<RectTransform>();
            _cardScrollArea.SetParent(_contentArea, false);
            _cardScrollArea.anchorMin = Vector2.zero;
            _cardScrollArea.anchorMax = Vector2.one;
            _cardScrollArea.offsetMin = Vector2.zero;
            _cardScrollArea.offsetMax = Vector2.zero;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(_cardScrollArea, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);
            _cardScroll = scrollGo.GetComponent<ScrollRect>();
            _cardScroll.horizontal = false;
            _cardScroll.vertical = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = Color.clear;
            _cardScroll.viewport = viewportRt;

            var gridGo = new GameObject("CardGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            gridGo.transform.SetParent(viewportGo.transform, false);
            _cardGrid = gridGo.GetComponent<RectTransform>();
            _cardGrid.anchorMin = new Vector2(0f, 1f);
            _cardGrid.anchorMax = new Vector2(1f, 1f);
            _cardGrid.pivot = new Vector2(0.5f, 1f);
            _cardGrid.offsetMin = Vector2.zero;
            _cardGrid.offsetMax = Vector2.zero;
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);
            grid.spacing = new Vector2(10f, 12f);
            grid.padding = new RectOffset(16, 16, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardsPerRow;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardScroll.content = _cardGrid;

            var confirmGo = new GameObject("Confirm", typeof(RectTransform), typeof(Image), typeof(Button));
            confirmGo.transform.SetParent(panelGo.transform, false);
            var confirmRt = confirmGo.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.5f, 0.04f);
            confirmRt.anchorMax = new Vector2(0.5f, 0.04f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.sizeDelta = new Vector2(220f, 52f);
            confirmGo.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.95f);
            _confirmButton = confirmGo.GetComponent<Button>();
            _confirmButton.onClick.AddListener(OnConfirmClicked);

            var confirmTextGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            confirmTextGo.transform.SetParent(confirmGo.transform, false);
            var confirmTextRt = confirmTextGo.GetComponent<RectTransform>();
            confirmTextRt.anchorMin = Vector2.zero;
            confirmTextRt.anchorMax = Vector2.one;
            confirmTextRt.offsetMin = Vector2.zero;
            confirmTextRt.offsetMax = Vector2.zero;
            _confirmLabel = confirmTextGo.GetComponent<Text>();
            StyleText(_confirmLabel, 20, TextAnchor.MiddleCenter);
            _confirmLabel.text = "确认";

            _root.gameObject.SetActive(false);
            _root.SetAsLastSibling();
        }

        static void ClearChildren(RectTransform parent)
        {
            if (parent == null)
                return;

            for (var i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        static void StyleText(Text text, int size, TextAnchor anchor)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }
    }
}
