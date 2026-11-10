using UnityEditor;

namespace Wireframe
{
    /// <summary>
    /// Unity never saves the Android keystore and key passwords, so a build signed with a custom keystore fails after
    /// Unity restarts, and always in batch mode, until they are entered again. Build Uploader keeps them per user and
    /// per project in EditorPrefs and fills them in before an Android build.
    /// </summary>
    internal static class AndroidSigning
    {
        private const string KeystorePasswordKey = "AndroidKeystorePassword";
        private const string KeyAliasPasswordKey = "AndroidKeyAliasPassword";

        public static string KeystorePassword
        {
            get => ProjectEditorPrefs.GetString(KeystorePasswordKey);
            set => ProjectEditorPrefs.SetString(KeystorePasswordKey, value);
        }

        public static string KeyAliasPassword
        {
            get => ProjectEditorPrefs.GetString(KeyAliasPasswordKey);
            set => ProjectEditorPrefs.SetString(KeyAliasPasswordKey, value);
        }

        /// <summary>
        /// Fills in the passwords Unity doesn't have. Passwords already entered in Player Settings are kept.
        /// </summary>
        /// <returns>False with the reason when a custom keystore still has no password</returns>
        public static bool TryApplyPasswords(out string error)
        {
            error = null;
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                return true; // Unity signs with the debug key
            }

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass))
            {
                PlayerSettings.Android.keystorePass = KeystorePassword;
            }

            if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                PlayerSettings.Android.keyaliasPass = KeyAliasPassword;
            }

            if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass) || string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass))
            {
                error = "The Android build uses a custom keystore but its passwords are not set. Enter them in " +
                        "Project Settings > Build Uploader > General > Android Signing, or in Player Settings.";
                return false;
            }

            return true;
        }
    }
}
