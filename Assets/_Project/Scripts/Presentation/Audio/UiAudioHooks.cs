using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Audio
{
    /// <summary>为 Button / 可点击控件挂上悬停、点击 UI 音效。</summary>
    public static class UiAudioHooks
    {
        public static void WireButton(Button button, bool menuStyle = false)
        {
            if (button == null)
                return;

            if (button.GetComponent<UiAudioWiredMarker>() != null)
                return;

            button.gameObject.AddComponent<UiAudioWiredMarker>();

            var trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            EnsureEntry(trigger, EventTriggerType.PointerEnter, _ =>
            {
                if (button.IsActive() && button.interactable)
                    GameAudioService.Instance.PlayUiButtonHover();
            });

            button.onClick.AddListener(() =>
            {
                if (menuStyle)
                    GameAudioService.Instance.PlayUiMenuPress();
                else
                    GameAudioService.Instance.PlayUiButtonPress();
            });
        }

        static void EnsureEntry(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
        {
            foreach (var existing in trigger.triggers)
            {
                if (existing.eventID == type)
                    return;
            }

            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        sealed class UiAudioWiredMarker : MonoBehaviour
        {
        }
    }
}
