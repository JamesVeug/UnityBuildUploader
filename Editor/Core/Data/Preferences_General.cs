using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Wireframe
{
    public class PreferencesSettings : SettingsProvider
    {
        private readonly Context m_context = new Context();
        private bool m_showFormattedRootPath = Preferences.DefaultShowFormattedTextToggle;

        [SettingsProvider]
        public static SettingsProvider CreateGeneralPreferencesProvider()
        {
            var provider =
                new PreferencesSettings("Preferences/Build Uploader/General", SettingsScope.User)
                {
                    label = "General",
                    keywords = new HashSet<string>(new[]
                    {
                        "Build", "Uploader", "Build Uploader", "BuildUploader", "Pipe", "line", "Pipe line", "Pipeline", "Cache"
                    }),
                };

            return provider;
        }

        private PreferencesSettings(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }
        
        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);
            GUILayout.Label("Preferences for the Build Uploader that exists per user and not shared.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent("Root Path",
                    "A path that every source and destination can start from so they don't all have to repeat it."),
                EditorStyles.boldLabel);
            GUILayout.Label($"Use it with {Context.ROOT_PATH_KEY} in any path, or by setting a sources Path Type to Relative To Root Path. " +
                            $"eg: C:/SomeFolder/Builds/{Context.VERSION_KEY}", EditorStyles.wordWrappedLabel);

            string rootPath = Preferences.RootPath;
            if (CustomFolderPathTextField.OnGUI("Select Root Path", ref rootPath, ref m_showFormattedRootPath, m_context))
            {
                Preferences.RootPath = rootPath;
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent($"Cached Builds ({GetSizeOfCacheFolder()})",
                    "When starting a task, all source files are copied to a temporary location to avoid modifying the originals. This is known as the cache."), 
                EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                string newCachePath = EditorGUILayout.TextField(Preferences.CacheFolderPath);
                if (newCachePath != Preferences.CacheFolderPath)
                {
                    Preferences.CacheFolderPath = newCachePath;
                    cachedSizeTime = DateTime.MinValue;
                }
                if (GUILayout.Button("...", GUILayout.Width(50)))
                {
                    string newPath = EditorUtility.OpenFolderPanel("Select Cache Folder", Preferences.CacheFolderPath, "");
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        Preferences.CacheFolderPath = newPath;
                        cachedSizeTime = DateTime.MinValue;
                    }
                }
                if (GUILayout.Button("Show", GUILayout.Width(50)))
                {
                    EditorUtility.RevealInFinder(Preferences.CacheFolderPath);
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Delete cache after uploading",
                        "If enabled, the cache folder of a build will be deleted after completion. This is useful to save space."),
                    GUILayout.Width(200));

                bool cacheAfterBuild = Preferences.DeleteCacheAfterUpload;
                bool newCacheAfterBuild = EditorGUILayout.Toggle(cacheAfterBuild);
                if (newCacheAfterBuild != Preferences.DeleteCacheAfterUpload)
                {
                    Preferences.DeleteCacheAfterUpload = newCacheAfterBuild;
                }
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Default Description:", 
                        "Description in the Upload tab to start with when opened. See docs for possible string formats such as {version}."), 
                    GUILayout.Width(200));

                string descFormat = Preferences.DefaultDescriptionFormat;
                string newDescFormat = EditorGUILayout.TextField(descFormat);
                if (newDescFormat != Preferences.DefaultDescriptionFormat)
                {
                    Preferences.DefaultDescriptionFormat = newDescFormat;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Save contents to LocalDestination", 
                        "If enabled and an upload config has a LocalDestination with no compression, Upload Tasks will save all source contents to the first LocalDestination instead of a temporary location. This will speed up uploading - especially for projects with many files."), 
                    GUILayout.Width(200));

                bool useLocalDestination = Preferences.UseLocalDestinationIfAvailable;
                bool newUseLocalDestination = EditorGUILayout.Toggle(useLocalDestination);
                if (newUseLocalDestination != Preferences.UseLocalDestinationIfAvailable)
                {
                    Preferences.UseLocalDestinationIfAvailable = newUseLocalDestination;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Auto save upload configs", 
                        "If enabled, after every change made to upload configs in the upload tab they will be saved. If disabled then requires pressing the save button to retain your changes."), 
                    GUILayout.Width(200));

                bool autoSave = Preferences.AutoSaveUploadConfigsAfterChanges;
                bool newAutoSave = EditorGUILayout.Toggle(autoSave);
                if (newAutoSave != Preferences.AutoSaveUploadConfigsAfterChanges)
                {
                    Preferences.AutoSaveUploadConfigsAfterChanges = newAutoSave;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Auto decompress source .zips", 
                        "If enabled, a Source that contains only a .zip it will be decompressed when being copied over to the cache."), 
                    GUILayout.Width(200));

                bool autoDecompress = Preferences.AutoDecompressZippedSourceFiles;
                bool newAutoDecompress = EditorGUILayout.Toggle(autoDecompress);
                if (newAutoDecompress != Preferences.AutoDecompressZippedSourceFiles)
                {
                    Preferences.AutoDecompressZippedSourceFiles = newAutoDecompress;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Auto save upload reports", "" +
                                                              "If enabled, upload reports made from the UI will be auto-saved to the cache folder after completion."),
                    GUILayout.Width(200));

                bool autoSave = Preferences.AutoSaveReportToCacheFolder;
                bool newAutoSave = EditorGUILayout.Toggle(autoSave);
                if (newAutoSave != Preferences.AutoSaveReportToCacheFolder)
                {
                    Preferences.AutoSaveReportToCacheFolder = newAutoSave;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Auto generate Quick Upload items", "" +
                                                               "If enabled, a new .cs file will be generated when recompiling code which will add Menu Items for each Upload Profile." +
                                                               "\n\nSee 'Window->Build Uploader->Quick Upload' when generated."),
                    GUILayout.Width(200));

                bool autoSave = Preferences.AutoGenerateMenuItems;
                bool newAutoSave = EditorGUILayout.Toggle(autoSave);
                if (newAutoSave != Preferences.AutoGenerateMenuItems)
                {
                    Preferences.AutoGenerateMenuItems = newAutoSave;
                }
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI", EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Show Welcome window", Preferences.WelcomeWindowPopupTooltip),
                    GUILayout.Width(200));

                int showWelcome = (int)Preferences.ShowWelcomeWindow;
                int newShowWelcome = EditorGUILayout.Popup(showWelcome, Preferences.WelcomeWindowPopupOptions);
                if (newShowWelcome != showWelcome)
                {
                    Preferences.ShowWelcomeWindow = (Preferences.WelcomeWindowPopup)newShowWelcome;
                    Preferences.LastSeenWelcomeVersion = Utils.PackageVersion;
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Auto show new Upload Tasks", 
                        "If enabled, when starting new Upload tasks using the GUI the Task Report window will open and follow the new upload task."), 
                    GUILayout.Width(200));

                bool autoFocusTask = Preferences.AutoFocusNewUploadTask;
                bool newAutoFocusTask = EditorGUILayout.Toggle(autoFocusTask);
                if (newAutoFocusTask != Preferences.AutoFocusNewUploadTask)
                {
                    Preferences.AutoFocusNewUploadTask = newAutoFocusTask;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Default show formatted text on", 
                        "If enabled, all toggles to show text fields/areas as their formatted text will start on by default eg:(#{buildversion} = #42)\n" +
                        "Can be toggled per field with the \"F\" button next to the field."), 
                    GUILayout.Width(200));

                bool autoFocusTask = Preferences.DefaultShowFormattedTextToggle;
                bool newAutoFocusTask = EditorGUILayout.Toggle(autoFocusTask);
                if (newAutoFocusTask != Preferences.DefaultShowFormattedTextToggle)
                {
                    Preferences.DefaultShowFormattedTextToggle = newAutoFocusTask;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Hide black formatted text keys", 
                        "If enabled, tool tips on the \"F\" button will not show keys that are blank to '???'"),
                    GUILayout.Width(200));

                bool hideBlackToolTips = Preferences.ToolTipsHideBlackValuesToggle;
                bool newHideBlackToolTips = EditorGUILayout.Toggle(hideBlackToolTips);
                if (newHideBlackToolTips != Preferences.ToolTipsHideBlackValuesToggle)
                {
                    Preferences.ToolTipsHideBlackValuesToggle = newHideBlackToolTips;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Show upload confirmation window", 
                        "If enabled, a popup window will appear indicating if the upload was successful and if not why not."), 
                    GUILayout.Width(200));

                Preferences.ShowIf showConfirmations = Preferences.ShowConfirmationWindowAfterUpload;
                Preferences.ShowIf newShowConfirmations = (Preferences.ShowIf)EditorGUILayout.EnumPopup(showConfirmations);
                if (newShowConfirmations != Preferences.ShowConfirmationWindowAfterUpload)
                {
                    Preferences.ShowConfirmationWindowAfterUpload = newShowConfirmations;
                }
            }
            
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Show upload report after uploading", "" +
                                                              "If enabled, when an upload completes a window will appear showing all information about what it did."),
                    GUILayout.Width(200));

                Preferences.ShowIf showReport = Preferences.ShowReportAfterUpload;
                Preferences.ShowIf newShowReport = (Preferences.ShowIf)EditorGUILayout.EnumPopup(showReport);
                if (newShowReport != Preferences.ShowReportAfterUpload)
                {
                    Preferences.ShowReportAfterUpload = newShowReport;
                }
            }
        }
        
        private string cachedSize;
        private DateTime cachedSizeTime = DateTime.MinValue;
        private string GetSizeOfCacheFolder()
        {
            // Fetch the size once a minute to avoid iterating the entire cache folder every time GUI is drawn.
            if (!string.IsNullOrEmpty(cachedSize) && (DateTime.UtcNow - cachedSizeTime).TotalSeconds < 60)
            {
                return cachedSize;
            }
            
            if (!System.IO.Directory.Exists(Preferences.CacheFolderPath))
            {
                cachedSize = "0 bytes";
                return cachedSize;
            }

            long size = 0;
            try
            {
                foreach (string file in System.IO.Directory.GetFiles(Preferences.CacheFolderPath, "*",
                             System.IO.SearchOption.AllDirectories))
                {
                    size += new System.IO.FileInfo(file).Length;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to calculate cache folder size: " + e.Message);
                cachedSize = "Error calculating size";
                return cachedSize;
            }


            cachedSize = EditorUtility.FormatBytes(size);
            cachedSizeTime = DateTime.UtcNow;
            return cachedSize;
        }
    }
}