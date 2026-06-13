using System;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗结算动画倍速（不影响 idle 循环帧率）。</summary>
    public static class BattlePresentationSpeed
    {
        public const float FastMultiplier = 2f;

        static bool _isFast;

        public static bool IsFast => _isFast;
        public static float Multiplier => _isFast ? FastMultiplier : 1f;

        public static event Action Changed;

        public static void Toggle()
        {
            _isFast = !_isFast;
            Changed?.Invoke();
        }

        public static float ScaleDuration(float seconds) =>
            seconds <= 0f ? seconds : seconds / Multiplier;

        public static WaitForSeconds Wait(float seconds) =>
            new WaitForSeconds(ScaleDuration(seconds));
    }
}
