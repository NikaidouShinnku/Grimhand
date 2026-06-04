using System.Collections;
using System.Collections.Generic;
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

        [SerializeField] RectTransform portraitRoot;
        [SerializeField] Image portraitImage;

        CharacterVisualCatalogSO _visuals;
        string _characterDefinitionId;
        Vector3 _homeWorldPosition;
        bool _homeCaptured;
        bool _isDead;
        bool _isAnimating;
        bool _idleLoopActive;
        Coroutine _idleRoutine;
        Coroutine _flashRoutine;
        Coroutine _damageHideRoutine;
        Text _damageFloater;

        public bool IsAnimating => _isAnimating;
        public bool IsIdleLoopActive => _idleLoopActive;
        public string CombatantId { get; private set; }

        public void Bind(CharacterVisualCatalogSO visuals, Image portrait, RectTransform root)
        {
            _visuals = visuals;
            portraitImage = portrait;
            portraitRoot = root;
            EnsureDamageFloater();
            CaptureHomeIfNeeded();
        }

        public void SetIdentity(string combatantId, string characterDefinitionId, bool isAlive)
        {
            CombatantId = combatantId;
            _characterDefinitionId = characterDefinitionId;
            _isDead = !isAlive;

            if (_isDead)
                ShowDeathPoseImmediate();
            else if (!_isAnimating && !_idleLoopActive)
                ApplyIdleStill();
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
            StopIdleLoop();
            CaptureHomeIfNeeded();
            RestoreHomePosition();

            var target = centerWorld;
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
            RestoreHomePosition();
            EnsurePortraitImageStable();
            SetPoseSprite(pose);
            yield return new WaitForSeconds(duration);
            _isAnimating = false;
        }

        public IEnumerator ReturnHome()
        {
            if (portraitRoot == null)
                yield break;

            if (!_homeCaptured)
            {
                _isAnimating = false;
                yield break;
            }

            yield return TweenWorldPosition(portraitRoot, _homeWorldPosition, MoveDuration);
            RestoreHomePosition();
            _isAnimating = false;
        }

        public void RestoreHomePosition()
        {
            if (!_homeCaptured || portraitRoot == null)
                return;

            portraitRoot.position = _homeWorldPosition;
        }

        public void RecaptureHomeIfIdle()
        {
            if (_isAnimating || _idleLoopActive || portraitRoot == null)
                return;

            _homeWorldPosition = portraitRoot.position;
            _homeCaptured = true;
        }

        public IEnumerator PlayHitReaction(int damage, bool useHitPose)
        {
            if (portraitImage == null)
                yield break;

            EnsurePortraitImageStable();
            if (useHitPose)
                SetPoseSprite(PortraitPoseKind.Hit);

            if (damage > 0)
                ShowDamageNumber(damage);

            yield return FlashPortrait(HitFlashDuration);
        }

        public IEnumerator PlayBlockedReaction()
        {
            yield return FlashPortrait(HitFlashDuration);
        }

        public IEnumerator PlayDeathSequence()
        {
            if (portraitImage == null)
                yield break;

            _isAnimating = true;
            _isDead = true;
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

            EnsurePortraitImageStable();
            portraitImage.sprite = _visuals.GetPortrait(_characterDefinitionId);
            portraitImage.color = Color.white;
        }

        void SetPoseSprite(PortraitPoseKind pose)
        {
            if (portraitImage == null || _visuals == null)
                return;

            portraitImage.sprite = _visuals.GetPoseSprite(_characterDefinitionId, pose);
            portraitImage.color = _isDead ? DeadTint : Color.white;
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
            rt.localScale = Vector3.one;
        }

        IEnumerator IdleLoop(IReadOnlyList<Sprite> frames)
        {
            _idleLoopActive = true;
            RestoreHomePosition();
            EnsurePortraitImageStable();

            var index = 0;
            while (_idleLoopActive && !_isDead && !_isAnimating)
            {
                portraitImage.sprite = frames[index];
                portraitImage.color = Color.white;
                index = (index + 1) % frames.Count;
                yield return new WaitForSeconds(IdleFrameInterval);
            }

            _idleRoutine = null;
        }

        IEnumerator FlashPortrait(float duration)
        {
            if (portraitImage == null)
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

        void CaptureHomeIfNeeded()
        {
            if (_homeCaptured || portraitRoot == null)
                return;

            _homeWorldPosition = portraitRoot.position;
            _homeCaptured = true;
        }

        void EnsureDamageFloater()
        {
            if (_damageFloater != null || portraitRoot == null)
                return;

            var go = new GameObject("DamageFloater", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(portraitRoot, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 12f);
            rt.sizeDelta = new Vector2(120f, 36f);

            _damageFloater = go.GetComponent<Text>();
            _damageFloater.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _damageFloater.fontSize = 26;
            _damageFloater.fontStyle = FontStyle.Bold;
            _damageFloater.alignment = TextAnchor.MiddleCenter;
            _damageFloater.color = new Color(1f, 0.35f, 0.35f, 1f);
            _damageFloater.raycastTarget = false;
            _damageFloater.gameObject.SetActive(false);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        void ShowDamageNumber(int damage)
        {
            if (_damageFloater == null)
                return;

            _damageFloater.text = $"-{damage}";
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
