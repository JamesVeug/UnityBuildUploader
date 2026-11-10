using System;
using System.IO;
using UnityEngine;

namespace Wireframe
{
    [Serializable]
    public class BuildMetaData
    {
        /// <summary>
        /// Packaged players (Android) can't take a StreamingAssets file after the build, so they get the meta data
        /// as a Resources text asset with this name
        /// </summary>
        public const string ResourceName = "BuildUploaderBuildData";

        public int BuildNumber;
        public int UploadNumber;

        public static BuildMetaData Get()
        {
#if UNITY_EDITOR
            BuildMetaData data = new BuildMetaData();
            data.BuildNumber = 123456789;
            data.UploadNumber = 123456789;
            return data;
#else
            
            // Get from build settings
            string streamingAssetsPath = Application.streamingAssetsPath;
            string path = Path.Combine(streamingAssetsPath, "BuildData") + ".json";
            if(File.Exists(path))
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<BuildMetaData>(json);
            }

            TextAsset resource = Resources.Load<TextAsset>(ResourceName);
            if (resource != null)
            {
                return JsonUtility.FromJson<BuildMetaData>(resource.text);
            }

            CloudBuildManifest cloudBuildManifest = CloudBuildManifest.Instance;
            if (cloudBuildManifest != null)
            {
                BuildMetaData data = new BuildMetaData();
                data.BuildNumber = cloudBuildManifest.BuildNumber;
                data.UploadNumber = cloudBuildManifest.BuildNumber;
                return data;
            }
            
            return new BuildMetaData(); // Not build using build uploader and or cloud build
#endif
        }
    }
}