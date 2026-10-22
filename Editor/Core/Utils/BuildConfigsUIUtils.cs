using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal static class BuildConfigsUIUtils
    {
        internal static readonly string FilePath = Application.dataPath + "/../BuildUploader/BuildConfigs.json";

        public class BuildConfigPopup : CustomDropdown<BuildConfig>
        {
            public override string FirstEntryText => "Choose Build Config";

            protected override List<BuildConfig> FetchAllData()
            {
                GetBuildConfigs();
                return data;
            }
        }

        private static List<BuildConfig> data;

        public static List<BuildConfig> GetBuildConfigs()
        {
            if (data == null)
            {
                LoadFile();
            }
            return data;
        }

        public static bool TryLoadFromGUID(string guid, out BuildConfig buildConfig)
        {
            if (!File.Exists(FilePath))
            {
                buildConfig = null;
                return false;
            }
            
            string json = File.ReadAllText(FilePath);
            SaveData savedData = JSON.DeserializeObject<SaveData>(json);
            if (savedData != null && savedData.Configs != null && savedData.Configs.Count > 0)
            {
                for (var i = 0; i < savedData.Configs.Count; i++)
                {
                    var saveData = savedData.Configs[i];
                    if (!saveData.TryGetValue("GUID", out object obj))
                    {
                        continue;
                    }

                    if (!(obj is string id) || id != guid)
                    {
                        continue;
                    }

                    BuildConfig config = new BuildConfig();
                    try
                    {
                        config.Deserialize(saveData);
                        buildConfig = config;
                        return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        buildConfig = null;
                        return false;
                    }
                }
            }
            
            buildConfig = null;
            return false;
        }

        public static bool TryLoadFromBuildName(string name, out BuildConfig buildConfig)
        {
            if (!File.Exists(FilePath))
            {
                buildConfig = null;
                return false;
            }
            
            string json = File.ReadAllText(FilePath);
            SaveData savedData = JSON.DeserializeObject<SaveData>(json);
            if (savedData != null && savedData.Configs != null && savedData.Configs.Count > 0)
            {
                for (var i = 0; i < savedData.Configs.Count; i++)
                {
                    var saveData = savedData.Configs[i];
                    if (!saveData.TryGetValue("BuildName", out object obj))
                    {
                        continue;
                    }

                    if (!(obj is string id) || id != name)
                    {
                        continue;
                    }

                    BuildConfig config = new BuildConfig();
                    try
                    {
                        config.Deserialize(saveData);
                        buildConfig = config;
                        return true;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        buildConfig = null;
                        return false;
                    }
                }
            }
            
            buildConfig = null;
            return false;
        }

		[MenuItem("Tools/Build Uploader/Other/Reload Build Configs")]
        private static void LoadFile()
        {
            data = new List<BuildConfig>();
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                SaveData savedData = JSON.DeserializeObject<SaveData>(json);
                if (savedData != null && savedData.Configs != null && savedData.Configs.Count > 0)
                {
                    int id = 1;
                    for (var i = 0; i < savedData.Configs.Count; i++)
                    {
                        var saveData = savedData.Configs[i];
                        BuildConfig config = new BuildConfig();
                        try
                        {
                            config.Deserialize(saveData);
                            config.Id = id++;
                            data.Add(config);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }

                    return;
                }
            }

            CreateDefaultConfigs();
            Save();
        }

        public static void CreateDefaultConfigs()
        {
            BuildConfig debugBuild = new BuildConfig();
            debugBuild.Id = data.Count > 0 ? data.Max(a=>a.Id) + 1 : 1;
            debugBuild.SetEditorSettings();
            debugBuild.BuildName = "Debugging Build";
            debugBuild.SetDebuggingOn(true);
            data.Add(debugBuild);

            BuildConfig releaseBuild = new BuildConfig();
            releaseBuild.Id = data.Count > 0 ? data.Max(a=>a.Id) + 1 : 1;
            releaseBuild.SetEditorSettings();
            releaseBuild.BuildName = "Release Build";
#if UNITY_2021_1_OR_NEWER
            releaseBuild.StrippingLevel = ManagedStrippingLevel.Minimal;
#else
            releaseBuild.StrippingLevel = ManagedStrippingLevel.Low;
#endif
            releaseBuild.SetDebuggingOn(false);
            data.Add(releaseBuild);
        }
        
        public static void Clear()
        {
            data.Clear();
        }

        public static void Save()
        {
            if (data != null)
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                SaveData saveData = new SaveData();
                saveData.Version = SaveData.CurrentVersion;
                saveData.Configs = new List<Dictionary<string, object>>();
                foreach (BuildConfig config in data)
                {
                    Dictionary<string, object> serializedData = config.Serialize();
                    if (serializedData == null)
                    {
                        Debug.LogWarning("BuildConfig data is null. Skipping this config.");
                        continue;
                    }
                    
                    saveData.Configs.Add(serializedData);
                }

                string json = JSON.SerializeObject(saveData);
                File.WriteAllText(FilePath, json);
            }
        }

        public static BuildConfigPopup BuildConfigsPopup => m_buildConfigsPopup ?? (m_buildConfigsPopup = new BuildConfigPopup());
        private static BuildConfigPopup m_buildConfigsPopup;

        public class SaveData
        {
            public const int CurrentVersion = 1;
            
            public int Version;
            public List<Dictionary<string, object>> Configs = new List<Dictionary<string, object>>();
        }
    }
}