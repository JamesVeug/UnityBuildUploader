using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Services tab for Project settings
    /// </summary>
    public partial class ProjectSettings_Settings : SettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            HashSet<string> keywords = new HashSet<string>(new[]
            {
                "Build", "Uploader", "Build Uploader", "BuildUploader", "Pipe", "line", "pipe line", "Pipeline", "meta", "data"
            });
            
            var provider =
                new ProjectSettings_Settings("Project/Build Uploader/General", SettingsScope.Project)
                {
                    label = "General",
                    keywords = keywords
                };
    
            return provider;
        }
    
        private ProjectSettings_Settings(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }
    
        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);

            GUILayout.Label("Build Uploader settings are shared with all users who access your Unity project through version control.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("Build and upload counters are stored separately in BuildUploader/ProjectState.json. Ignore it for workspace-local counters, or track it to share counters with intentional counter changes.", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(20);
            
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;
            BuildUploaderProjectState state = BuildUploaderProjectState.Instance;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Auto Generated Files Path", "Assets folder path for any Auto generated files. Example: Quick Upload Menu Items");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                if (CustomFolderPathTextField.OnGUI("Auto Generated Files Path", ref settings.AutoGenerateMenuItemPath, Application.dataPath))
                {
                    BuildUploaderProjectSettings.Save();
                }
            }

            GUILayout.Label("", EditorStyles.boldLabel);
            
            GUILayout.Label("Build Meta Data", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Include Streaming Folder", 
                    "Include a BuildData.json file in your builds StreamingAssets folder so your can reference the build number and other variables in your game." +
                    "\n\nSee BuildMetaData.Get()");
                GUILayout.Label(label, GUILayout.MaxWidth(150));

                bool newInclude = GUILayout.Toggle(settings.IncludeBuildMetaDataInStreamingDataFolder, "");
                if (newInclude != settings.IncludeBuildMetaDataInStreamingDataFolder)
                {
                    settings.IncludeBuildMetaDataInStreamingDataFolder = newInclude;
                    BuildUploaderProjectSettings.Save();
                }
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Last Build Number", "Number of the last build created using the build uploader. This is used for {buildNumber} when formatting text fields in the build uploader. Access it in builds using BuildMetaData.Get().UploadNumber");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                
                int newBuildNumber = EditorGUILayout.IntField(state.LastBuildNumber);
                if (newBuildNumber != state.LastBuildNumber)
                {
                    state.LastBuildNumber = newBuildNumber;
                    BuildUploaderProjectState.Save();
                }
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Upload Tasks Started", 
                    "How many upload tasks that have been started. This is used for {uploadNumber} when formatting text fields in the build uploader. Access it in builds using BuildMetaData.Get().UploadNumber");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                
                int totalStartedUploadTasks = EditorGUILayout.IntField(state.TotalUploadTasksStarted);
                if (totalStartedUploadTasks != state.TotalUploadTasksStarted)
                {
                    state.TotalUploadTasksStarted = totalStartedUploadTasks;
                    BuildUploaderProjectState.Save();
                }
            }
        }
    }
}
