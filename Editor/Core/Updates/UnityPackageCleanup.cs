using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Unity's importer adds and updates files but never deletes the ones a newer version removed, and a file that
    /// moved folder keeps its old path. After a Build Uploader .unitypackage is imported (the GitHub update button,
    /// the Asset Store or one downloaded by hand), this makes the package folder match the package: files it no
    /// longer has are deleted and moved files go to their new path.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityPackageCleanup
    {
        internal const string PendingImportKey = "BuildUploader_PendingUnityPackage";

        internal class Asset
        {
            public string Guid;

            /// <summary>
            /// Project relative with forward slashes, eg: Packages/com.veugeljame.builduploader/Editor/Core/Utils.cs
            /// </summary>
            public string Path;
            public bool IsFolder;
        }

        internal class Move
        {
            public string From;
            public string To;
            public bool IsFolder;
        }

        internal class Plan
        {
            public readonly List<Move> Moves = new List<Move>();
            public readonly List<Asset> Deletes = new List<Asset>();

            /// <summary>
            /// Why nothing will be done, or null.
            /// </summary>
            public string Problem;

            /// <summary>
            /// What could not be done, filled in by <see cref="Apply"/>.
            /// </summary>
            public readonly List<string> Skipped = new List<string>();

            public bool HasChanges => Problem == null && (Moves.Count > 0 || Deletes.Count > 0);
        }

        static UnityPackageCleanup()
        {
            AssetDatabase.importPackageCompleted += OnImportCompleted;
        }

        /// <summary>
        /// Call before importing a Build Uploader .unitypackage so the cleanup can find the file once it's imported.
        /// </summary>
        public static void ExpectImport(string unityPackagePath)
        {
            SessionState.SetString(PendingImportKey, unityPackagePath);
        }

        private static void OnImportCompleted(string packageName)
        {
            string unityPackagePath = FindImportedFile(packageName);
            if (unityPackagePath == null)
            {
                return;
            }

            SessionState.EraseString(PendingImportKey);
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                Plan plan = CleanUp(unityPackagePath, projectRoot, Utils.s_packagePath.Replace('\\', '/'));
                if (plan == null)
                {
                    return;
                }

                if (plan.Problem != null)
                {
                    Debug.LogWarning($"Build Uploader did not tidy up after importing {packageName}: {plan.Problem}");
                    return;
                }

                if (plan.HasChanges)
                {
                    Debug.Log(Describe(plan, packageName));
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Build Uploader could not tidy up after importing {packageName}: {e}");
            }
        }

        /// <summary>
        /// The .unitypackage that was just imported: the one the GitHub update button downloaded, the Asset Store's
        /// download, or one in Downloads. Null when it isn't known.
        /// </summary>
        /// <param name="packageName">What Unity passes: the file's full path, or only its name, without ".unitypackage".</param>
        internal static string FindImportedFile(string packageName)
        {
            string name = Path.GetFileName(packageName);
            string[] candidates =
            {
                packageName + ".unitypackage",
                SessionState.GetString(PendingImportKey, ""),
                PackageInstallSource.FindAssetStoreDownload(),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", name + ".unitypackage"),
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && Path.IsPathRooted(candidate) && File.Exists(candidate) &&
                    string.Equals(Path.GetFileNameWithoutExtension(candidate), name, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Makes the package folder match a .unitypackage that was just imported over it.
        /// </summary>
        /// <param name="packageRoot">Project relative, eg: Packages/com.veugeljame.builduploader</param>
        /// <returns>What was done, or null when the .unitypackage isn't this package.</returns>
        internal static Plan CleanUp(string unityPackagePath, string projectRoot, string packageRoot)
        {
            if (Utils.PathExists(Path.Combine(projectRoot, packageRoot, ".git")))
            {
                // A submodule or clone is updated with git, never by deleting files from under it.
                return null;
            }

            Dictionary<string, string> contents = UnityPackageFile.ReadContents(unityPackagePath);
            List<Asset> installed = ReadInstalled(projectRoot, packageRoot);
            Plan plan = MakePlan(packageRoot, contents, installed);
            if (plan != null && plan.Problem == null)
            {
                Apply(projectRoot, plan);
            }

            return plan;
        }

        /// <summary>
        /// Every file and folder in the package folder that has a .meta, skipping what Unity ignores (.git, Samples~).
        /// </summary>
        internal static List<Asset> ReadInstalled(string projectRoot, string packageRoot)
        {
            List<Asset> assets = new List<Asset>();
            string root = Path.Combine(projectRoot, packageRoot);
            if (Directory.Exists(root))
            {
                AddAssets(projectRoot, root, assets);
            }

            return assets;
        }

        private static void AddAssets(string projectRoot, string folder, List<Asset> assets)
        {
            foreach (string meta in Directory.GetFiles(folder, "*.meta"))
            {
                string guid = ReadGuid(meta);
                string assetPath = meta.Substring(0, meta.Length - ".meta".Length);
                if (guid == null || !Utils.PathExists(assetPath))
                {
                    continue;
                }

                assets.Add(new Asset
                {
                    Guid = guid,
                    Path = ToProjectPath(projectRoot, assetPath),
                    IsFolder = Directory.Exists(assetPath),
                });
            }

            foreach (string child in Directory.GetDirectories(folder))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith(".") || name.EndsWith("~"))
                {
                    continue;
                }

                AddAssets(projectRoot, child, assets);
            }
        }

        private static string ReadGuid(string metaPath)
        {
            foreach (string line in File.ReadAllLines(metaPath))
            {
                if (line.StartsWith("guid:"))
                {
                    return line.Substring("guid:".Length).Trim();
                }
            }

            return null;
        }

        /// <summary>
        /// What has to move or go so the package folder matches the package.
        /// </summary>
        /// <param name="contents">GUID → asset path of everything in the .unitypackage.</param>
        /// <returns>Null when the .unitypackage isn't this package. A plan with a Problem when it looks wrong.</returns>
        internal static Plan MakePlan(string packageRoot, Dictionary<string, string> contents, List<Asset> installed)
        {
            string root = packageRoot.TrimEnd('/') + "/";

            // Every version ships the same package.json (same GUID), which tells a Build Uploader package apart.
            Asset manifest = installed.FirstOrDefault(a => string.Equals(a.Path, root + "package.json", StringComparison.OrdinalIgnoreCase));
            if (manifest == null || !contents.TryGetValue(manifest.Guid, out string manifestPath) ||
                !string.Equals(manifestPath, manifest.Path, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Plan plan = new Plan();
            foreach (Asset asset in installed)
            {
                if (!contents.TryGetValue(asset.Guid, out string newPath))
                {
                    plan.Deletes.Add(asset);
                }
                else if (!string.Equals(newPath, asset.Path, StringComparison.OrdinalIgnoreCase) &&
                         newPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    plan.Moves.Add(new Move { From = asset.Path, To = newPath, IsFolder = asset.IsFolder });
                }
            }

            // A package missing most of what is installed is not an update of it. Don't delete half the package.
            int installedFiles = installed.Count(a => !a.IsFolder);
            int deletedFiles = plan.Deletes.Count(a => !a.IsFolder);
            if (deletedFiles > installedFiles / 2)
            {
                plan.Problem = $"it would delete {deletedFiles} of the {installedFiles} files in {packageRoot}.";
            }

            return plan;
        }

        /// <summary>
        /// Moves and deletes files with their .meta files. Folders are only removed once empty, so anything without
        /// a .meta that someone put in the package stays.
        /// </summary>
        internal static void Apply(string projectRoot, Plan plan)
        {
            foreach (Move move in plan.Moves.Where(a => !a.IsFolder))
            {
                string from = Path.Combine(projectRoot, move.From);
                string to = Path.Combine(projectRoot, move.To);
                if (!File.Exists(from) || File.Exists(to))
                {
                    plan.Skipped.Add($"{move.From} → {move.To}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Move(from, to);
                if (File.Exists(from + ".meta") && !File.Exists(to + ".meta"))
                {
                    File.Move(from + ".meta", to + ".meta");
                }
            }

            foreach (Asset asset in plan.Deletes.Where(a => !a.IsFolder))
            {
                DeleteFile(Path.Combine(projectRoot, asset.Path));
                DeleteFile(Path.Combine(projectRoot, asset.Path + ".meta"));
            }

            // Deepest first so a folder's empty subfolders are gone before it is looked at.
            List<KeyValuePair<string, string>> folders = plan.Moves.Where(a => a.IsFolder).Select(a => new KeyValuePair<string, string>(a.From, a.To))
                .Concat(plan.Deletes.Where(a => a.IsFolder).Select(a => new KeyValuePair<string, string>(a.Path, null)))
                .OrderByDescending(a => a.Key.Count(c => c == '/'))
                .ToList();
            foreach (KeyValuePair<string, string> folder in folders)
            {
                string from = Path.Combine(projectRoot, folder.Key);
                if (!Directory.Exists(from) || Directory.EnumerateFileSystemEntries(from).Any())
                {
                    if (Directory.Exists(from))
                    {
                        plan.Skipped.Add(folder.Key + " (not empty)");
                    }

                    continue;
                }

                if (folder.Value != null)
                {
                    // Keep the folder's GUID when it moved.
                    string to = Path.Combine(projectRoot, folder.Value);
                    Directory.CreateDirectory(to);
                    if (File.Exists(from + ".meta") && !File.Exists(to + ".meta"))
                    {
                        File.Move(from + ".meta", to + ".meta");
                    }
                }

                Directory.Delete(from);
                DeleteFile(from + ".meta");
            }
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string ToProjectPath(string projectRoot, string fullPath)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(fullPath);
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(root.Length);
            }

            return path.Replace('\\', '/');
        }

        private static string Describe(Plan plan, string packageName)
        {
            StringBuilder text = new StringBuilder();
            text.Append($"Build Uploader tidied up after importing {packageName}: ");
            text.Append($"deleted {plan.Deletes.Count} file(s) and folder(s) the new version no longer has and moved {plan.Moves.Count} to where the new version keeps them.");
            foreach (Asset asset in plan.Deletes)
            {
                text.Append("\nDeleted ").Append(asset.Path);
            }

            foreach (Move move in plan.Moves)
            {
                text.Append("\nMoved ").Append(move.From).Append(" → ").Append(move.To);
            }

            foreach (string skipped in plan.Skipped)
            {
                text.Append("\nLeft ").Append(skipped);
            }

            return text.ToString();
        }
    }
}
