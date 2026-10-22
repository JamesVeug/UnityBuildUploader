using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wireframe
{
    public static class Utils
    {
        public enum FileExistHandling
        {
            Error,
            Skip,
            Overwrite,
        }

        internal static string s_packagePath = FindPackagePath();
        private static string s_packageVersion;

        private static string FindPackagePath()
        {
            string[] guids = AssetDatabase.FindAssets("veugeljame.builduploader.editor");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("VeugelJame.BuildUploader.Editor.asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                string parentFolder = Path.GetDirectoryName(Path.GetDirectoryName(path));
                return parentFolder;
            }

            Debug.LogError("Could not find package path for com.veugeljame.builduploader");
            return "";
        }


        public static string PackageVersion
        {
            get
            {
                if (s_packageVersion != null)
                {
                    return s_packageVersion;
                }

                s_packageVersion = "";
                try
                {
                    string path = Path.Combine(s_packagePath, "package.json");
                    if (File.Exists(path))
                    {
                        var data = JSON.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(path));
                        if (data != null && data.TryGetValue("version", out object version) && version != null)
                        {
                            s_packageVersion = version.ToString();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read version from package.json: {e.Message}");
                }

                return s_packageVersion;
            }
        }

        private static Dictionary<string, Texture2D> s_Icons = new Dictionary<string, Texture2D>();
        private static Texture2D TryGetIcon(string iconPath)
        {
            if (s_Icons.TryGetValue(iconPath, out Texture2D icon))
            {
                return icon;
            }

            string path = Path.Combine(s_packagePath, "Assets", iconPath);
            Object loadAssetAtPath = AssetDatabase.LoadAssetAtPath(path, typeof(Object));
            if (loadAssetAtPath is Texture2D texture)
            {
                s_Icons[iconPath] = texture;
                return texture;
            }

            Debug.LogWarning($"Could not find icon at path: {path}");
            return null;
        }

        public static bool IsDarkMode => EditorGUIUtility.isProSkin;
        public static Texture2D WindowIcon => TryGetIcon(IsDarkMode ? "Icon.png" : "IconLight.png");
        public static Texture2D WindowLargeIcon => TryGetIcon("LargeIcon.png");
        public static Texture2D ErrorIcon => TryGetIcon(IsDarkMode ? "errorIcon.png" : "errorIconLight.png");
        public static Texture2D WarningIcon => TryGetIcon(IsDarkMode ? "warningIcon.png" : "warningIconLight.png");
        public static Texture2D FoldoutOpenIcon => TryGetIcon("FoldoutOpen.png");
        public static Texture2D FoldoutClosedIcon => TryGetIcon("FoldoutClosed.png");
        public static Texture2D SettingsIcon => TryGetIcon(IsDarkMode ? "Settings.png" : "SettingsLight.png");
        public static Texture2D CheckIcon => TryGetIcon(IsDarkMode ? "checkIcon.png" : "checkIconLight.png");
        public static Texture2D CheckIconSmall => TryGetIcon(IsDarkMode ? "checkIconSmall.png" : "checkIconSmallLight.png");
        public static Texture2D CrossIcon => TryGetIcon(IsDarkMode ? "crossIcon.png" : "crossIconLight.png");
        public static Texture2D CrossIconSmall => TryGetIcon(IsDarkMode ? "crossIconSmall.png" : "crossIconSmallLight.png");
        public static Texture2D LinkIcon => TryGetIcon("linkIcon.png");
        public static Texture2D DownloadIcon => TryGetIcon(IsDarkMode ? "DownloadIcon.png" : "DownloadIconLight.png");

        /// <summary>
        /// https://stackoverflow.com/questions/2281531/how-can-i-compare-directory-paths-in-c
        /// </summary>
        public static bool ComparePaths(string path1, string path2)
        {
            string Normalize(string path)
            {
                string fullPath = Path.GetFullPath(path);
                string root = Path.GetPathRoot(fullPath);
                while (fullPath.Length > root.Length &&
                       (fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar ||
                        fullPath[fullPath.Length - 1] == Path.AltDirectorySeparatorChar))
                {
                    fullPath = fullPath.Substring(0, fullPath.Length - 1);
                }
                return fullPath;
            }

            // Unix volumes can be case-sensitive, including macOS APFS volumes.
#if UNITY_EDITOR_WIN
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
#else
            const StringComparison comparison = StringComparison.Ordinal;
#endif
            return string.Equals(Normalize(path1), Normalize(path2), comparison);
        }
        
        public static bool CompareDirectories(string path1, string path2)
        {
            string directory1 = IsPathADirectory(path1) ? path1 : Path.GetDirectoryName(path1);
            string directory2 = IsPathADirectory(path2) ? path2 : Path.GetDirectoryName(path2);
            return ComparePaths(directory1, directory2);
        }

        public static string GetDirectoryOrFileDirectory(string path)
        {
            return IsPathADirectory(path) ? path : Path.GetDirectoryName(path);
        }
        
        public static bool IsPathADirectory(string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.Directory) == FileAttributes.Directory;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
        }

        public static async Task<bool> CopyFileAsync(string source, string destination, FileExistHandling dupeFileHandling, UploadTaskReport.StepResult result = null)
        {
            try{
                if (destination.Length > MaxFilePath)
                {
                    throw new FilePathTooIsLongException();
                }
                
                if (File.Exists(destination))
                {
                    switch (dupeFileHandling)
                    {
                        case FileExistHandling.Error:
                            result?.SetFailed("File already exists: " + destination);
                            return false;
                        case FileExistHandling.Skip:
                            result?.SetSkipped("Skipping duplicate file since it already exists: " + destination);
                            return true;
                    }
                }
                
                // Native copying preserves Unix executable permissions; copying stream bytes does not.
                await Task.Run(() => File.Copy(source, destination, dupeFileHandling == FileExistHandling.Overwrite));
            }
            catch (Exception e)
            {
                if (result != null)
                {
                    result.AddException(e);
                    result.SetFailed("Failed to copy directory: " + source + " to " + destination);
                }
                else
                {
                    Debug.LogException(e);
                }
                return false;
            }

            return true;
        }
        
        public static async Task<bool> CopyDirectoryAsync(string source, string destination, FileExistHandling dupeFileHandling, UploadTaskReport.StepResult result = null, Func<string, bool> ignore = null)
        {
            try
            {
                foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                {
                    if (ignore != null && ignore(dirPath))
                    {
                        continue;
                    }

                    string newDirectory = dirPath.Replace(source, destination);
                    if (newDirectory.Length > MaxFilePath)
                    {
                        throw new FilePathTooIsLongException();
                    }
                    
                    Directory.CreateDirectory(newDirectory);
                }

                string[] paths = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
                if (ignore != null)
                {
                    paths = paths.Where(a=>!ignore(a)).ToArray();
                }

                for (var i = 0; i < paths.Length; i++)
                {
                    var newPath = paths[i];
                    string destinationFile = newPath.Replace(source, destination);
                    if (destinationFile.Length > MaxFilePath)
                    {
                        throw new FilePathTooIsLongException();
                    }

                    string directory = Path.GetDirectoryName(destinationFile);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (result != null)
                    {
                        float percent = (float)i / (float)paths.Length;
                        result.SetPercentComplete(percent);
                    }

                    if (!await CopyFileAsync(newPath, destinationFile, dupeFileHandling, result))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (IOException e)
            {
                if (result != null)
                {
                    result.AddException(e);
                    result.SetFailed("Failed to copy directory: " + source + " to " + destination);
                }
                else
                {
                    Debug.LogException(e);
                }

                return false;
            }
        }

        public static string TruncateText(string m_enteredFilePath, float maxWidth, string defaultText)
        {
            string displayedPath = m_enteredFilePath;
            if (!string.IsNullOrEmpty(displayedPath))
            {
                float characterWidth = 8f;
                int characters = displayedPath.Length;
                float expectedWidth = characterWidth * characters;
                if (expectedWidth >= maxWidth)
                {
                    int charactersToRemove = (int)((expectedWidth - maxWidth) / characterWidth);
                    if (charactersToRemove < displayedPath.Length)
                    {
                        displayedPath = displayedPath.Substring(charactersToRemove);
                    }
                    else
                    {
                        displayedPath = "";
                    }
                }
            
                if(displayedPath.Length < m_enteredFilePath.Length)
                {
                    displayedPath = "..." + displayedPath;
                }
            }
            else
            {
                displayedPath = defaultText;
            }

            return displayedPath;
        }
        
        public static string Replace(string text, string replace, string with, StringComparison compare)
        {
#if UNITY_2021_1_OR_NEWER
            return text.Replace(replace, with, compare);
#else
            int index = text.IndexOf(replace, compare);
            while(index >= 0)
            {
                text = text.Remove(index, replace.Length).Insert(index, with);
                index = text.IndexOf(replace, index + with.Length, compare);
            }
            return text;
#endif
        }
        
        public static int IndexOf(string text, string otherString, StringComparison compare)
        {
            return text.IndexOf(otherString, compare);
        }
        
        public static bool Contains(string text, string otherString, StringComparison compare)
        {
#if UNITY_2021_1_OR_NEWER
            return text.Contains(otherString, compare);
#else
            return text.IndexOf(otherString, compare) >= 0;
#endif
        }
        
        public static List<string> GetSortedFilesAndDirectories(string directory)
        {
            if (File.Exists(directory)) 
            {
                List<string> singleFile = new List<string>();
                singleFile.Add(directory);
                return singleFile;
            }
            
            // Log out every file in this directory
            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            string[] folders = Directory.GetDirectories(directory, "*", SearchOption.AllDirectories);

            List<string> allFiles = files.Concat(folders).Distinct().ToList();
            allFiles.Sort(CompareFileNames);

            return allFiles;
        }

        private static int CompareFileNames(string a, string b)
        {
            // Order by folder depth first, then by name
            var aKey = a.Split(Path.DirectorySeparatorChar);
            int aLength = aKey.Length;

            var bKey = b.Split(Path.DirectorySeparatorChar);
            int bLength = bKey.Length;

            for (int aIndex = 0; aIndex < Mathf.Min(aKey.Length, bKey.Length); aIndex++)
            {
                string strA = bKey[aIndex];
                string strB = aKey[aIndex];
                int compare = string.Compare(strA, strB, StringComparison.Ordinal);
                if (compare != 0)
                {
                    return compare;
                }
            }

            if (aLength != bLength)
            {
                return aLength - bLength;
            }

            return string.Compare(a, b, StringComparison.Ordinal);
        }

        public static bool CreateInstance<T>(Type type, out T result)
        {
            if (type == null)
            {
                result = default(T);
                return false;
            }
            
            ConstructorInfo ci = type.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            if (ci != null)
            {
                result = (T)ci.Invoke(null);
                return true;
            }

            Debug.LogError($"Could not create Type {type}. Missing empty constructor.");
            result = default(T);
            return false;
        }

        /// <summary>
        /// https://stackoverflow.com/questions/7140575/mac-os-x-lion-what-is-the-max-path-length
        /// </summary>
        public static int MaxFilePath
        {
            get
            {
#if UNITY_EDITOR_WIN
                return 260; // Windows
#elif UNITY_EDITOR_OSX
                return 255; // macOS (or 1024 for HFS+ if you believe
#elif UNITY_EDITOR_LINUX
                return 4096; // macOS (or 255 for HFS+ if you believe Apple)
#else
                return 4096; // unknown but this seems random and
#endif
            }
        }

        public static string SummarizeErrors(BuildReport report)
        {
            StringBuilder summarizedErrors = new StringBuilder();
            if (report.summary.result != BuildResult.Succeeded)
            {
                summarizedErrors.AppendLine($"Build failed with result: {report.summary.result}");
            }
            else
            {
                summarizedErrors.AppendLine("Build succeeded.");
            }
            
            foreach (BuildStep step in report.steps)
            {
                foreach (BuildStepMessage message in step.messages)
                {
                    switch (message.type)
                    {
                        case LogType.Error:
                        case LogType.Exception:
                            summarizedErrors.AppendLine(message.content);
                            break;
                    }
                }
            }

            return summarizedErrors.ToString();
        }

        public static bool PathContainsInvalidCharacters(string path)
        {
            return !string.IsNullOrEmpty(path) && path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
        }

        public static bool FileNameContainsInvalidCharacters(string fileName)
        {
            return fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;
        }

        public static bool DrawPathAsButton(ref string fullPath, string emptyPathText, float maxWidth)
        {
            string displayedPath = fullPath;
            if (!string.IsNullOrEmpty(displayedPath))
            {
                float characterWidth = 8f;
                int characters = displayedPath.Length;
                float expectedWidth = characterWidth * characters;
                if (expectedWidth >= maxWidth)
                {
                    int charactersToRemove = (int)((expectedWidth - maxWidth) / characterWidth);
                    if (charactersToRemove < displayedPath.Length)
                    {
                        displayedPath = displayedPath.Substring(charactersToRemove);
                    }
                    else
                    {
                        displayedPath = "";
                    }
                }
                
                if(displayedPath.Length < fullPath.Length)
                {
                    displayedPath = "..." + displayedPath;
                }
            }
            else
            {
                displayedPath = emptyPathText;
            }
            
            // TODO: Cache this
            var pathButtonExistsStyle = new GUIStyle(GUI.skin.button);
            var pathButtonDoesNotExistStyle = new GUIStyle(GUI.skin.button);
            pathButtonDoesNotExistStyle.normal.textColor = Color.yellow;
            
            GUIStyle style = PathExists(displayedPath) ? pathButtonExistsStyle : pathButtonDoesNotExistStyle;
            style.alignment = TextAnchor.MiddleLeft;
            
            if (GUILayout.Button(displayedPath, style))
            {
                string newPath = EditorUtility.OpenFolderPanel("Select Folder to upload", ".", "");
                fullPath = newPath;
                return true;
            }

            return false;
        }

        public static bool PathExists(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) || Directory.Exists(path);
        }
        
        public static string ToSemantic(string version)
        {
            StringBuilder summary = new StringBuilder();
            foreach (char c in version)
            {
                if (char.IsDigit(c) || c == '.')
                {
                    summary.Append(c);
                }
            }

            return summary.ToString();
        }
        
        public enum VersionSegment
        {
            Major,
            Minor,
            Patch,
            Revision
        }
        
        public static string VersionSegmentToString(string version, VersionSegment segment)
        {
            // Split by . or -
            // a1.2.3-rc1 -> ["a1","2","3","rc1"]
            string[] strings = version.Split('.', '-');
            switch (segment)
            {
                case VersionSegment.Major:
                    return strings.Length > 0 ? strings[0] : "";
                case VersionSegment.Minor:
                    return strings.Length > 1 ? strings[1] : "";
                case VersionSegment.Patch:
                    return strings.Length > 2 ? strings[2] : "";
                case VersionSegment.Revision:
                    return strings.Length > 3 ? strings[3] : "";
                default:
                    throw new ArgumentOutOfRangeException(nameof(segment), segment, null);
            }
        }

        public static int GetClosingBracketIndex(string text, int startIndex, char startBracket = '{',
            char endBracket = '}')
        {
            if (string.IsNullOrEmpty(text))
            {
                return -1;
            }

            int textLength = text.Length;
            
            int index = startIndex + 1;
            int depth = 1;
            while (index < textLength)
            {
                if (text[index] == startBracket)
                {
                    depth++;
                }
                else if (text[index] == endBracket)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return index;
                    }
                }
                
                index++;
            }

            return -1;
        }
    }
}
