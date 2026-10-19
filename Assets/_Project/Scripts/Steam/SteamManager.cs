// Based on Steamworks.NET SteamManager sample (public domain).
// Adapted for The Grimhand AppID 5111710 (Depot 5111712 is not the App ID).

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

namespace Grimhand.Steam
{
    [DisallowMultipleComponent]
    public class SteamManager : MonoBehaviour
    {
        public const uint AppId = 5111710;

#if !DISABLESTEAMWORKS
        protected static bool s_EverInitialized;
        protected static SteamManager s_instance;

        protected static SteamManager Instance
        {
            get
            {
                if (s_instance == null)
                    return new GameObject("SteamManager").AddComponent<SteamManager>();
                return s_instance;
            }
        }

        protected bool m_bInitialized;
        protected SteamAPIWarningMessageHook_t m_SteamAPIWarningMessageHook;

        public static bool Initialized => Instance.m_bInitialized;

        [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
        protected static void SteamAPIDebugTextHook(int nSeverity, System.Text.StringBuilder pchDebugText)
        {
            Debug.LogWarning(pchDebugText);
        }

#if UNITY_2019_3_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitOnPlayMode()
        {
            s_EverInitialized = false;
            s_instance = null;
        }
#endif

        protected virtual void Awake()
        {
            if (s_instance != null)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;

            if (s_EverInitialized)
                throw new System.Exception("Tried to Initialize the SteamAPI twice in one session!");

            DontDestroyOnLoad(gameObject);

            if (!Packsize.Test())
                Debug.LogError("[Steamworks.NET] Packsize Test returned false.", this);

            if (!DllCheck.Test())
                Debug.LogError("[Steamworks.NET] DllCheck Test returned false.", this);

            // Do not call RestartAppIfNecessary here:
            // - Double-clicking the exe would force Steam and show "no license" for accounts without the app.
            // - When launched from the Steam client, SteamAPI.Init() already receives the correct App ID.
            // Cloud save gracefully falls back to local-only if Init fails.

            try
            {
                m_bInitialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogWarning(
                    "[Steamworks.NET] Steam API library not found; running without Steam.\n" + e,
                    this);
                return;
            }

            if (!m_bInitialized)
            {
                Debug.LogWarning(
                    "[Steamworks.NET] SteamAPI_Init() failed. Cloud save will stay local-only.",
                    this);
                return;
            }

            s_EverInitialized = true;
            Debug.Log($"[Steamworks.NET] Initialized (AppId={AppId}).");
        }

        protected virtual void OnEnable()
        {
            if (s_instance == null)
                s_instance = this;

            if (!m_bInitialized)
                return;

            if (m_SteamAPIWarningMessageHook == null)
            {
                m_SteamAPIWarningMessageHook = new SteamAPIWarningMessageHook_t(SteamAPIDebugTextHook);
                SteamClient.SetWarningMessageHook(m_SteamAPIWarningMessageHook);
            }
        }

        protected virtual void OnDestroy()
        {
            if (s_instance != this)
                return;

            s_instance = null;
            if (!m_bInitialized)
                return;

            SteamAPI.Shutdown();
        }

        protected virtual void Update()
        {
            if (!m_bInitialized)
                return;

            SteamAPI.RunCallbacks();
        }
#else
        public static bool Initialized => false;
#endif
    }
}
