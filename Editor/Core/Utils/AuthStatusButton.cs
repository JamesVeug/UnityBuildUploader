using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Whether a service is allowed to act as the account the user configured.
    /// Stored as an int in prefs by some services, so do not renumber.
    /// </summary>
    public enum AuthStatus
    {
        /// <summary>We have never checked, or what we checked no longer applies.</summary>
        Unknown = 0,

        /// <summary>Credentials are saved on this machine but we have not confirmed they still work.</summary>
        CredentialsCached = 1,

        /// <summary>Confirmed working.</summary>
        Authorized = 2,

        /// <summary>The service refused us. The user has to log in.</summary>
        RequiresLogin = 3
    }

    /// <summary>
    /// Implemented by a service that needs the user to log in on this machine before it can be used.
    /// Draw the state with <see cref="AuthStatusButton"/>.
    /// </summary>
    public interface IAuthenticatedService
    {
        /// <summary>What the user is logging into. eg "Steam". Used in tooltips and to key UI state.</summary>
        string AuthServiceName { get; }

        /// <summary>False while there is nothing to check yet - no username, no SDK, service disabled.</summary>
        bool CanAuthenticate { get; }

        /// <summary>The state to show. Must be cheap - this is called every repaint.</summary>
        AuthStatus GetAuthStatus();

        /// <summary>Detail from the last check to show in the tooltip. May be empty.</summary>
        string AuthStatusMessage { get; }

        /// <summary>Confirm the saved login still works.</summary>
        Task VerifyAuthAsync();

        /// <summary>Do whatever authorizes this machine - open a console, a browser, a dialog.</summary>
        void StartAuthentication();
    }

    /// <summary>
    /// A single icon button showing whether a service is logged in, and doing whatever the current
    /// state needs when clicked. Tick when we are good, cross when a login is needed, exclamation
    /// when nobody has checked yet.
    /// </summary>
    public static class AuthStatusButton
    {
        private const int DefaultWidth = 24;

        // Keyed by name rather than by instance - services are handed out by reflection and the
        // object we are given is not guaranteed to be the same one next repaint.
        private static readonly HashSet<string> m_busy = new HashSet<string>();

        public static void Draw(IAuthenticatedService service, params GUILayoutOption[] options)
        {
            if (service == null)
            {
                return;
            }

            bool busy = m_busy.Contains(service.AuthServiceName);
            AuthStatus status = service.GetAuthStatus();

            GUIContent content = GetContent(service, status, busy);
            GUILayoutOption[] layout = options.Length > 0 ? options : new[] { GUILayout.Width(DefaultWidth) };

            using (new EditorGUI.DisabledScope(busy || !service.CanAuthenticate))
            {
                if (GUILayout.Button(content, layout))
                {
                    Interact(service, status);
                }
            }
        }

        /// <summary>
        /// Colour for whatever field this button sits next to - a username, a token - so the state is
        /// visible without having to notice the button itself. Matches the icon: green tick, red cross,
        /// yellow exclamation.
        /// </summary>
        public static Color GetStatusColor(IAuthenticatedService service)
        {
            if (service == null || !service.CanAuthenticate)
            {
                return Color.red;
            }

            return GetStatusColor(service.GetAuthStatus());
        }

        public static Color GetStatusColor(AuthStatus status)
        {
            switch (status)
            {
                case AuthStatus.Authorized:
                case AuthStatus.CredentialsCached:
                    return Color.green;

                case AuthStatus.RequiresLogin:
                    return Color.red;

                default:
                    return Color.yellow;
            }
        }

        private static void Interact(IAuthenticatedService service, AuthStatus status)
        {
            if (status == AuthStatus.RequiresLogin)
            {
                // Nothing to verify - the user has to log in before there is anything to confirm.
                service.StartAuthentication();
                return;
            }

            Verify(service);
        }

        private static async void Verify(IAuthenticatedService service)
        {
            string key = service.AuthServiceName;
            m_busy.Add(key);
            try
            {
                await service.VerifyAuthAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                m_busy.Remove(key);
                InternalEditorUtility.RepaintAllViews();
            }

            // Otherwise a check that can't tell, such as Steam printing something unexpected, leaves the user with no
            // way to log in: every click would only check again.
            AuthStatus status = service.GetAuthStatus();
            if (status == AuthStatus.RequiresLogin || status == AuthStatus.Unknown)
            {
                service.StartAuthentication();
            }
        }

        private static GUIContent GetContent(IAuthenticatedService service, AuthStatus status, bool busy)
        {
            string name = service.AuthServiceName;
            if (busy)
            {
                return Icon("WaitSpin00", "...", $"Checking whether {name} is logged in...");
            }

            if (!service.CanAuthenticate)
            {
                return Icon("console.warnicon.sml", "!",
                    $"{name} is not set up far enough to check whether it is logged in.");
            }

            string detail = service.AuthStatusMessage;
            detail = string.IsNullOrEmpty(detail) ? "" : "\n\n" + detail;

            switch (status)
            {
                case AuthStatus.Authorized:
                    return Icon("TestPassed", "✓",
                        $"{name} is logged in on this machine.{detail}\n\nClick to verify it still is, and log in if it isn't.");

                case AuthStatus.CredentialsCached:
                    return Icon("TestPassed", "✓",
                        $"{name} has login details saved on this machine but we have not confirmed they work.{detail}\n\nClick to verify them, and log in if they don't.");

                case AuthStatus.RequiresLogin:
                    return Icon("TestFailed", "✗",
                        $"{name} is not logged in on this machine.{detail}\n\nClick to log in.");

                default:
                    return Icon("console.warnicon.sml", "!",
                        $"Nobody has checked whether {name} is logged in on this machine.{detail}\n\nClick to check, and log in if it isn't.");
            }
        }

        private static readonly Dictionary<string, Texture> m_icons = new Dictionary<string, Texture>();

        /// <summary>
        /// Built-in editor icons move around between Unity versions, so fall back to text when one is missing.
        /// </summary>
        private static GUIContent Icon(string iconName, string fallbackText, string tooltip)
        {
            if (!m_icons.TryGetValue(iconName, out Texture texture))
            {
                try
                {
                    GUIContent icon = EditorGUIUtility.IconContent(iconName);
                    texture = icon?.image;
                }
                catch (Exception)
                {
                    texture = null;
                }

                m_icons[iconName] = texture;
            }

            return texture != null
                ? new GUIContent(texture, tooltip)
                : new GUIContent(fallbackText, tooltip);
        }
    }
}
