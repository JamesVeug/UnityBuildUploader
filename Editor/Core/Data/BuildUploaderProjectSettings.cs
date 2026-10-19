using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public class BuildUploaderProjectSettings
    {
        private static readonly string FilePath = Application.dataPath + "/../BuildUploader/ProjectSettings.json";
        private static readonly int CurrentVersion = 1;
        
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
        public int LastBuildNumber;
        public int TotalUploadTasksStarted;
        public string AutoGenerateMenuItemPath = "ThirdParty/BuildUploader";

        // When true, starting uploads and builds never changes the two numbers above or rewrites this file, so it
        // stays clean in version control (#109). {buildNumber} and {uploadNumber} then repeat between runs.
        public bool SkipBuildAndUploadNumberUpdates;

        public BuildUploaderProjectSettings()
        {
            Version = CurrentVersion;
        }
        
        private static void LoadFile()
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                BuildUploaderProjectSettings savedData = JSON.DeserializeObject<BuildUploaderProjectSettings>(json);
                if (savedData != null)
                {
                    _instance = savedData;
                    return;
                }
            }

            _instance = new BuildUploaderProjectSettings();
            Save();
        }
        
        public static void Save()
        {
            if (_instance != null)
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                string json = JSON.SerializeObject(_instance);
                File.WriteAllText(FilePath, json);
            }
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
        
        public static void BumpUploadNumber()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            if (settings.SkipBuildAndUploadNumberUpdates)
            {
                return;
            }

            settings.TotalUploadTasksStarted++;
            BuildUploaderProjectSettings.Save();
        }

        public static void BumpBuildNumber()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            if (settings.SkipBuildAndUploadNumberUpdates)
            {
                return;
            }

            settings.LastBuildNumber++;
            BuildUploaderProjectSettings.Save();
        }

        public static BuildMetaData CreateFromProjectSettings()
        {
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;

            BuildMetaData metaData = new BuildMetaData();
            metaData.BuildNumber = settings.LastBuildNumber;
            metaData.UploadNumber = settings.TotalUploadTasksStarted;
            
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
    }
}