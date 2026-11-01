using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// The Upload tab's expanded layout, for windows wide enough to list every Upload Config with its sections side
    /// by side. WindowUploadTab.Thin.cs has the layout for thin windows.
    /// </summary>
    internal partial class WindowUploadTab
    {
        private Vector2 m_scrollPosition;
        private bool m_descriptionFoldoutCollapsed;

        private void OnGUIExpanded()
        {
            using (new GUILayout.VerticalScope())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Upload Configs", m_titleStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("New Upload Config"))
                    {
                        AddNewUploadConfig();
                    }
                    
                    DrawUploadProfilePopup(GUILayout.Width(150));
                    DrawSaveButton(GUILayout.Width(100));

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

                // Builds to upload
                m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
                for (int i = 0; i < m_currentUploadProfile.UploadConfigs.Count; i++)
                {
                    UploadConfig uploadConfig = m_currentUploadProfile.UploadConfigs[i];
                    using (new GUILayout.HorizontalScope("box"))
                    {
                        if (CustomSettingsIcon.OnGUI())
                        {
                            ShowUploadConfigMenu(uploadConfig);
                        }
                        
                        if (CustomFoldoutButton.OnGUI(uploadConfig.Collapsed))
                        {
                            uploadConfig.Collapsed = !uploadConfig.Collapsed;
                        }

                        bool e = EditorGUILayout.Toggle(uploadConfig.Enabled, GUILayout.Width(20));
                        if (e != uploadConfig.Enabled)
                        {
                            uploadConfig.Enabled = e;
                            m_isDirty = true;
                        }

                        using (new GUILayout.VerticalScope())
                        {
                            uploadConfig.OnGUI(UploaderWindow.position.width, ref m_isDirty);
                        }
                    }
                }
                
                GUILayout.FlexibleSpace();
                
                // Actions
                DrawActions("Actions", m_currentUploadProfile.Actions);
                

                if (m_isDirty && Preferences.AutoSaveUploadConfigsAfterChanges)
                {
                    Save();
                }

                EditorGUILayout.EndScrollView();
                
                GUILayout.Space(20);

                // Description
                DrawDescriptionTextArea(ref m_descriptionFoldoutCollapsed, false);

                // Draw Upload button
                DrawRunAllButton(100);
            }
        }

        private void DrawActions(string title, List<UploadConfig.UploadActionData> actions)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                DrawActionsHeader(title, actions);

                for (int i = 0; i < actions.Count; i++)
                {
                    UploadConfig.UploadActionData actionData = actions[i];
                    using (new GUILayout.HorizontalScope("box"))
                    {
                        if (CustomSettingsIcon.OnGUI())
                        {
                            ShowActionMenu(title, actions, actionData);
                        }
                            
                        if (CustomFoldoutButton.OnGUI(actionData.Collapsed))
                        {
                            actionData.Collapsed = !actionData.Collapsed;
                        }

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
                            }, GUILayout.Width(200));

                        m_isDirty |= UploadConfig.DrawStopTaskIfFailedToggle(actionData);

                        bool disabled = actionData.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never;
                        using (new EditorGUI.DisabledScope(disabled))
                        {
                            DrawActionTypePopup(actionData, GUILayout.Width(200));

                            using (new GUILayout.VerticalScope())
                            {
                                if (actionData.ActionType != null && actionData.UploadAction != null)
                                {
                                    float maxWidth = UploaderWindow.position.width - (130 + 200 + 200 + 100 + UploadConfig.StopTaskIfFailedToggleWidth); // Gross
                                    if (actionData.Collapsed)
                                    {
                                        using (new GUILayout.HorizontalScope())
                                        {
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
                    }

                    DrawActionLogs(actionData);
                }
            }
        }
    }
}