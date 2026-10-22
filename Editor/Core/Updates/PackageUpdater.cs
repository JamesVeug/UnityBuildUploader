using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Starts an update the way that fits how the package was installed.
    /// </summary>
    internal static class PackageUpdater
    {
        private static AddRequest s_addRequest;
        private static bool s_downloading;

        public static bool IsUpdating => s_addRequest != null || s_downloading;

        /// <summary>
        /// Why updating has to wait, or null when it can start now.
        /// </summary>
        public static string BlockedReason()
        {
            if (IsUpdating)
            {
                return "An update is already in progress.";
            }

            if (EditorApplication.isCompiling)
            {
                return "Wait for scripts to finish compiling.";
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "Exit Play Mode to update.";
            }

            if (UploadTask.AllTasks.Any(task => task.HasStarted && !task.IsComplete))
            {
                return "Wait for the running upload to finish.";
            }

            return null;
        }

        /// <summary>
        /// Git and registry installs: Package Manager downloads the release and the scripts reload.
        /// </summary>
        public static void UpdateWithPackageManager(PackageInstallSource source, ReleaseInfo release)
        {
            string identifier = source.PackageManagerIdentifier(release);
            if (string.IsNullOrEmpty(identifier))
            {
                return;
            }

            string target = source.FollowsBranch ? $"the newest commit on {source.GitRevision}" : $"v{release.Version}";
            bool update = EditorUtility.DisplayDialog("Update Build Uploader",
                $"Update the Build Uploader to {target}?\n\n" +
                "Package Manager will change Packages/manifest.json and Packages/packages-lock.json. " +
                "They are usually committed, so your team will get this version too.",
                "Update", "Cancel");
            if (!update)
            {
                return;
            }

            Debug.Log($"Build Uploader: updating with Package Manager to {identifier}");
            s_addRequest = Client.Add(identifier);
            EditorApplication.update += WaitForPackageManager;
            UpdateChecker.NotifyChanged();
        }

        private static void WaitForPackageManager()
        {
            if (s_addRequest == null)
            {
                EditorApplication.update -= WaitForPackageManager;
                return;
            }

            if (!s_addRequest.IsCompleted)
            {
                return;
            }

            EditorApplication.update -= WaitForPackageManager;
            if (s_addRequest.Status == StatusCode.Failure)
            {
                string error = s_addRequest.Error != null ? s_addRequest.Error.message : "Unknown error";
                Debug.LogError("Build Uploader: Package Manager could not update the package: " + error);
                EditorUtility.DisplayDialog("Update Build Uploader", "Package Manager could not update the Build Uploader:\n\n" + error, "OK");
            }
            else if (s_addRequest.Result != null)
            {
                Debug.Log($"Build Uploader: Package Manager installed {s_addRequest.Result.version}");
            }

            s_addRequest = null;
            UpdateChecker.Reload();
        }

        /// <summary>
        /// Opens the Build Uploader in Package Manager's My Assets, where Asset Store owners update it.
        /// </summary>
        public static void OpenInAssetStore()
        {
            // Package Manager handles Asset Store links itself. Application.OpenURL would hand the link to the
            // operating system, which starts whichever editor registered it last.
            MethodInfo openUrl = FindPackageManagerOpenUrl();
            if (openUrl != null)
            {
                try
                {
                    openUrl.Invoke(null, new object[] { PackageInstallSource.AssetStoreLink });
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Build Uploader could not open Package Manager: " + e.Message);
                }
            }

            Application.OpenURL(PackageInstallSource.AssetStoreUrl);
        }

        /// <summary>
        /// PackageManagerWindow.OpenURL is internal and lives in a different assembly and namespace across Unity versions.
        /// </summary>
        internal static MethodInfo FindPackageManagerOpenUrl()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.Name != "PackageManagerWindow")
                    {
                        continue;
                    }

                    MethodInfo method = type.GetMethod("OpenURL", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(string) }, null);
                    if (method != null)
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Downloads the release's .unitypackage from GitHub and opens Unity's import window for it.
        /// </summary>
        public static async void DownloadAndImport(ReleaseInfo release)
        {
            if (string.IsNullOrEmpty(release.UnityPackageUrl))
            {
                Application.OpenURL(release.Url);
                return;
            }

            s_downloading = true;
            UpdateChecker.NotifyChanged();
            try
            {
                using (RequestWrapper www = RequestWrapper.Get(release.UnityPackageUrl))
                {
                    RequestResult result = await www.SendAsync(null);
                    if (!result.IsSuccessful || result.Bytes == null || result.Bytes.Length == 0)
                    {
                        Debug.LogError($"Build Uploader could not download {release.UnityPackageUrl}: {result.Data}");
                        EditorUtility.DisplayDialog("Update Build Uploader",
                            $"Could not download {release.UnityPackageName} from GitHub. Download it from the release page instead.",
                            "OK");
                        Application.OpenURL(release.Url);
                        return;
                    }

                    string path = Path.Combine(Path.GetTempPath(), "BuildUploader", release.UnityPackageName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, result.Bytes);

                    // Once imported, files this version no longer has are deleted and moved files moved.
                    UnityPackageCleanup.ExpectImport(path);
                    AssetDatabase.ImportPackage(path, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                s_downloading = false;
                UpdateChecker.NotifyChanged();
            }
        }
    }
}
