using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Inspector-style layout for when the window is too thin to show an Upload Config's sections side by side.
    /// Each Upload Config gets its own tab with its sections stacked as foldouts, and the profile's Actions get a tab
    /// after them. The tabs become a dropdown when they don't fit. Right-clicking a config's tab opens its menu, which
    /// also holds its Enabled toggle.
    /// </summary>
    internal partial class WindowUploadTab
    {
        // The window switches to the thin layout below ThinLayoutWidth. The margin stops it flicking between the
        // layouts while the window's edge is dragged across that width.
        internal const float ThinLayoutWidth = 900;
        internal const float ThinLayoutWidthMargin = 20;
        private const float ThinRunAllButtonHeight = 40;
        private const float ThinTabStripPadding = 12; // The window's box around this tab
        private const float ThinContentPadding = 50; // The boxes and scroll bar around a collapsed GUI
        private const float ThinSmallButtonWidth = 24;
        private const string ThinTabMenuTooltip = "\n\nRight-click for Enabled, Move, Duplicate, Delete and more";

        private bool m_usingThinLayout;
        private UploadConfig m_thinSelectedConfig;
        private bool m_thinActionsTabSelected;
        private int m_thinRequestedTab = -1;
        private Vector2 m_thinScrollPosition;
        private bool m_thinDescriptionFoldoutCollapsed = true;

        internal static bool ShouldUseThinLayout(float windowWidth, bool usingThinLayout)
        {
            float switchWidth = usingThinLayout
                ? ThinLayoutWidth + ThinLayoutWidthMargin
                : ThinLayoutWidth - ThinLayoutWidthMargin;
            return windowWidth < switchWidth;
        }

        /// <summary>
        /// A short name made from the first enabled source and destination, e.g. "1. Build Config → Steamworks".
        /// </summary>
        internal static string GetUploadConfigLabel(int index, UploadConfig uploadConfig)
        {
            string source = uploadConfig.Sources.FirstOrDefault(a => a.Enabled && a.SourceType != null)?.SourceType.DisplayName;
            string destination = uploadConfig.Destinations.FirstOrDefault(a => a.Enabled && a.DestinationType != null)?.DestinationType.DisplayName;

            string label;
            if (source != null && destination != null)
            {
                label = source + " → " + destination;
            }
            else
            {
                label = source ?? destination ?? "Upload Config";
            }

            return $"{index + 1}. {label}";
        }

        private void OnGUIThin()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawUploadProfilePopup(GUILayout.MinWidth(60));
                    DrawSaveButton(GUILayout.Width(50));

                    if (CustomSettingsIcon.OnGUI())
                    {
                        ShowUploadProfileMenu();
                    }

                    BuildUploaderWindow.DrawUpdateButton(GUI.skin.button, GUILayout.Height(20));
                }

                if (m_currentUploadProfile == null)
                {
                    return;
                }

                // A tab change waits for the next Layout event so every event in a frame draws the same tab
                if (Event.current.type == EventType.Layout)
                {
                    ApplyThinTabSelection();
                }

                DrawThinTabs();

                m_thinScrollPosition = EditorGUILayout.BeginScrollView(m_thinScrollPosition);
                if (m_thinActionsTabSelected)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        DrawActionsHeader("Actions", m_currentUploadProfile.Actions);
                        DrawThinActions("Actions", m_currentUploadProfile.Actions);
                    }
                }
                else if (m_thinSelectedConfig != null)
                {
                    m_thinSelectedConfig.OnGUIThin(UploaderWindow.position.width - ThinContentPadding, ref m_isDirty);
                }
                else
                {
                    EditorGUILayout.HelpBox("This Upload Profile has no Upload Configs. Press + to add one.", MessageType.Info);
                }

                if (m_isDirty && Preferences.AutoSaveUploadConfigsAfterChanges)
                {
                    Save();
                }

                EditorGUILayout.EndScrollView();

                GUILayout.Space(5);
                DrawDescriptionTextArea(ref m_thinDescriptionFoldoutCollapsed, true);
                DrawRunAllButton(ThinRunAllButtonHeight);
            }
        }

        private void ApplyThinTabSelection()
        {
            List<UploadConfig> uploadConfigs = m_currentUploadProfile.UploadConfigs;
            if (m_thinRequestedTab >= 0)
            {
                m_thinActionsTabSelected = m_thinRequestedTab >= uploadConfigs.Count;
                m_thinSelectedConfig = m_thinActionsTabSelected ? null : uploadConfigs[m_thinRequestedTab];
                m_thinRequestedTab = -1;
                m_thinScrollPosition = Vector2.zero;
            }

            // The config was deleted or another profile was loaded
            if (!m_thinActionsTabSelected && !uploadConfigs.Contains(m_thinSelectedConfig))
            {
                m_thinSelectedConfig = uploadConfigs.Count > 0 ? uploadConfigs[0] : null;
            }
        }

        private void SelectThinTab(int index)
        {
            m_thinRequestedTab = index;
            UploaderWindow.Repaint();
        }

        /// <summary>
        /// One tab per Upload Config then Actions, or a dropdown of them when they don't fit, followed by a + button.
        /// </summary>
        private void DrawThinTabs()
        {
            List<UploadConfig> uploadConfigs = m_currentUploadProfile.UploadConfigs;
            int selectedTab = m_thinActionsTabSelected ? uploadConfigs.Count : uploadConfigs.IndexOf(m_thinSelectedConfig);

            GUIContent[] tabs = new GUIContent[uploadConfigs.Count + 1];
            for (int i = 0; i < uploadConfigs.Count; i++)
            {
                tabs[i] = GetThinTabContent(i, uploadConfigs[i]);
            }
            tabs[uploadConfigs.Count] = new GUIContent("Actions", "Actions that run for the whole Upload Profile");

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Vector2 iconSize = EditorGUIUtility.GetIconSize();
                EditorGUIUtility.SetIconSize(new Vector2(12, 12));

                float tabsWidth = ThinSmallButtonWidth;
                foreach (GUIContent tab in tabs)
                {
                    tabsWidth += EditorStyles.toolbarButton.CalcSize(tab).x;
                }

                bool tabsFit = tabsWidth <= UploaderWindow.position.width - ThinTabStripPadding;
                if (tabsFit)
                {
                    for (int i = 0; i < tabs.Length; i++)
                    {
                        Color contentColor = GUI.contentColor;
                        if (i < uploadConfigs.Count && !uploadConfigs[i].Enabled)
                        {
                            GUI.contentColor = new Color(contentColor.r, contentColor.g, contentColor.b, 0.5f);
                        }

                        Rect tabRect = GUILayoutUtility.GetRect(tabs[i], EditorStyles.toolbarButton);
                        if (i < uploadConfigs.Count && EditorUtils.IsRightClicked(tabRect))
                        {
                            ShowThinUploadConfigMenu(uploadConfigs[i]);
                        }

                        bool selected = GUI.Toggle(tabRect, i == selectedTab, tabs[i], EditorStyles.toolbarButton);
                        GUI.contentColor = contentColor;
                        if (selected && i != selectedTab)
                        {
                            SelectThinTab(i);
                        }
                    }

                    GUILayout.FlexibleSpace();
                }
                EditorGUIUtility.SetIconSize(iconSize);

                if (!tabsFit)
                {
                    DrawThinTabDropdown(tabs, selectedTab, uploadConfigs);
                }

                if (GUILayout.Button(new GUIContent("+", "New Upload Config"), EditorStyles.toolbarButton, GUILayout.Width(ThinSmallButtonWidth)))
                {
                    AddNewUploadConfig();
                    SelectThinTab(uploadConfigs.Count - 1);
                }
            }
        }

        private void DrawThinTabDropdown(GUIContent[] tabs, int selectedTab, List<UploadConfig> uploadConfigs)
        {
            string[] names = new string[tabs.Length];
            for (int i = 0; i < tabs.Length; i++)
            {
                bool disabled = i < uploadConfigs.Count && !uploadConfigs[i].Enabled;
                names[i] = disabled ? tabs[i].text + " (Disabled)" : tabs[i].text;
            }

            if (GUILayout.Button(new GUIContent("<", "Previous tab"), EditorStyles.toolbarButton, GUILayout.Width(ThinSmallButtonWidth)))
            {
                SelectThinTab((selectedTab - 1 + tabs.Length) % tabs.Length);
            }

            Rect popupRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.toolbarPopup, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
            if (selectedTab >= 0 && selectedTab < uploadConfigs.Count && EditorUtils.IsRightClicked(popupRect))
            {
                ShowThinUploadConfigMenu(uploadConfigs[selectedTab]);
            }

            int newSelectedTab = EditorGUI.Popup(popupRect, selectedTab, names, EditorStyles.toolbarPopup);
            if (newSelectedTab != selectedTab)
            {
                SelectThinTab(newSelectedTab);
            }

            if (GUILayout.Button(new GUIContent(">", "Next tab"), EditorStyles.toolbarButton, GUILayout.Width(ThinSmallButtonWidth)))
            {
                SelectThinTab((selectedTab + 1) % tabs.Length);
            }
        }

        /// <summary>
        /// Shows an error icon on the tab when the config can't be uploaded, since only one config is visible at a time.
        /// </summary>
        private static GUIContent GetThinTabContent(int index, UploadConfig uploadConfig)
        {
            string label = GetUploadConfigLabel(index, uploadConfig);
            if (!uploadConfig.Enabled)
            {
                return new GUIContent(label, label + "\nDisabled: Run All skips this Upload Config" + ThinTabMenuTooltip);
            }

            if (!uploadConfig.CanStartBuild(out string reason))
            {
                return new GUIContent(label, Utils.ErrorIcon, reason + ThinTabMenuTooltip);
            }

            return new GUIContent(label, label + ThinTabMenuTooltip);
        }

        /// <summary>
        /// The Upload Config's menu, with its Enabled toggle first, shown when its tab is right-clicked.
        /// </summary>
        private void ShowThinUploadConfigMenu(UploadConfig uploadConfig)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Enabled"), uploadConfig.Enabled, () =>
            {
                uploadConfig.Enabled = !uploadConfig.Enabled;
                m_isDirty = true;
            });
            menu.AddSeparator("");
            AddUploadConfigMenuItems(menu, uploadConfig);
            menu.ShowAsContext();
        }

        /// <summary>
        /// The profile's actions with each one's settings on several lines instead of DrawActions' single row.
        /// </summary>
        private void DrawThinActions(string title, List<UploadConfig.UploadActionData> actions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                UploadConfig.UploadActionData actionData = actions[i];
                bool disabled = actionData.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never;
                using (new GUILayout.VerticalScope("box"))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        if (CustomSettingsIcon.OnGUI())
                        {
                            ShowActionMenu(title, actions, actionData);
                        }

                        if (CustomFoldoutButton.OnGUI(actionData.Collapsed))
                        {
                            actionData.Collapsed = !actionData.Collapsed;
                        }

                        using (new EditorGUI.DisabledScope(disabled))
                        {
                            DrawActionTypePopup(actionData, GUILayout.MinWidth(80));
                        }
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        var status = (UploadConfig.UploadActionData.UploadCompleteStatus)EditorGUILayout.EnumPopup(actionData.WhenToExecute, GUILayout.Width(100));
                        if (status != actionData.WhenToExecute)
                        {
                            actionData.WhenToExecute = status;
                            m_isDirty = true;
                        }

                        EditorUtils.DrawEnumPopup(actionData.Triggers, "Choose Triggers",
                            (newTriggers) =>
                            {
                                actionData.Triggers = newTriggers;
                                m_isDirty = true;
                            }, GUILayout.MinWidth(80), GUILayout.ExpandWidth(true));
                    }

                    m_isDirty |= UploadConfig.DrawStopTaskIfFailedToggle(actionData);

                    if (actionData.ActionType != null && actionData.UploadAction != null)
                    {
                        using (new EditorGUI.DisabledScope(disabled))
                        {
                            if (actionData.Collapsed)
                            {
                                using (new GUILayout.HorizontalScope())
                                {
                                    float maxWidth = UploaderWindow.position.width - ThinContentPadding;
                                    actionData.UploadAction.OnGUICollapsed(ref m_isDirty, maxWidth);
                                }
                            }
                            else
                            {
                                using (new EditorGUILayout.VerticalScope())
                                {
                                    actionData.UploadAction.OnGUIExpanded(ref m_isDirty);
                                }
                            }
                        }
                    }
                }

                DrawActionLogs(actionData);
            }
        }
    }
}
