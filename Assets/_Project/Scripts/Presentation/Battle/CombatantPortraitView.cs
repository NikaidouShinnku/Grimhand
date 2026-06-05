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

        const float MoveDuration = 0.35f;
        const float PoseHoldDuration = 1f;
        const float IdleFrameInterval = 0.2f;
        const float HitFlashDuration = 1f;
        const float DamageFloaterFontSize = 34f;

        [SerializeField] RectTransform portraitRoot;
        [SerializeField] Image portraitImage;

        CharacterVisualCatalogSO _visuals;
        RectTransform _damageFloaterAnchor;
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
            CombatantId = combatantId;
            _characterDefinitionId = characterDefinitionId;
            _team = team;
            RefreshReferenceSprite();

            if (isAlive)
            {
                if (_isDead)
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

        public void BeginPlanningIdle()
        {
            if (_isDead || _isAnimating || portraitImage == null || _visuals == null)
                return;

            RestoreHomePosition();

            var frames = _visuals.GetIdleAnimationFrames(_characterDefinitionId);
            if (frames.Count <= 1)
            {
                StopIdleLoop();
                ApplyIdleStill();
                return;
            }

            if (_idleLoopActive)
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
            RestoreHomePosition();

            // 仅 X 轴移到战场中央，Y 保持与站位时相同水平线。
            var target = new Vector3(centerWorld.x, _homeWorldPosition.y, _homeWorldPosition.z);
            yield return TweenWorldPosition(portraitRoot, target, MoveDuration);
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

            yield return new WaitForSeconds(duration);
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
            yield return new WaitForSeconds(duration);
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
                _awayFromHome = false;
                _isAnimating = false;
                yield break;
            }

            yield return TweenWorldPosition(portraitRoot, _homeWorldPosition, MoveDuration);
            RestoreHomePosition();
            _awayFromHome = false;
            _isAnimating = false;
            if (!_isDead)
                ApplyIdleStill();
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

        public IEnumerator PlayBlockedReaction(int blockedAmount = 0)
        {
            if (_isDead)
                yield break;

            _isAnimating = true;
            yield return FlashPortrait(HitFlashDuration);
            _isAnimating = false;
        }

        public IEnumerator PlayParryCounterAttack(float duration)
        {
            if (_isDead || portraitImage == null || duration <= 0f)
                yield break;

            _isAnimating = true;
            if (!_awayFromHome)
                RestoreHomePosition();

            EnsurePortraitImageStable();
            SetPoseSprite(PortraitPoseKind.Attack);
            yield return new WaitForSeconds(duration);
            _isAnimating = false;
            if (!_isDead && !_awayFromHome)
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

            _poseFlipX = false;
            if (pose == PortraitPoseKind.Hit && faceCenter)
                _poseFlipX = _team == TeamSide.Player;

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

            var scale = 1f;
            if (sprite != null && _referenceSprite != null)
            {
                var refSize = _referenceSprite.rect.size;
                var size = sprite.rect.size;
                if (size.x > 0.01f && size.y > 0.01f)
                    scale = Mathf.Min(refSize.x / size.x, refSize.y / size.y);
            }

            var flipX = _poseFlipX ? -1f : 1f;
            portraitImage.rectTransform.localScale = new Vector3(flipX * scale, scale, 1f);
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
                return;

            var anchor = _damageFloaterAnchor != null ? _damageFloaterAnchor : transform as RectTransform;
            if (anchor == null)
                return;

            var go = new GameObject("DamageFloater", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(anchor, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -6f);
            rt.sizeDelta = new Vector2(140f, 44f);

            _damageFloater = go.GetComponent<Text>();
            _damageFloater.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _damageFloater.fontSize = (int)DamageFloaterFontSize;
            _damageFloater.fontStyle = FontStyle.Bold;
            _damageFloater.alignment = TextAnchor.UpperCenter;
            _damageFloater.color = new Color(1f, 0.28f, 0.28f, 1f);
            _damageFloater.raycastTarget = false;
            _damageFloater.gameObject.SetActive(false);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        void ShowDamageNumber(int damage)
        {
            if (_damageFloater == null)
                return;

            _damageFloater.color = new Color(1f, 0.35f, 0.35f, 1f);
            _damageFloater.text = $"-{damage}";
            _damageFloater.gameObject.SetActive(true);
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
            if (_damageHideRoutine != null)
                StopCoroutine(_damageHideRoutine);
            _damageHideRoutine = StartCoroutine(HideDamageFloaterAfterDelay());
        }

        IEnumerator HideDamageFloaterAfterDelay()
        {
            yield return new WaitForSeconds(HitFlashDuration);
            if (_damageFloater != null)
                _damageFloater.gameObject.SetActive(false);
            _damageHideRoutine = null;
        }
    }
}
