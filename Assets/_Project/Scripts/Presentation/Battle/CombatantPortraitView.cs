using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>单个槽位的立绘动画：idle 循环、位移、pose、受击闪烁、飘字。</summary>
    [DisallowMultipleComponent]
    public sealed class CombatantPortraitView : MonoBehaviour
    {
        static readonly Color DeadTint = new(0.35f, 0.35f, 0.35f, 1f);
        static readonly Color HitFlashTint = new(1f, 0.55f, 0.55f, 1f);
        static readonly Color HealFlashTint = new(0.45f, 1f, 0.55f, 1f);

        const float MoveDuration = 0.35f;
        const float PoseHoldDuration = 1f;
        const float IdleFrameInterval = 0.13f;
        const float HitFlashDuration = 1f;
        const float DamageFloaterFontSize = 28f;
        const float ActionEffectDuration = 0.55f;
        const float ActionEffectAlpha = 0.6f;

        [SerializeField] RectTransform portraitRoot;
        [SerializeField] Image portraitImage;

        CharacterVisualCatalogSO _visuals;
        RectTransform _damageFloaterAnchor;
        Image _actionEffectImage;
        string _characterDefinitionId;
        TeamSide _team;
        Sprite _referenceSprite;
        Vector3 _homeWorldPosition;
        bool _homeCaptured;
        bool _isDead;
        bool _isAnimating;
        bool _awayFromHome;
        bool _idleLoopActive;
        bool _poseFlipX;
        Coroutine _idleRoutine;
        Coroutine _flashRoutine;
        Coroutine _damageHideRoutine;
        Text _damageFloater;

        public bool IsAnimating => _isAnimating;
        public bool IsAwayFromHome => _awayFromHome;
        public bool IsIdleLoopActive => _idleLoopActive;
        public bool IsDeadDisplay => _isDead;
        public string CombatantId { get; private set; }

        public Vector3 HomeWorldPosition
        {
            get
            {
                CaptureHomeIfNeeded();
                return _homeWorldPosition;
            }
        }

        public Vector3 CurrentWorldPosition =>
            portraitRoot != null ? portraitRoot.position : transform.position;

        public Image PortraitImage => portraitImage;
        public RectTransform PortraitRootRect => portraitRoot;

        public void Bind(
            CharacterVisualCatalogSO visuals,
            Image portrait,
            RectTransform root,
            RectTransform damageFloaterAnchor = null,
            TeamSide team = TeamSide.Player)
        {
            _visuals = visuals;
            portraitImage = portrait;
            portraitRoot = root;
            _damageFloaterAnchor = damageFloaterAnchor != null ? damageFloaterAnchor : transform as RectTransform;
            _team = team;
            EnsureDamageFloater();
            CaptureHomeIfNeeded();
        }

        public void SetIdentity(string combatantId, string characterDefinitionId, bool isAlive, TeamSide team)
        {
            var characterChanged = _characterDefinitionId != characterDefinitionId
                                   || CombatantId != combatantId;

            CombatantId = combatantId;
            _characterDefinitionId = characterDefinitionId;
            _team = team;
            RefreshReferenceSprite();

            // 换角/新开局时必须打断上一局残留演出，否则会一直保留旧立绘。
            if (characterChanged)
                ResetInterruptedPresentationState();

            if (isAlive)
            {
                if (_isDead || characterChanged)
                {
                    _isDead = false;
                    if (!_isAnimating && !_idleLoopActive)
                        ApplyIdleStill();
                }
            }
            else if (!_isDead)
            {
                _isDead = true;
                ShowDeathPoseImmediate();
            }
        }

        /// <summary>
        /// 放弃远征 / 隐藏战斗 UI 时协程会被掐断，必须同步清掉演出 flag，
        /// 否则 IsAnimating/IsAwayFromHome 会永久为 true，立绘与 idle 卡死在旧角色。
        /// </summary>
        public void ResetInterruptedPresentationState()
        {
            if (_idleRoutine != null)
            {
                StopCoroutine(_idleRoutine);
                _idleRoutine = null;
            }

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }

            if (_damageHideRoutine != null)
            {
                StopCoroutine(_damageHideRoutine);
                _damageHideRoutine = null;
            }

            _idleLoopActive = false;
            _isAnimating = false;
            _awayFromHome = false;
            _poseFlipX = false;
            RestoreHomePosition();
            SetPortraitVisible(true);
            if (_damageFloater != null)
                _damageFloater.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            ResetInterruptedPresentationState();
        }

        public void BeginPlanningIdle()
        {
            if (_isDead || _isAnimating || portraitImage == null || _visuals == null)
                return;

            // 槽位在战斗结束/领奖等阶段会被禁用，此时启动协程会抛
            // "Coroutine couldn't be started because the game object is inactive!"。
            if (!gameObject.activeInHierarchy)
                return;

            RestoreHomePosition();

            var frames = _visuals.GetIdleAnimationFrames(_characterDefinitionId);
            if (frames.Count <= 1)
            {
                StopIdleLoop();
                ApplyIdleStill();
                return;
            }

            if (_idleLoopActive && _idleRoutine != null)
                return;

            StopIdleLoop();
            _idleRoutine = StartCoroutine(IdleLoop(frames));
        }

        public void StopIdleLoop()
        {
            _idleLoopActive = false;
            if (_idleRoutine != null)
            {
                StopCoroutine(_idleRoutine);
                _idleRoutine = null;
            }

            if (!_isAnimating && !_isDead)
                ApplyIdleStill();
        }

        public IEnumerator MoveToCenter(Vector3 centerWorld)
        {
            if (portraitRoot == null)
                yield break;

            _isAnimating = true;
            _awayFromHome = true;
            StopIdleLoop();
            CaptureHomeIfNeeded();

            // 仅 X 轴移到战场中央，Y 保持与站位时相同水平线。
            var target = new Vector3(centerWorld.x, _homeWorldPosition.y, _homeWorldPosition.z);
            yield return TweenWorldPosition(portraitRoot, target, MoveDuration);
        }

        /// <summary>平移到指定世界坐标（换位等）；保留已捕获的 home，便于结束后归位。</summary>
        public IEnumerator MoveToWorldPosition(Vector3 worldPos)
        {
            if (portraitRoot == null)
                yield break;

            _isAnimating = true;
            _awayFromHome = true;
            StopIdleLoop();
            CaptureHomeIfNeeded();
            yield return TweenWorldPosition(portraitRoot, worldPos, MoveDuration);
        }

        public void SetPortraitVisible(bool visible)
        {
            if (portraitImage != null)
                portraitImage.enabled = visible;
        }

        public void SnapToHomeImmediate()
        {
            if (portraitRoot == null)
                return;

            RestoreHomePosition();
            _awayFromHome = false;
            _isAnimating = false;
            if (!_isDead)
                ApplyIdleStill();
        }

        public void ShowPose(PortraitPoseKind pose)
        {
            StopIdleLoop();
            EnsurePortraitImageStable();
            SetPoseSprite(pose);
        }

        public IEnumerator HoldPose(float duration)
        {
            if (duration <= 0f)
                yield break;

            yield return BattlePresentationSpeed.Wait(duration);
        }

        public IEnumerator MoveToCenterAndPose(Vector3 centerWorld, PortraitPoseKind pose)
        {
            yield return MoveToCenter(centerWorld);
            ShowPose(pose);
            yield return HoldPose(PoseHoldDuration);
        }

        public IEnumerator PlayInPlacePose(PortraitPoseKind pose, float duration)
        {
            if (portraitImage == null)
                yield break;

            _isAnimating = true;
            StopIdleLoop();
            if (!_awayFromHome)
                RestoreHomePosition();
            EnsurePortraitImageStable();
            SetPoseSprite(pose);
            yield return BattlePresentationSpeed.Wait(duration);
            _isAnimating = false;
            if (!_isDead)
                ApplyIdleStill();
        }

        public IEnumerator ReturnHome()
        {
            if (portraitRoot == null)
                yield break;

            if (!_homeCaptured)
            {
                CaptureHomeIfNeeded();
                if (!_homeCaptured)
                {
                    _awayFromHome = false;
                    _isAnimating = false;
                    yield break;
                }
            }

            _isAnimating = true;
            var dist = Vector3.Distance(portraitRoot.position, _homeWorldPosition);
            if (dist > 0.01f)
                yield return TweenWorldPosition(portraitRoot, _homeWorldPosition, MoveDuration);

            RestoreHomePosition();
            _awayFromHome = false;
            _isAnimating = false;
            if (!_isDead)
                ApplyIdleStill();
        }

        public bool IsAtHomePosition() =>
            portraitRoot != null
            && _homeCaptured
            && Vector3.Distance(portraitRoot.position, _homeWorldPosition) <= 0.01f;

        public void RecaptureHomePosition()
        {
            if (portraitRoot == null)
                return;

            _homeWorldPosition = portraitRoot.position;
            _homeCaptured = true;
        }

        public void RestoreHomePosition()
        {
            if (!_homeCaptured || portraitRoot == null)
                return;

            portraitRoot.position = _homeWorldPosition;
        }

        public void ForceSettleHome()
        {
            if (!_awayFromHome || portraitRoot == null)
                return;

            if (Vector3.Distance(portraitRoot.position, _homeWorldPosition) > 0.01f)
                return;

            RestoreHomePosition();
            _awayFromHome = false;
            _isAnimating = false;
            if (!_isDead)
                ApplyIdleStill();
        }

        public void RecaptureHomeIfIdle()
        {
            if (_isAnimating || _idleLoopActive || _awayFromHome || portraitRoot == null)
                return;

            _homeWorldPosition = portraitRoot.position;
            _homeCaptured = true;
        }

        public void ShowHpDamageNumber(int damage)
        {
            if (damage > 0)
                ShowDamageNumber(damage);
        }

        public IEnumerator PlayHitReaction(int damage, bool useHitPose, bool retainPoseAfter = false)
        {
            if (_isDead || portraitImage == null)
                yield break;

            _isAnimating = true;
            EnsurePortraitImageStable();
            if (useHitPose)
                SetPoseSprite(PortraitPoseKind.Hit, faceCenter: true);

            if (damage > 0)
                ShowDamageNumber(damage);

            yield return FlashPortrait(HitFlashDuration);

            _isAnimating = false;
            if (!_isDead && !retainPoseAfter)
                ApplyIdleStill();
        }

        public IEnumerator PlayDamageFlashOnly()
        {
            if (_isDead || portraitImage == null)
                yield break;

            _isAnimating = true;
            yield return FlashPortrait(HitFlashDuration);
            _isAnimating = false;
        }

        public void ShowBlockAbsorbedNumber(int blocked)
        {
            if (blocked > 0)
                ShowBlockAbsorbed(blocked);
        }

        public void ShowHealNumber(int amount)
        {
            if (amount > 0)
                ShowHeal(amount);
        }

        public void ShowDodgeNumber()
        {
            ShowDodge();
        }

        public IEnumerator PlayHealFlash(float duration = 0.55f)
        {
            if (_isDead || portraitImage == null)
                yield break;

            _isAnimating = true;
            duration = BattlePresentationSpeed.ScaleDuration(duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.PingPong(elapsed * 3f, 1f);
                portraitImage.color = Color.Lerp(_isDead ? DeadTint : Color.white, HealFlashTint, t * 0.65f);
                yield return null;
            }

            portraitImage.color = _isDead ? DeadTint : Color.white;
            _isAnimating = false;
        }

        public IEnumerator PlayOverlayEffect(Sprite sprite, float duration = ActionEffectDuration)
        {
            if (sprite == null)
                yield break;

            HideActionEffectImmediate();

            EnsureActionEffectImage();
            if (_actionEffectImage == null)
                yield break;

            _actionEffectImage.sprite = sprite;
            _actionEffectImage.preserveAspect = true;
            _actionEffectImage.color = new Color(1f, 1f, 1f, ActionEffectAlpha);
            _actionEffectImage.gameObject.SetActive(true);
            _actionEffectImage.transform.SetAsLastSibling();

            yield return BattlePresentationSpeed.Wait(duration);
            HideActionEffectImmediate();
        }

        public IEnumerator PlayBlockedReaction(int blockedAmount = 0)
        {
            if (_isDead)
                yield break;

            _isAnimating = true;
            yield return FlashPortrait(HitFlashDuration);
            _isAnimating = false;
        }

        public IEnumerator PlayParryCounterAttack(float duration, Vector3? duelCenter = null)
        {
            if (_isDead || portraitImage == null || duration <= 0f)
                yield break;

            _isAnimating = true;
            if (duelCenter.HasValue)
            {
                if (_awayFromHome)
                    yield return ReturnHome();
                yield return MoveToCenter(duelCenter.Value);
            }
            else if (!_awayFromHome)
            {
                RestoreHomePosition();
            }

            EnsurePortraitImageStable();
            SetPoseSprite(PortraitPoseKind.Attack);
            yield return BattlePresentationSpeed.Wait(duration);
            _isAnimating = false;
            if (!_isDead && _awayFromHome)
                yield return ReturnHome();
            else if (!_isDead && !_awayFromHome)
                ApplyIdleStill();
        }

        public IEnumerator PlayDeathSequence()
        {
            if (portraitImage == null)
                yield break;

            _isAnimating = true;
            _isDead = true;
            _awayFromHome = false;
            StopIdleLoop();
            RestoreHomePosition();
            EnsurePortraitImageStable();
            SetPoseSprite(PortraitPoseKind.Death);
            portraitImage.color = DeadTint;
            _isAnimating = false;
            yield break;
        }

        void ShowDeathPoseImmediate()
        {
            StopIdleLoop();
            RestoreHomePosition();
            EnsurePortraitImageStable();
            SetPoseSprite(PortraitPoseKind.Death);
            if (portraitImage != null)
                portraitImage.color = DeadTint;
        }

        void ApplyIdleStill()
        {
            if (portraitImage == null || _visuals == null || _isDead)
                return;

            _poseFlipX = false;
            ApplyPortraitSprite(_visuals.GetPortrait(_characterDefinitionId));
            portraitImage.color = Color.white;
        }

        void SetPoseSprite(PortraitPoseKind pose, bool faceCenter = false)
        {
            if (portraitImage == null || _visuals == null)
                return;

            // 玩家朝右、敌人朝左由原画决定，受击等 pose 也不再水平翻转
            _poseFlipX = false;
            ApplyPortraitSprite(_visuals.GetPoseSprite(_characterDefinitionId, pose));
            portraitImage.color = _isDead ? DeadTint : Color.white;
        }

        void RefreshReferenceSprite()
        {
            _referenceSprite = _visuals != null
                ? _visuals.GetPortraitReference(_characterDefinitionId)
                : null;
        }

        void ApplyPortraitSprite(Sprite sprite)
        {
            if (portraitImage == null)
                return;

            EnsurePortraitImageStable();
            portraitImage.sprite = sprite;
            ApplySpriteFitScale(sprite);
        }

        void ApplySpriteFitScale(Sprite sprite)
        {
            if (portraitImage == null)
                return;

            EnsurePortraitImageStable();
            var flipX = _poseFlipX ? -1f : 1f;
            portraitImage.rectTransform.localScale = new Vector3(flipX, 1f, 1f);
        }

        void EnsurePortraitImageStable()
        {
            if (portraitImage == null)
                return;

            portraitImage.preserveAspect = true;
            var rt = portraitImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        IEnumerator IdleLoop(IReadOnlyList<Sprite> frames)
        {
            _idleLoopActive = true;
            RestoreHomePosition();
            EnsurePortraitImageStable();

            var index = 0;
            while (_idleLoopActive && !_isDead && !_isAnimating)
            {
                ApplyPortraitSprite(frames[index]);
                portraitImage.color = Color.white;
                index = (index + 1) % frames.Count;
                yield return new WaitForSeconds(IdleFrameInterval);
            }

            _idleRoutine = null;
        }

        IEnumerator FlashPortrait(float duration)
        {
            if (portraitImage == null || _isDead)
                yield break;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);

            duration = BattlePresentationSpeed.ScaleDuration(duration);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.PingPong(elapsed * 3f, 1f);
                portraitImage.color = Color.Lerp(_isDead ? DeadTint : Color.white, HitFlashTint, t * 0.55f);
                yield return null;
            }

            portraitImage.color = _isDead ? DeadTint : Color.white;
            _flashRoutine = null;
        }

        IEnumerator TweenWorldPosition(RectTransform rt, Vector3 targetWorld, float duration)
        {
            duration = BattlePresentationSpeed.ScaleDuration(duration);
            var start = rt.position;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                rt.position = Vector3.Lerp(start, targetWorld, t);
                yield return null;
            }

            rt.position = targetWorld;
        }

        public void SetDamageFloaterBelow(RectTransform statsRow)
        {
            EnsureDamageFloater();
            if (_damageFloater == null || statsRow == null)
                return;

            var rt = _damageFloater.rectTransform;
            rt.SetParent(statsRow, false);
            rt.SetAsLastSibling();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -10f);
            rt.sizeDelta = new Vector2(140f, 44f);
        }

        /// <summary>掉血/掉盾飘字：贴在立绘可视区域顶部略上方（红框位置），勿挂到槽位容器顶。</summary>
        public void SetDamageFloaterAbovePortrait()
        {
            EnsureDamageFloater();
            if (_damageFloater == null)
                return;

            var parent = portraitRoot != null ? portraitRoot : transform as RectTransform;
            if (parent == null)
                return;

            var rt = _damageFloater.rectTransform;
            rt.SetParent(parent, false);
            rt.SetAsLastSibling();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(160f, 40f);
            rt.anchoredPosition = new Vector2(0f, ResolveSpriteTopLocalY(parent) + 6f);
        }

        /// <summary>立绘 Image 在 portraitRoot 内 letterbox 后的可视顶边（相对根中心）。</summary>
        float ResolveSpriteTopLocalY(RectTransform parent)
        {
            if (parent == null)
                return 40f;

            var rect = parent.rect;
            if (rect.height <= 1f)
                return 40f;

            var sprite = portraitImage != null ? portraitImage.sprite : _referenceSprite;
            if (sprite == null || sprite.rect.height <= 0f || sprite.rect.width <= 0f)
                return rect.height * 0.5f;

            var spriteAspect = sprite.rect.width / sprite.rect.height;
            var rectAspect = rect.width / Mathf.Max(1f, rect.height);
            float visualHeight;
            if (spriteAspect > rectAspect)
                visualHeight = rect.width / spriteAspect;
            else
                visualHeight = rect.height;

            return visualHeight * 0.5f;
        }

        void CaptureHomeIfNeeded()
        {
            if (_homeCaptured || portraitRoot == null)
                return;

            _homeWorldPosition = portraitRoot.position;
            _homeCaptured = true;
        }

        void EnsureDamageFloater()
        {
            if (_damageFloater != null)
            {
                // 已存在时同步最新字号与锚点（热重载/旧实例）
                _damageFloater.fontSize = (int)DamageFloaterFontSize;
                return;
            }

            var anchor = portraitRoot != null
                ? portraitRoot
                : (_damageFloaterAnchor != null ? _damageFloaterAnchor : transform as RectTransform);
            if (anchor == null)
                return;

            var go = new GameObject("DamageFloater", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(anchor, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, ResolveSpriteTopLocalY(anchor) + 6f);
            rt.sizeDelta = new Vector2(160f, 40f);

            _damageFloater = go.GetComponent<Text>();
            _damageFloater.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _damageFloater.fontSize = (int)DamageFloaterFontSize;
            _damageFloater.fontStyle = FontStyle.Bold;
            _damageFloater.alignment = TextAnchor.LowerCenter;
            _damageFloater.color = new Color(1f, 0.28f, 0.28f, 1f);
            _damageFloater.raycastTarget = false;
            _damageFloater.gameObject.SetActive(false);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        void EnsureActionEffectImage()
        {
            if (_actionEffectImage != null)
                return;

            var parent = portraitRoot != null ? portraitRoot : transform as RectTransform;
            if (parent == null)
                return;

            var go = new GameObject("ActionEffect", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(160f, 160f);

            _actionEffectImage = go.GetComponent<Image>();
            _actionEffectImage.raycastTarget = false;
            _actionEffectImage.gameObject.SetActive(false);
        }

        void HideActionEffectImmediate()
        {
            if (_actionEffectImage == null)
                return;

            _actionEffectImage.gameObject.SetActive(false);
            _actionEffectImage.sprite = null;
        }

        void ShowDamageNumber(int damage)
        {
            if (_damageFloater == null)
                return;

            _damageFloater.color = new Color(1f, 0.35f, 0.35f, 1f);
            _damageFloater.text = $"-{damage}";
            _damageFloater.gameObject.SetActive(true);
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            if (_damageHideRoutine != null)
                StopCoroutine(_damageHideRoutine);
            _damageHideRoutine = StartCoroutine(HideDamageFloaterAfterDelay());
        }

        void ShowBlockAbsorbed(int blocked)
        {
            if (_damageFloater == null)
                return;

            _damageFloater.color = new Color(0.55f, 0.85f, 1f, 1f);
            _damageFloater.text = $"护甲 -{blocked}";
            _damageFloater.gameObject.SetActive(true);
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            if (_damageHideRoutine != null)
                StopCoroutine(_damageHideRoutine);
            _damageHideRoutine = StartCoroutine(HideDamageFloaterAfterDelay());
        }

        void ShowHeal(int amount)
        {
            if (_damageFloater == null)
                return;

            _damageFloater.color = new Color(0.45f, 1f, 0.55f, 1f);
            _damageFloater.text = $"+{amount}";
            _damageFloater.gameObject.SetActive(true);
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            if (_damageHideRoutine != null)
                StopCoroutine(_damageHideRoutine);
            _damageHideRoutine = StartCoroutine(HideDamageFloaterAfterDelay());
        }

        void ShowDodge()
        {
            if (_damageFloater == null)
                return;

            _damageFloater.color = new Color(1f, 0.92f, 0.35f, 1f);
            _damageFloater.text = "闪避！";
            _damageFloater.gameObject.SetActive(true);
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            if (_damageHideRoutine != null)
                StopCoroutine(_damageHideRoutine);
            _damageHideRoutine = StartCoroutine(HideDamageFloaterAfterDelay());
        }

        IEnumerator HideDamageFloaterAfterDelay()
        {
            yield return BattlePresentationSpeed.Wait(HitFlashDuration);
            if (_damageFloater != null)
                _damageFloater.gameObject.SetActive(false);
            _damageHideRoutine = null;
        }
    }
}
