using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wireframe {
    public class BuildUploaderWelcomeWindow : EditorWindow
    {
        private struct ServiceStatus
        {
            public AService Service;
            public bool ReadyToBuild;
            public GUIContent Reason;
            public bool ProjectSettingsSetup;
        }

        private GUIStyle headerLabelStyle;
        private GUIStyle versionLabelStyle;
        private GUIStyle sectionLabelStyle;
        private GUIStyle sectionFoldoutStyle;
        private GUIStyle changeFoldoutStyle;
        private GUIStyle headerStyle;
        private GUIStyle indentStyle;
        private GUIStyle indentedBoxStyle;
        private GUIStyle exampleStyle;
        private Vector2 scrollPosition;

        private List<ChangelogMarkdown.Entry> parsedChangeLog;
        private int newVersionCount;
        private string lastSeenVersion;

        private List<ServiceStatus> serviceStatuses;
        private DateTime serviceStatusesTime = DateTime.MinValue;

        [MenuItem("Tools/Build Uploader/Welcome")]
        public static void ShowWindow()
        {
            bool alreadyOpen = Resources.FindObjectsOfTypeAll<BuildUploaderWelcomeWindow>().Length > 0;

            BuildUploaderWelcomeWindow window = GetWindow<BuildUploaderWelcomeWindow>();
            window.titleContent = new GUIContent("Welcome to Build Uploader!", Utils.WindowIcon);
            window.minSize = new Vector2(600, 400);

            if (!alreadyOpen)
            {
                // Only place the window the first time it opens so we don't move/resize it out from under the user.
                Rect windowPosition = window.position;
                windowPosition.size = new Vector2(Mathf.Min(1080, Screen.currentResolution.width * 0.5f), Screen.currentResolution.height * 0.5f);
                windowPosition.center = new Rect(0f, 0f, Screen.currentResolution.width * 0.5f + windowPosition.size.x * 0.5f, Screen.currentResolution.height * 0.5f + windowPosition.size.y * 0.5f).center;
                window.position = windowPosition;
            }

            window.Show();
        }

        private void OnEnable()
        {
            // Remember the version the user last saw before anything opening this window overwrites it.
            lastSeenVersion = Preferences.LastSeenWelcomeVersion;
        }

        private void OnGUI()
        {
            Parse();
            DrawLinks();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label(Utils.WindowLargeIcon, headerLabelStyle);
            GUILayout.Label("Build Uploader", headerLabelStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(Utils.PackageVersion))
                {
                    GUILayout.Label($"v{Utils.PackageVersion}", versionLabelStyle);
                }
                BuildUploaderWindow.DrawUpdateButton(EditorStyles.toolbarButton);
            }

            GUILayout.Label("Welcome to the Build Uploader!");
            GUILayout.Label("This tool is designed to make it easy to make a build and upload it to all kinds of services.");

            EditorGUILayout.Space(20);

            DrawServiceStatus();

            EditorGUILayout.Space();

            DrawSetupCheckList();

            EditorGUILayout.Space(20);

            GUILayout.Label("Changelog", sectionLabelStyle);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawChanges();
            }

            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawFooter()
        {
            using (new EditorGUILayout.HorizontalScope("box"))
            {
                GUILayout.Label(new GUIContent("Show this window:", Preferences.WelcomeWindowPopupTooltip),
                    GUILayout.Width(115));

                int current = (int)Preferences.ShowWelcomeWindow;
                int newIndex = EditorGUILayout.Popup(current, Preferences.WelcomeWindowPopupOptions, GUILayout.Width(120));
                if (newIndex != current)
                {
                    Preferences.ShowWelcomeWindow = (Preferences.WelcomeWindowPopup)newIndex;

                    // The user is looking at this version right now so don't show it to them again.
                    Preferences.LastSeenWelcomeVersion = Utils.PackageVersion;
                }

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(Utils.PackageVersion))
                {
                    GUILayout.Label($"v{Utils.PackageVersion}");
                }

                if (GUILayout.Button("Close", GUILayout.Width(80)))
                {
                    Close();
                }
            }
        }

        private List<ServiceStatus> GetServiceStatuses()
        {
            // Each service may hit the disk to check if it's setup so don't do this every time the GUI is drawn.
            if (serviceStatuses != null && (DateTime.UtcNow - serviceStatusesTime).TotalSeconds < 1)
            {
                return serviceStatuses;
            }

            serviceStatuses = new List<ServiceStatus>();
            foreach (AService service in InternalUtils.AllServices())
            {
                bool readyToBuild = service.IsReadyToStartBuild(out GUIContent reason);
                serviceStatuses.Add(new ServiceStatus
                {
                    Service = service,
                    ReadyToBuild = readyToBuild,
                    Reason = reason,
                    ProjectSettingsSetup = readyToBuild && service.IsProjectSettingsSetup(),
                });
            }

            serviceStatusesTime = DateTime.UtcNow;
            return serviceStatuses;
        }

        private void DrawServiceStatus()
        {
            List<ServiceStatus> statuses = GetServiceStatuses();
            bool anyServiceSetup = statuses.Any(a => a.ReadyToBuild && a.ProjectSettingsSetup);

            bool show = EditorPrefs.GetBool("BuildUploader_showServices", !anyServiceSetup);
            bool newShow = EditorGUILayout.Foldout(show, new GUIContent("Services", SuccessIcon(anyServiceSetup, true)), sectionFoldoutStyle);
            if (newShow != show)
            {
                EditorPrefs.SetBool("BuildUploader_showServices", newShow);
            }

            if (!newShow)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                GUILayout.Label("Services you can upload to or notify once setup. You only need the ones you plan on using.");
                foreach (ServiceStatus status in statuses)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool setup = status.ReadyToBuild && status.ProjectSettingsSetup;
                        string tooltip;
                        if (setup)
                        {
                            tooltip = "Ready to use.";
                        }
                        else if (!status.ReadyToBuild)
                        {
                            tooltip = status.Reason != null ? status.Reason.text : "Not setup in Preferences.";
                        }
                        else
                        {
                            tooltip = "Needs setting up in Project Settings.";
                        }

                        GUILayout.Label(new GUIContent(status.Service.ServiceName, SuccessIcon(setup, false), tooltip), GUILayout.Width(200));

                        if (!status.ReadyToBuild)
                        {
                            // Services usually hand back a link to the exact page that fixes the reason they're not ready.
                            SettingsLinkGUIContent link = status.Reason as SettingsLinkGUIContent ??
                                                          status.Service.PreferencesLink(status.Service.ServiceName, "");
                            if (GUILayout.Button(link.ButtonText, GUILayout.Width(160)))
                            {
                                link.OpenSettings();
                            }
                        }
                        else if (!status.ProjectSettingsSetup && status.Service.HasProjectSettingsGUI)
                        {
                            SettingsLinkGUIContent link = status.Service.ProjectSettingsLink(status.Service.ServiceName, "");
                            if (GUILayout.Button(link.ButtonText, GUILayout.Width(160)))
                            {
                                link.OpenSettings();
                            }
                        }

                        GUILayout.FlexibleSpace();
                    }
                }
            }
        }

        private void DrawSetupCheckList()
        {
            List<ServiceStatus> statuses = GetServiceStatuses();
            bool oneServiceReadyToBuild = statuses.Any(a => a.ReadyToBuild);
            bool oneServiceProjectSettingsSetup = statuses.Any(a => a.ReadyToBuild && a.ProjectSettingsSetup);
            bool oneUploadProfileSetup = UploadProfilesExist();
            bool rootPathSetup = !string.IsNullOrEmpty(Preferences.RootPath);

            bool allComplete = rootPathSetup && oneServiceReadyToBuild && oneServiceProjectSettingsSetup && oneUploadProfileSetup;

            bool show = EditorPrefs.GetBool("BuildUploader_showHowToSetup", !allComplete);
            bool newShow = EditorGUILayout.Foldout(show, new GUIContent("Setup checklist", SuccessIcon(allComplete, true)), sectionFoldoutStyle);
            if (newShow != show)
            {
                EditorPrefs.SetBool("BuildUploader_showHowToSetup", newShow);
            }

            if (newShow)
            {
                GUILayout.Label("Need more help setting up the Build Uploader?");

                using (new EditorGUILayout.VerticalScope(indentedBoxStyle))
                {
                    GUILayout.Label("Setup Preferences (Edit->Preferences)", headerStyle);
                    GUILayout.Label("These are settings for your project and not shared with anyone.");

                    using (new EditorGUILayout.VerticalScope(indentStyle))
                    {
                        GUILayout.Label($"\nBuild Uploader -> General");
                        string rootPathText = "Set the Root Path all of your sources and destinations start from";
                        DrawCheckList(rootPathText,
                            $"C:/SomeFolder/Builds/{Context.VERSION_KEY}", rootPathSetup,
                            new SettingsLinkGUIContent(rootPathText, "", "Preferences/Build Uploader/General", SettingsScope.User));

                        string cacheText = "Change Cached Builds to a smaller path. eg: C:/CachedBuilds";
                        DrawCheckList(cacheText,
                            null, !Preferences.CacheFolderPath.Equals(Preferences.DefaultCacheFolder),
                            new SettingsLinkGUIContent(cacheText, "", "Preferences/Build Uploader/General", SettingsScope.User));

                        GUILayout.Label($"\nBuild Uploader -> Services");
                        string credentialsText = "Enable and enter credentials for all services you want to use";
                        DrawCheckList(credentialsText,
                            "Enable Steamworks, download and install SteamSDK and enter your username.",
                            oneServiceReadyToBuild,
                            new SettingsLinkGUIContent(credentialsText, "", "Preferences/Build Uploader/Services", SettingsScope.User));
                    }

                    GUILayout.Label("\nSetup Project Settings (Edit->Project Settings)", headerStyle);
                    GUILayout.Label("These are specific to your project and will be shared with anyone with access to your source code.");

                    using (new EditorGUILayout.VerticalScope(indentStyle))
                    {
                        GUILayout.Label($"\nBuild Uploader -> Services");
                        string projectSettingsText = "Enter settings for all Services you want to use";
                        DrawCheckList(projectSettingsText,
                            "For Steamworks add a new App for your game and any branches you want to use.",
                            oneServiceProjectSettingsSetup,
                            new SettingsLinkGUIContent(projectSettingsText, "", "Project/Build Uploader/Services", SettingsScope.Project));
                    }

                    GUILayout.Label("\nSetup Upload Profile (Tools -> Build Uploader -> Open Window)", headerStyle);
                    using (new EditorGUILayout.VerticalScope(indentStyle))
                    {
                        DrawCheckList(
                            "Create an Upload Config so you can make a build and upload it to a service of your choosing.",
                            "", oneUploadProfileSetup, "Open Build Uploader", BuildUploaderWindow.OpenWindow);
                    }
                }
            }
        }

        private bool UploadProfilesExist()
        {
            string path = WindowUploadTab.UploadProfilePath;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }
            
            string[] files = Directory.GetFiles(path, "*.json");
            return files.Length > 0;
        }

        private Texture2D SuccessIcon(bool success, bool big)
        {
            if (big)
            {
                return success ? Utils.CheckIcon : Utils.CrossIcon;
            }
            return success ? Utils.CheckIconSmall : Utils.CrossIconSmall;
        }
        
        private void DrawCheckList(string text, string example, bool isComplete, SettingsLinkGUIContent link)
        {
            DrawCheckList(text, example, isComplete, link.ButtonText, link.OpenSettings);
        }

        private void DrawCheckList(string text, string example, bool isComplete, string buttonText = null, Action buttonAction = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(text, SuccessIcon(isComplete, false)));
                if (buttonAction != null && !string.IsNullOrEmpty(buttonText))
                {
                    if (GUILayout.Button(buttonText, GUILayout.Width(160)))
                    {
                        buttonAction();
                    }
                }
            }

            if (!string.IsNullOrEmpty(example))
            {
                GUILayout.Label($"\tExample: {example}", exampleStyle);
            }
        }

        private void Parse()
        {
            if (parsedChangeLog != null)
            {
                return;
            }
            
            headerLabelStyle = new GUIStyle(GUI.skin.label);
            headerLabelStyle.wordWrap = true;
            headerLabelStyle.alignment = TextAnchor.MiddleCenter;
            headerLabelStyle.fontStyle = FontStyle.Bold;
            headerLabelStyle.fontSize = 24;

            versionLabelStyle = new GUIStyle(GUI.skin.label);
            versionLabelStyle.alignment = TextAnchor.MiddleCenter;
            versionLabelStyle.fontStyle = FontStyle.Italic;

            sectionLabelStyle = new GUIStyle(GUI.skin.label);
            sectionLabelStyle.wordWrap = true;
            sectionLabelStyle.alignment = TextAnchor.MiddleLeft;
            sectionLabelStyle.fontStyle = FontStyle.Normal;
            sectionLabelStyle.fontSize = 18;

            sectionFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            sectionFoldoutStyle.fontSize = 16;

            changeFoldoutStyle = new GUIStyle(EditorStyles.foldout);
            changeFoldoutStyle.fontSize = 18;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontStyle = FontStyle.Bold;

            exampleStyle = new GUIStyle(GUI.skin.label);
            exampleStyle.fontStyle = FontStyle.Italic;
            exampleStyle.wordWrap = true;
            exampleStyle.richText = true;
            exampleStyle.onNormal.textColor = Color.black;

            indentStyle = new GUIStyle(GUIStyle.none);
            indentStyle.margin.left = 10;

            // Copy the box style instead of using GUI.skin's own or the margin below leaks into every other window.
            indentedBoxStyle = new GUIStyle("box");
            indentedBoxStyle.margin.left = 10;


            string path = Path.Combine(Utils.s_packagePath, "CHANGELOG.md");
            Object loadAssetAtPath = AssetDatabase.LoadAssetAtPath(path, typeof(TextAsset));
            string allText = loadAssetAtPath is TextAsset textAsset ? textAsset.text : "";
            parsedChangeLog = ChangelogMarkdown.Parse(allText);

            OpenNewVersions();
        }

        /// <summary>
        /// Opens the changes made in versions the user has not seen yet so they don't have to go looking for them.
        /// </summary>
        private void OpenNewVersions()
        {
            newVersionCount = 0;
            if (parsedChangeLog.Count == 0)
            {
                return;
            }

            if (Version.TryParse(Utils.ToSemantic(lastSeenVersion ?? ""), out Version lastSeen))
            {
                foreach (ChangelogMarkdown.Entry data in parsedChangeLog)
                {
                    if (Version.TryParse(Utils.ToSemantic(data.Title), out Version version) && version > lastSeen)
                    {
                        data.FoldoutOpen = true;
                        newVersionCount++;
                    }
                }
            }
            else
            {
                // Never opened this window before (or an unexpected version) so only show the latest changes.
                parsedChangeLog[0].FoldoutOpen = true;
                newVersionCount = 1;
            }
        }

        private void DrawChanges()
        {
            for (int i = 0; i < parsedChangeLog.Count; i++)
            {
                ChangelogMarkdown.Entry data = parsedChangeLog[i];
                data.FoldoutOpen = EditorGUILayout.Foldout(data.FoldoutOpen, data.Title, true, changeFoldoutStyle);
                if (!data.FoldoutOpen)
                {
                    continue;
                }

                EditorGUI.indentLevel++;
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    ChangelogMarkdown.Draw(data.Lines);
                }
                EditorGUI.indentLevel--;
                
                if (i != 0)
                {
                    GUILayout.Space(10);
                    GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
                    GUILayout.Space(10);
                }
            }
        }

        private static void DrawLinks()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Documentation", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://github.com/JamesVeug/UnityBuildUploader/wiki");
                }
                
                if (GUILayout.Button(new GUIContent("Discord", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://discord.gg/R2UjXB6pQ8");
                }
                
                if (GUILayout.Button(new GUIContent("CLI/CI Docs", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://github.com/JamesVeug/UnityBuildUploader/wiki/CLI");
                }

                if (GUILayout.Button(new GUIContent("Github", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://github.com/JamesVeug/UnityBuildUploader");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Asset Store", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://assetstore.unity.com/packages/tools/utilities/build-uploader-306907");
                }
                
                if (GUILayout.Button(new GUIContent("Report Bug / Suggest Feature", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://github.com/JamesVeug/UnityBuildUploader/issues");
                }
                
                if (GUILayout.Button(new GUIContent("Support Me", Utils.LinkIcon)))
                {
                    Application.OpenURL("https://buymeacoffee.com/jamesgamesnz");
                }
            }
        }
    }
    
    [InitializeOnLoad]
    public class ScriptReloadWatcher
    {
        static ScriptReloadWatcher()
        {
            EditorApplication.delayCall += OnScriptsReloaded;
        }

        private const string LegacyShownKey = "BuildUploaderWelcomeWindow";
        private const string ShownThisSessionKey = "BuildUploader_WelcomeShownThisSession";

        private static void OnScriptsReloaded()
        {
            MigrateLegacyKey();

            string version = Utils.PackageVersion;
            switch (Preferences.ShowWelcomeWindow)
            {
                case Preferences.WelcomeWindowPopup.Never:
                    return;

                case Preferences.WelcomeWindowPopup.OnStartup:
                    // This is called every time scripts reload so only show it the first time the editor opens.
                    if (SessionState.GetBool(ShownThisSessionKey, false))
                    {
                        return;
                    }
                    break;

                case Preferences.WelcomeWindowPopup.WhenUpdated:
                    // No version means we failed to read package.json. Don't show it every reload because of that.
                    if (string.IsNullOrEmpty(version) || Preferences.LastSeenWelcomeVersion == version)
                    {
                        return;
                    }
                    break;
            }

            SessionState.SetBool(ShownThisSessionKey, true);
            BuildUploaderWelcomeWindow.ShowWindow();
            Preferences.LastSeenWelcomeVersion = version;
        }

        /// <summary>
        /// Older versions only had a bool for if this window has ever been shown.
        /// Treat those users as having seen the version they're on so updating doesn't open the window unexpectedly.
        /// </summary>
        private static void MigrateLegacyKey()
        {
            if (!ProjectEditorPrefs.HasKey(LegacyShownKey))
            {
                return;
            }

            if (ProjectEditorPrefs.GetBool(LegacyShownKey))
            {
                Preferences.LastSeenWelcomeVersion = Utils.PackageVersion;
            }

            ProjectEditorPrefs.DeleteKey(LegacyShownKey);
        }
    }
}