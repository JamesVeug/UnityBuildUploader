using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class UploadTask
    {
        /// <summary>
        /// Where each enabled config's build ended up, once for each path. That's the folder, or .zip file, of every
        /// enabled Local Path destination. A config without one leaves its build in the task cache, which is only
        /// listed when Cleanup kept it (Preferences.DeleteCacheAfterUpload off). Paths that don't exist or are empty
        /// are left out. Empty unless the task succeeded.
        /// </summary>
        internal List<string> BuildFoldersToOpen()
        {
            List<string> paths = new List<string>();
            if (!IsSuccessful)
            {
                return paths;
            }

            for (int i = 0; i < m_uploadConfigs.Count; i++)
            {
                UploadConfig config = m_uploadConfigs[i];
                if (!config.Enabled)
                {
                    continue;
                }

                bool hasLocalPath = false;
                foreach (UploadConfig.DestinationData destinationData in config.Destinations)
                {
                    if (destinationData.Enabled && destinationData.Destination is LocalPathDestination destination)
                    {
                        hasLocalPath = true;
                        AddBuildFolder(paths, destination.FullPath());
                    }
                }

                if (!hasLocalPath && m_cachedLocations != null && i < m_cachedLocations.Length)
                {
                    AddBuildFolder(paths, m_cachedLocations[i]);
                }
            }

            return paths;
        }

        private static void AddBuildFolder(List<string> paths, string path)
        {
            if (HasContent(path) && !paths.Any(addedPath => Utils.ComparePaths(addedPath, path)))
            {
                paths.Add(path);
            }
        }

        /// <summary>
        /// Opens the build folders when Preferences.OpenBuildFoldersAfterUpload is on and someone can see them.
        /// They open on the next editor update, after the Upload window's result dialog is closed.
        /// </summary>
        private void OpenBuildFoldersWhenComplete()
        {
            if (!Preferences.OpenBuildFoldersAfterUpload || Application.isBatchMode)
            {
                return;
            }

            try
            {
                List<string> paths = BuildFoldersToOpen();
                if (paths.Count > 0)
                {
                    EditorApplication.delayCall += () => OpenBuildFolders(paths);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BuildUploader] Could not find the build folders to open: {e.Message}");
            }
        }

        private static void OpenBuildFolders(List<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        // A zipped build: open its folder with the .zip selected
                        EditorUtility.RevealInFinder(path);
                    }
                    else
                    {
                        EditorUtility.OpenWithDefaultApp(path);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BuildUploader] Could not open the build folder {path}: {e.Message}");
                }
            }
        }

        private static bool HasContent(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                return File.Exists(path) || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any());
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
