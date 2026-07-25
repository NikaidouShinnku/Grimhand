using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗 HUD 按钮按下变暗反馈。</summary>
    public static class BattleButtonPressFeedback
    {
        public static void Apply(Button button)
        {
            if (button == null)
                return;

            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.95f, 1f);
            colors.pressedColor = new Color(0.52f, 0.52f, 0.56f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.42f, 0.42f, 0.46f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }
    }
}
