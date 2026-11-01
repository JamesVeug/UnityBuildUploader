using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Running tasks are saved here between steps so a script reload or a closed editor doesn't lose them.
    /// Library is per project, untracked and survives both. See UploadTaskResumer.
    /// </summary>
    internal static class UploadTaskCheckpoints
    {
        internal const int Version = 1;
        private const string TemporaryExtension = ".tmp";

        internal static readonly string DefaultFolderPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "BuildUploader", "InProgressTasks"));

        /// <summary>
        /// Tests point this at their own folder. A task keeps the folder it started with.
        /// </summary>
        internal static string FolderPath = DefaultFolderPath;

        internal static string FilePath(string folder, string taskGUID)
        {
            return Path.Combine(folder, taskGUID + ".json");
        }

        /// <summary>
        /// Writes synchronously, so the file is complete before a script reload can happen. The new file is written
        /// beside the old one first, so it is never half-written; List recovers it if Unity stops before the move.
        /// File.Replace is not used: Mono's fails on Windows with "Unable to remove the file to be replaced".
        /// </summary>
        internal static void Write(string folder, string taskGUID, Dictionary<string, object> checkpoint)
        {
            Directory.CreateDirectory(folder);
            string path = FilePath(folder, taskGUID);
            string temporaryPath = path + TemporaryExtension;
            File.WriteAllText(temporaryPath, JSON.SerializeObject(checkpoint));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporaryPath, path);
        }

        internal static Dictionary<string, object> Read(string path)
        {
            return JSON.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
        }

        internal static string[] List(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return new string[0];
            }

            // A finished write that Unity stopped before moving into place
            foreach (string temporaryPath in Directory.GetFiles(folder, "*.json" + TemporaryExtension))
            {
                string path = temporaryPath.Substring(0, temporaryPath.Length - TemporaryExtension.Length);
                if (!File.Exists(path))
                {
                    File.Move(temporaryPath, path);
                }
            }

            return Directory.GetFiles(folder, "*.json");
        }

        internal static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                // Or List would bring it back
                if (File.Exists(path + TemporaryExtension))
                {
                    File.Delete(path + TemporaryExtension);
                }
            }
            catch (System.Exception e) when (e is IOException || e is System.UnauthorizedAccessException)
            {
                Debug.LogWarning($"[BuildUploader] Could not delete saved upload task {path}: {e.Message}");
            }
        }
    }
}
