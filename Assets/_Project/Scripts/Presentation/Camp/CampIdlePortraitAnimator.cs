using System.Collections;
using System.Collections.Generic;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>营地 UI：在 Image 上循环播放角色 idle 动画帧，保持长宽比。</summary>
    [DisallowMultipleComponent]
    public sealed class CampIdlePortraitAnimator : MonoBehaviour
    {
        const float FrameInterval = 0.12f;

        Image _image;
        CharacterVisualCatalogSO _visuals;
        string _characterId;
        Coroutine _loop;
        IReadOnlyList<Sprite> _frames;
        bool _animate;

        public void Bind(
            Image image,
            CharacterVisualCatalogSO visuals,
            string characterId,
            bool animate = true)
        {
            StopLoop();
            _image = image;
            _visuals = visuals;
            _characterId = characterId ?? "";
            _animate = animate;

            if (_image != null)
            {
                _image.preserveAspect = true;
                _image.color = Color.white;
            }

            if (_visuals == null || string.IsNullOrEmpty(_characterId))
            {
                if (_image != null)
                {
                    _image.sprite = null;
                    _image.color = Color.clear;
                }

                return;
            }

            _frames = _animate ? _visuals.GetIdleAnimationFrames(_characterId) : null;
            if (_frames != null && _frames.Count > 0)
            {
                _image.sprite = _frames[0];
                if (_animate && _frames.Count > 1 && isActiveAndEnabled)
                    _loop = StartCoroutine(PlayLoop());
                return;
            }

            _image.sprite = _visuals.GetPortrait(_characterId);
        }

        void OnEnable()
        {
            if (!_animate || _image == null || _frames == null || _frames.Count <= 1)
                return;
            if (_loop == null)
                _loop = StartCoroutine(PlayLoop());
        }

        void OnDisable()
        {
            StopLoop();
        }

        void StopLoop()
        {
            if (_loop == null)
                return;
            StopCoroutine(_loop);
            _loop = null;
        }

        IEnumerator PlayLoop()
        {
            var index = 0;
            while (_image != null && _frames != null && _frames.Count > 1)
            {
                _image.sprite = _frames[index];
                index = (index + 1) % _frames.Count;
                yield return new WaitForSecondsRealtime(FrameInterval);
            }

            _loop = null;
        }
    }
}
