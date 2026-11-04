using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Asks for the Steam password, and a Steam Guard code when Steam wants one, then logs SteamCMD in so it saves
    /// the login on this machine. Opened by the Steam login status button in Preferences when the login needs it.
    /// The password and code only stay in this window until the login works or the window closes. They are never
    /// saved or logged, and are left out of serialization so a script reload forgets them.
    /// </summary>
    internal partial class SteamLoginWindow : EditorWindow
    {
        private const string PasswordControl = "SteamLoginPassword";
        private const string SteamGuardCodeControl = "SteamLoginSteamGuardCode";
        private static readonly Vector2 Size = new Vector2(420, 230);

        [NonSerialized] private string m_password = "";
        [NonSerialized] private string m_steamGuardCode = "";
        [NonSerialized] private bool m_loggingIn;
        [NonSerialized] private string m_message = "";
        [NonSerialized] private MessageType m_messageType = MessageType.None;
        [NonSerialized] private string m_focusControl = PasswordControl;

        public static void ShowWindow()
        {
            SteamLoginWindow window = GetWindow<SteamLoginWindow>(true, "Build Uploader Steam Login", true);
            window.minSize = Size;
            window.position = new Rect(GetCentre() - Size / 2, Size);
            if (!string.IsNullOrEmpty(SteamSDK.LastProbeMessage))
            {
                window.SetMessage(SteamSDK.LastProbeMessage, MessageType.Info);
            }
            window.ShowUtility();
        }

        /// <summary>
        /// Hides all but the first and last two characters, eg "build-uploader" shows as "bu**********er".
        /// Names of four characters or fewer are hidden completely.
        /// </summary>
        internal static string MaskUserName(string userName)
        {
            if (string.IsNullOrEmpty(userName))
            {
                return "";
            }

            if (userName.Length <= 4)
            {
                return new string('*', userName.Length);
            }

            return userName.Substring(0, 2) + new string('*', userName.Length - 4) + userName.Substring(userName.Length - 2);
        }

        private static Vector2 GetCentre()
        {
#if UNITY_2020_1_OR_NEWER
            return EditorGUIUtility.GetMainWindowPosition().center;
#else
            return new Vector2(Screen.currentResolution.width, Screen.currentResolution.height) / 2;
#endif
        }

        private bool CanLogIn => !m_loggingIn && !string.IsNullOrEmpty(m_password);

        private async void LogIn()
        {
            if (!CanLogIn)
            {
                return;
            }

            m_loggingIn = true;
            SetMessage("Logging in. If Steam asks, approve the login in the Steam Mobile app.", MessageType.Info);

            SteamLoginResult result;
            try
            {
                result = await SteamSDK.LoginAsync(m_password, m_steamGuardCode);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = new SteamLoginResult(AuthStatus.Unknown, false, "Logging in failed. Check the Console for the error.");
            }
            finally
            {
                m_loggingIn = false;
                InternalEditorUtility.RepaintAllViews();
            }

            // Closed while SteamCMD was running
            if (this == null)
            {
                return;
            }

            if (result.Status == AuthStatus.Authorized)
            {
                m_password = "";
                m_steamGuardCode = "";
                Close();
                return;
            }

            if (result.NeedsSteamGuardCode)
            {
                m_steamGuardCode = "";
                m_focusControl = SteamGuardCodeControl;
            }

            string message = string.IsNullOrEmpty(result.Message) ? "Steam didn't log in." : result.Message;
            SetMessage(message, result.NeedsSteamGuardCode ? MessageType.Warning : MessageType.Error);
        }

        private void SetMessage(string message, MessageType type)
        {
            m_message = message;
            m_messageType = type;
            Repaint();
        }

        private void OnDestroy()
        {
            m_password = "";
            m_steamGuardCode = "";
        }
    }
}
