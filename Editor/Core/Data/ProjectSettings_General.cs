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

            GUILayout.Label("Settings for the Build Uploader that exists per project and shared with all users with access to your Unity Project via version control.", EditorStyles.wordWrappedLabel);
            GUILayout.Label("To prevent them being shared by version control add `BuildUploader` to your .gitignore", EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(20);
            
            BuildUploaderProjectSettings settings = BuildUploaderProjectSettings.Instance;

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
                GUIContent label = new GUIContent("Skip Number Updates",
                    "When on, starting builds and uploads does not increase Last Build Number or Upload Tasks Started, " +
                    "so BuildUploader/ProjectSettings.json is not rewritten and stays clean in version control." +
                    "\n\n{buildNumber} and {uploadNumber} then stay the same for every build and upload until you change them below.");
                GUILayout.Label(label, GUILayout.MaxWidth(150));

                bool newSkip = GUILayout.Toggle(settings.SkipBuildAndUploadNumberUpdates, "");
                if (newSkip != settings.SkipBuildAndUploadNumberUpdates)
                {
                    settings.SkipBuildAndUploadNumberUpdates = newSkip;
                    BuildUploaderProjectSettings.Save();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Last Build Number","Number of the last build created using the build uploader. This is used for {buildNumber} when formatting text fields in the build uploader. Access it in builds using BuildMetaData.Get().UploadNumber");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                
                int newBuildNumber = EditorGUILayout.IntField(settings.LastBuildNumber);
                if (newBuildNumber != settings.LastBuildNumber)
                {
                    settings.LastBuildNumber = newBuildNumber;
                    BuildUploaderProjectSettings.Save();
                }
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Upload Tasks Started", 
                    "How many upload tasks that have been started. This is used for {uploadNumber} when formatting text fields in the build uploader. Access it in builds using BuildMetaData.Get().UploadNumber");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                
                int totalStartedUploadTasks = EditorGUILayout.IntField(settings.TotalUploadTasksStarted);
                if (totalStartedUploadTasks != settings.TotalUploadTasksStarted)
                {
                    settings.TotalUploadTasksStarted = totalStartedUploadTasks;
                    BuildUploaderProjectSettings.Save();
                }
            }
        }
    }
}