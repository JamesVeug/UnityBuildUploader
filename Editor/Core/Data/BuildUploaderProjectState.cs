using System.IO;
using UnityEngine;

namespace Wireframe
{
    public class BuildUploaderProjectState
    {
        internal static readonly string FilePath = Application.dataPath + "/../BuildUploader/ProjectState.json";
        private const int CurrentVersion = 1;

        private static BuildUploaderProjectState _instance;
        public static BuildUploaderProjectState Instance
        {
            get
            {
                if (_instance == null)
                {
                    BuildUploaderProjectSettings.EnsureLoaded();
                    if (_instance == null)
                    {
                        _instance = Load(FilePath);
                    }
                }

                return _instance;
            }
        }

        public int Version;
        public int LastBuildNumber;
        public int TotalUploadTasksStarted;

        public BuildUploaderProjectState()
        {
            Version = CurrentVersion;
        }

        internal static void SetInstance(BuildUploaderProjectState state)
        {
            _instance = state;
        }

        internal static BuildUploaderProjectState Load(string filePath, bool saveIfMissing = true)
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                BuildUploaderProjectState savedData = JSON.DeserializeObject<BuildUploaderProjectState>(json);
                if (savedData != null)
                {
                    return savedData;
                }
            }

            var state = new BuildUploaderProjectState();
            if (saveIfMissing && !File.Exists(filePath))
            {
                Save(state, filePath);
            }

            return state;
        }

        internal static void Save(BuildUploaderProjectState state, string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JSON.SerializeObject(state);
            File.WriteAllText(filePath, json);
        }

        internal static void SaveIfLoaded()
        {
            if (_instance != null)
            {
                Save(_instance, FilePath);
            }
        }

        public static void Save()
        {
            SaveIfLoaded();
        }

        public static void BumpUploadNumber()
        {
            Instance.TotalUploadTasksStarted++;
            Save();
        }

        public static void BumpBuildNumber()
        {
            Instance.LastBuildNumber++;
            Save();
        }
    }
}
