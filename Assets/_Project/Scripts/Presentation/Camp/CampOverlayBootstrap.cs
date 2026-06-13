using UnityEngine;

namespace Grimhand.Presentation.Camp
{
    /// <summary>运行时确保 CampOverlays 宿主与子界面存在（场景未跑 Setup 时兜底）。</summary>
    internal static class CampOverlayBootstrap
    {
        public static Transform EnsureOverlayHost(Transform canvasRoot)
        {
            if (canvasRoot == null)
                return null;

            var existing = canvasRoot.Find("CampOverlays");
            if (existing != null)
                return existing;

            var go = new GameObject("CampOverlays", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            CampUiRuntime.StretchFull(go.GetComponent<RectTransform>());
            go.transform.SetAsLastSibling();
            return go.transform;
        }

        public static T EnsureOverlay<T>(Transform canvasRoot, string objectName) where T : Component
        {
            var host = EnsureOverlayHost(canvasRoot);
            if (host == null)
                return null;

            var child = host.Find(objectName);
            GameObject go;
            if (child == null)
            {
                go = new GameObject(objectName, typeof(RectTransform));
                go.transform.SetParent(host, false);
                CampUiRuntime.StretchFull(go.GetComponent<RectTransform>());
            }
            else
            {
                go = child.gameObject;
            }

            var comp = go.GetComponent<T>();
            if (comp == null)
                comp = go.AddComponent<T>();

            go.transform.SetAsLastSibling();
            return comp;
        }
    }
}
