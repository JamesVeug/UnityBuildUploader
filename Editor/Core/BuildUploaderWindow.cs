using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal class BuildUploaderWindow : EditorWindow, IHasCustomMenu
    {
        private const float SAVE_TIME_DELAY = 60 * 5; // 5 minutes 
        private const float EDIT_TIME_BEFORE_SAVE = 10; // 10 seconds 

        public WindowTab CurrentTab => currentTab;

        private WindowTab currentTab;
        private List<WindowTab> m_tabs;

        private float m_nextSaveDelta;
        private float m_lastEditDelta;
        private bool m_saveQueued;

        [MenuItem("Window/Build Uploader/Open Window", false, 0)]
        public static void OpenWindow()
        {
            // Get existing open window or if none, make a new one:
            BuildUploaderWindow uploaderWindow = GetWindow<BuildUploaderWindow>();
            uploaderWindow.titleContent = new GUIContent("Build Uploader", Utils.WindowIcon);
            uploaderWindow.InitializeTabs();
            uploaderWindow.Show();
        }

        private void OnEnable()
        {
            UpdateChecker.StateChanged += Repaint;
        }

        private void OnDisable()
        {
            UpdateChecker.StateChanged -= Repaint;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Check for Updates"), false, BuildUploaderUpdateWindow.CheckForUpdates);
        }

        private void Update()
        {
            InitializeTabs();
            
            foreach (WindowTab tab in m_tabs)
            {
                if (tab.Enabled)
                {
                    tab.Update();
                }
            }

            // Save
            if (m_saveQueued)
            {
                m_nextSaveDelta += Time.deltaTime;
                m_lastEditDelta += Time.deltaTime;

                bool autoSave = m_nextSaveDelta >= SAVE_TIME_DELAY;
                bool haveNotEditedRecently = m_lastEditDelta >= EDIT_TIME_BEFORE_SAVE;
                if (autoSave && haveNotEditedRecently)
                {
                    bool isUpdatingAssets = EditorApplication.isUpdating;
                    bool isCompiling = EditorApplication.isCompiling;
                    bool canAutoSave = !isUpdatingAssets && !isCompiling;
                    if (canAutoSave)
                    {
                        ImmediateSave();
                    }
                }
            }
        }

        private void InitializeTabs()
        {
            if (m_tabs == null)
            {
                m_tabs = new List<WindowTab>();
                foreach (AService service in InternalUtils.AllServices())
                {
                    WindowTab type = service.WindowTabType;
                    if (type != null)
                    {
                        m_tabs.Add(type);
                    }
                }
                
                m_tabs.Add(new WindowUploadTab());

                foreach (WindowTab tab in m_tabs)
                {
                    tab.Initialize(this);
                }
                
                currentTab = m_tabs[0];
            }
        }

        private void OnGUI()
        {
            // Pump format-string autocomplete dropdowns: input at the very start, overlay at the
            // very end (outside all layout scopes) so it isn't clipped by any scroll view.
            FormatStringFieldDropdowns.BeginHost(this);

            InitializeTabs();

            if (!currentTab.Enabled)
            {
                currentTab = m_tabs.FirstOrDefault(a => a.Enabled);
            }

            // Tabs
            if (m_tabs.Count(a=>a.Enabled) > 1)
            {
                using (new GUILayout.HorizontalScope())
                {
                    DrawTabs();

                    // The Upload tab draws the update button next to its settings icon. Other tabs have no
                    // header for it, so it goes at the end of the tabs.
                    if (!(CurrentTab is WindowUploadTab))
                    {
                        GUILayout.FlexibleSpace();
                        DrawUpdateButton(EditorStyles.toolbarButton);
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope("box"))
            {
                CurrentTab.OnGUI();
            }

            FormatStringFieldDropdowns.EndHost(this);
        }

        private void DrawTabs()
        {
            using (new GUILayout.HorizontalScope())
            {
                foreach (WindowTab tab in m_tabs)
                {
                    if (tab.Enabled)
                    {
                        bool isActive = CurrentTab == tab;
                        GUIStyle style = new GUIStyle(EditorStyles.toolbarButton)
                        {
                            fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                            alignment = TextAnchor.MiddleCenter,
                            padding = new RectOffset(10, 10, 4, 4)
                        };
                        if (GUILayout.Toggle(isActive, tab.TabName, style))
                        {
                            currentTab = tab;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A small button that opens the update window, drawn only when a new version is out.
        /// </summary>
        internal static void DrawUpdateButton(GUIStyle style, params GUILayoutOption[] options)
        {
            GUIContent content;
            ReleaseInfo update = UpdateChecker.AvailableUpdate;
            CommitUpdate commits = UpdateChecker.AvailableCommits;
            if (commits != null)
            {
                int count = commits.NewCommits.Count;
                string text = count == 1 ? "1 commit" : $"{count} commits";
                content = new GUIContent(text, Utils.DownloadIcon,
                    $"{text} on {commits.BranchLabel} on GitHub that you don't have. Click to see them.");
            }
            else if (update != null)
            {
                PackageInstallSource source = UpdateChecker.InstallSource;
                string tooltip = source.FollowsBranch
                    ? $"Build Uploader v{update.Version} is out. You follow the {source.GitRevision} branch. Click to see what changed and update."
                    : $"Build Uploader v{update.Version} is available. Click to see what changed and update.";
                content = new GUIContent("v" + update.Version, Utils.DownloadIcon, tooltip);
            }
            else
            {
                return;
            }

            Vector2 iconSize = EditorGUIUtility.GetIconSize();
            EditorGUIUtility.SetIconSize(new Vector2(16, 16));
            List<GUILayoutOption> layout = new List<GUILayoutOption>(options) { GUILayout.ExpandWidth(false) };
            Rect rect = GUILayoutUtility.GetRect(content, style, layout.ToArray());
            bool clicked = GUI.Button(rect, content, style);
            EditorGUIUtility.SetIconSize(iconSize);

            if (clicked)
            {
                Vector2 screenPosition = GUIUtility.GUIToScreenPoint(rect.position);
                BuildUploaderUpdateWindow.ShowDropDown(new Rect(screenPosition, rect.size));
            }
        }

        void OnDestroy()
        {
            ImmediateSave();
        }

        private void ImmediateSave()
        {
            if (m_tabs == null)
            {
                // Docked and never initialized
                return;
            }
            
            foreach (WindowTab tab in m_tabs)
            {
                if (tab.Enabled)
                {
                    tab.Save();
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            m_nextSaveDelta = 0;
            m_saveQueued = false;
            Debug.Log("Build Uploader Window Saved");
        }

        public void QueueSave()
        {
            m_nextSaveDelta = 0;
            m_saveQueued = true;
        }

        public T SetTab<T>() where T : WindowTab
        {
            foreach (WindowTab tab in m_tabs)
            {
                if (tab is T t)
                {
                    currentTab = tab;
                    return t;
                }
            }
            
            Debug.LogWarning($"Tab of type {typeof(T).Name} not found.");
            return null;
        }
    }
}