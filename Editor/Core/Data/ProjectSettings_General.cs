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
                "Build", "Uploader", "Build Uploader", "BuildUploader", "Pipe", "line", "pipe line", "Pipeline", "meta", "data", "Android", "Keystore"
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
                    " Packaged players such as Android get it as a Resources text asset instead, added for the build and removed after." +
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

            GUILayout.Label("", EditorStyles.boldLabel);

            GUIContent androidSigningLabel = new GUIContent("Android Signing", "Unity forgets the custom keystore passwords when it closes, so Android builds signed with your own " +
                                                                 "keystore fail until they are entered again. These are filled in when Player Settings has none. " +
                                                                 "They are saved and obfuscated on this computer, not in the project.");
            GUILayout.Label(androidSigningLabel, EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Keystore Password", "Password of the keystore set in Player Settings > Publishing Settings.");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                string keystorePassword = AndroidSigning.KeystorePassword;
                string newKeystorePassword = EditorGUILayout.PasswordField(keystorePassword);
                if (newKeystorePassword != keystorePassword)
                {
                    AndroidSigning.KeystorePassword = newKeystorePassword;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Key Password", "Password of the key alias set in Player Settings > Publishing Settings.");
                GUILayout.Label(label, GUILayout.MaxWidth(150));
                string keyAliasPassword = AndroidSigning.KeyAliasPassword;
                string newKeyAliasPassword = EditorGUILayout.PasswordField(keyAliasPassword);
                if (newKeyAliasPassword != keyAliasPassword)
                {
                    AndroidSigning.KeyAliasPassword = newKeyAliasPassword;
                }
            }
        }
    }
}