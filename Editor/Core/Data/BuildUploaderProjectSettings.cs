using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class BuildUploaderProjectSettings
    {
        private static readonly string FilePath = Application.dataPath + "/../BuildUploader/ProjectSettings.json";
        private const int CurrentVersion = 2;
        
        private static BuildUploaderProjectSettings _instance;
        public static BuildUploaderProjectSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    LoadFile();
                }

                return _instance;
            }
        }
        
        
        public int Version;
        public bool IncludeBuildMetaDataInStreamingDataFolder = true;
        public string AutoGenerateMenuItemPath = "ThirdParty/BuildUploader";

        /// <summary>
        /// Retained for source compatibility and forwards to <see cref="BuildUploaderProjectState.LastBuildNumber"/>.
        /// New code should use <see cref="BuildUploaderProjectState"/> directly.
        /// </summary>
        public int LastBuildNumber
        {
            get => BuildUploaderProjectState.Instance.LastBuildNumber;
            set => BuildUploaderProjectState.Instance.LastBuildNumber = value;
        }

        /// <summary>
        /// Retained for source compatibility and forwards to <see cref="BuildUploaderProjectState.TotalUploadTasksStarted"/>.
        /// New code should use <see cref="BuildUploaderProjectState"/> directly.
        /// </summary>
        public int TotalUploadTasksStarted
        {
            get => BuildUploaderProjectState.Instance.TotalUploadTasksStarted;
            set => BuildUploaderProjectState.Instance.TotalUploadTasksStarted = value;
        }

        public BuildUploaderProjectSettings()
        {
            Version = CurrentVersion;
        }
        
        internal static void EnsureLoaded()
        {
            if (_instance == null)
            {
                LoadFile();
            }
        }

        private static void LoadFile()
        {
            _instance = Load(FilePath, BuildUploaderProjectState.FilePath, out BuildUploaderProjectState state);
            BuildUploaderProjectState.SetInstance(state);
        }

        internal static BuildUploaderProjectSettings Load(
            string settingsFilePath,
            string stateFilePath,
            out BuildUploaderProjectState state)
        {
            if (File.Exists(settingsFilePath))
            {
                string json = File.ReadAllText(settingsFilePath);
                BuildUploaderProjectSettings savedData = JSON.DeserializeObject<BuildUploaderProjectSettings>(json);
                if (savedData != null)
                {
                    LegacyProjectSettingsData legacyData = JSON.DeserializeObject<LegacyProjectSettingsData>(json);
                    bool stateExists = File.Exists(stateFilePath);
                    state = BuildUploaderProjectState.Load(stateFilePath, false);

                    if ((legacyData?.Version ?? 0) < CurrentVersion)
                    {
                        if (!stateExists)
                        {
                            state.LastBuildNumber = legacyData?.LastBuildNumber ?? 0;
                            state.TotalUploadTasksStarted = legacyData?.TotalUploadTasksStarted ?? 0;
                            BuildUploaderProjectState.Save(state, stateFilePath);
                        }

                        savedData.Version = CurrentVersion;
                        Save(savedData, settingsFilePath);
                    }
                    else if (!stateExists)
                    {
                        BuildUploaderProjectState.Save(state, stateFilePath);
                    }

                    return savedData;
                }
            }

            state = BuildUploaderProjectState.Load(stateFilePath);
            var settings = new BuildUploaderProjectSettings();
            Save(settings, settingsFilePath);
            return settings;
        }
        
        public static void Save()
        {
            if (_instance != null)
            {
                BuildUploaderProjectState.SaveIfLoaded();
                Save(_instance, FilePath);
            }
        }

        private static void Save(BuildUploaderProjectSettings settings, string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JSON.SerializeObject(settings);
            File.WriteAllText(filePath, json);
        }
        
        public static void SaveToStreamingAssets(BuildMetaData meta, BuildPlayerOptions options, string buildPath)
        {
            if (!Directory.Exists(buildPath))
            {
                buildPath = Path.GetDirectoryName(buildPath);
            }
        
            // TODO: Support non-mono builds
            
            string streamingAssetPath = "";
            string[] streamingAssetsFolders = Directory.GetDirectories(buildPath, "StreamingAssets", SearchOption.AllDirectories);
            if (streamingAssetsFolders.Length == 0)
            {
                if (options.target == BuildTarget.WebGL)
                {
                    string[] indexFiles = Directory.GetFiles(buildPath, "index.html", SearchOption.AllDirectories);
                    if (indexFiles.Length > 0)
                    {
                        streamingAssetPath = Path.Combine(Path.GetDirectoryName(indexFiles[0]), "StreamingAssets");
                    }
                    else
                    {
                        Debug.LogError($"Failed to find index.html for WebGL build to include build meta data in path '{buildPath}'.");
                        return;
                    }
                }
                else if(options.targetGroup == BuildTargetGroup.Standalone)
                {
                    // Standalone.
                    string[] resourceFolders = Directory.GetDirectories(buildPath, "Resources", SearchOption.AllDirectories);
                    if (resourceFolders.Length > 0)
                    {
                        streamingAssetPath = Path.Combine(Path.GetDirectoryName(resourceFolders[0]), "StreamingAssets");
                    }
                    else
                    {
                        Debug.LogError($"Failed to find StreamingAssets folder to include build meta data in path '{buildPath}'.");
                        return;
                    }
                }
                else
                {
                    // TODO: More platforms
                    Debug.LogWarning($"Unsupported build target group '{options.targetGroup}' for build meta data inclusion.");
                }
            }
            else
            {
                streamingAssetPath = streamingAssetsFolders[0];
            }

            if (!Directory.Exists(streamingAssetPath))
            {
                Directory.CreateDirectory(streamingAssetPath);
            }
            
            string json = JsonUtility.ToJson(meta, true);
            File.WriteAllText(streamingAssetPath + "/BuildData.json", json);
        }
        
        /// <summary>
        /// Retained for source compatibility and forwards to <see cref="BuildUploaderProjectState.BumpUploadNumber"/>.
        /// New code should use <see cref="BuildUploaderProjectState"/> directly.
        /// </summary>
        public static void BumpUploadNumber()
        {
            BuildUploaderProjectState.BumpUploadNumber();
        }
        
        /// <summary>
        /// Retained for source compatibility and forwards to <see cref="BuildUploaderProjectState.BumpBuildNumber"/>.
        /// New code should use <see cref="BuildUploaderProjectState"/> directly.
        /// </summary>
        public static void BumpBuildNumber()
        {
            BuildUploaderProjectState.BumpBuildNumber();
        }

        public static BuildMetaData CreateFromProjectSettings()
        {
            BuildUploaderProjectState state = BuildUploaderProjectState.Instance;

            BuildMetaData metaData = new BuildMetaData();
            metaData.BuildNumber = state.LastBuildNumber;
            metaData.UploadNumber = state.TotalUploadTasksStarted;
            
            return metaData;
        }

        public static BuildMetaData CreateFromContext(Context context)
        {
            BuildMetaData metaData = new BuildMetaData();
            context.FormatKey(Context.BUILD_NUMBER_KEY, out string buildNumberStr);
            metaData.BuildNumber = int.Parse(buildNumberStr);
            
            context.FormatKey(Context.UPLOAD_NUMBER_KEY, out string uploadNumberStr);
            metaData.UploadNumber = int.Parse(uploadNumberStr);
            
            return metaData;
        }

        private class LegacyProjectSettingsData
        {
            public int Version;
            public int LastBuildNumber;
            public int TotalUploadTasksStarted;
        }
    }
}
