using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace Wireframe
{
    internal partial class WindowUploadTab : WindowTab
    {
        internal static readonly string UploadProfilePath =  Application.dataPath + "/../BuildUploader/UploadProfiles";
        internal static string UploadReportSaveDirectory => Path.Combine(Preferences.CacheFolderPath, "Upload Task Reports");
        private static readonly GUIContent DryRunContent = new GUIContent("Dry Run",
            "Run All makes local changes but nothing will leave this PC." +
            "\nSources and modifiers run as normal so builds are still made. Destinations and actions with their own dry run " +
            "(eg: Steam's Preview Upload) only check the upload with their service, local ones (eg: Local Path) run as normal " +
            "and the rest are skipped.");

        public override string TabName => "Upload";
        
        private Context m_context;
        private List<UploadProfileMeta> m_unloadedUploadProfiles;
        private UploadProfile m_currentUploadProfile;
        private UploadProfileMeta m_currentUploadProfileData;
        
        private GUIStyle m_titleStyle;
        private GUIStyle m_subTitleStyle;
        private string m_buildDescription;
        private bool m_showFormattedDescription = Preferences.DefaultShowFormattedTextToggle;
        private bool m_isDirty;
        private bool m_dryRun;
        private Vector2 m_descriptionScrollPosition;

        public override void Initialize(BuildUploaderWindow uploaderWindow)
        {
            base.Initialize(uploaderWindow);
            
            m_context = new UploadTaskStringFormatterContext(this);
            m_context.AddCommand(Context.TASK_PROFILE_NAME_KEY, () => m_context.FormatString(m_currentUploadProfile?.ProfileName) ?? "No Profile Selected");
            m_context.AddCommand(Context.TASK_DESCRIPTION_KEY, () => m_context.FormatString(m_buildDescription));
            
            m_buildDescription = Preferences.DefaultDescriptionFormat;
        }

        private void Setup()
        {
            m_titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };
            
            m_subTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold
            };

            if (m_unloadedUploadProfiles == null)
                Load();
        }

        public override void OnGUI()
        {
            Setup();

            // Only decided on Layout events so every event in a frame draws the same layout
            if (Event.current.type == EventType.Layout)
            {
                m_usingThinLayout = ShouldUseThinLayout(UploaderWindow.position.width, m_usingThinLayout);
            }

            if (m_usingThinLayout)
            {
                OnGUIThin();
            }
            else
            {
                OnGUIExpanded();
            }
        }

        private void DrawUploadProfilePopup(params GUILayoutOption[] options)
        {
            List<string> profileNames = new List<string>();
            profileNames.Add("-- Select Upload Profile --");

            profileNames.AddRange(m_unloadedUploadProfiles.Select(p => m_context.FormatString(p.ProfileName)));
            for (int i = 1; i < profileNames.Count; i++)
            {
                profileNames[i] = $"{i}. {profileNames[i]}";
            }

            int selectedIndex = 0;
            if (m_currentUploadProfile != null)
            {
                selectedIndex = m_unloadedUploadProfiles.FindIndex(a => a.GUID == m_currentUploadProfile.GUID);
                if (selectedIndex != -1)
                {
                    selectedIndex++;
                }
            }

            var newSelectedIndex = EditorGUILayout.Popup(selectedIndex, profileNames.ToArray(), options);
            if (newSelectedIndex < 0)
            {
                newSelectedIndex = selectedIndex;
            }

            if (newSelectedIndex != selectedIndex && newSelectedIndex > 0)
            {
                if (m_isDirty)
                {
                    if (EditorUtility.DisplayDialog("Unsaved Changes",
                            "You have unsaved changes. Do you want to save them before switching profiles?",
                            "Yes", "No"))
                    {
                        Save();
                    }
                }
                LoadMetaDataFromPath(m_unloadedUploadProfiles[newSelectedIndex - 1]);
            }
        }

        private void DrawSaveButton(params GUILayoutOption[] options)
        {
            DrawSaveButton(GUI.skin.button, options);
        }

        private void DrawSaveButton(GUIStyle style, params GUILayoutOption[] options)
        {
            if (!Preferences.AutoSaveUploadConfigsAfterChanges)
            {
                string text = m_isDirty ? "*Save" : "Save";
                if (GUILayout.Button(text, style, options))
                {
                    Save();
                }
            }
        }

        private void ShowUploadProfileMenu()
        {
            string dropdownText = m_isDirty ? "*" : "";
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(dropdownText + "Save"), false, Save);
            menu.AddItem(new GUIContent("Copy GUID"), false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = m_currentUploadProfile.GUID;
            });

            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                TextInputPopup.ShowWindow(newName =>
                {
                    if (!string.IsNullOrEmpty(newName))
                    {
                        m_currentUploadProfile.ProfileName = newName;
                        m_currentUploadProfileData.ProfileName = newName;
                        m_isDirty = true;
                        // Save or update as needed
                    }
                });
            });

            menu.AddMenuItem("Duplicate Upload Profile", false, () =>
            {
                UploadProfileSavedData data = UploadProfileSavedData.FromUploadProfile(m_currentUploadProfile);
                string json = JSON.SerializeObject(data);
                UploadProfileSavedData deserializedData = UploadProfileSavedData.FromJSON(json);
                UploadProfile duplicateProfile = deserializedData.ToUploadProfile();
                duplicateProfile.GUID = Guid.NewGuid().ToString().Substring(0, 6);
                duplicateProfile.ProfileName += " (Copy)";
                m_currentUploadProfileData = CreateMetaData(duplicateProfile);
                m_unloadedUploadProfiles.Add(m_currentUploadProfileData);
                m_currentUploadProfile = duplicateProfile;
                foreach (UploadConfig config in m_currentUploadProfile.UploadConfigs)
                {
                    config.Context.SetParent(m_context);
                }
                m_isDirty = true;
            }, m_currentUploadProfile == null);

            menu.AddMenuItem("Delete", false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete Upload Profile",
                        "Are you sure you want to delete this upload profile?", "Yes", "No"))
                {
                    int index = m_unloadedUploadProfiles.FindIndex(a =>
                        a.GUID == m_currentUploadProfile.GUID);
                    UploadProfileMeta meta = m_unloadedUploadProfiles[index];
                    if (!string.IsNullOrEmpty(meta.FilePath))
                    {
                        File.Delete(meta.FilePath);
                    }

                    m_unloadedUploadProfiles.RemoveAt(index);
                    int newIndex = Mathf.Clamp(index, 0, m_unloadedUploadProfiles.Count - 1);
                    LoadMetaDataFromPath(m_unloadedUploadProfiles[newIndex]);
                    m_isDirty = true;
                }
            }, m_unloadedUploadProfiles.Count <= 1);

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("-- Create New Upload Profile --"), false, () =>
            {
                TextInputPopup.ShowWindow((profileName) =>
                {
                    if (!string.IsNullOrEmpty(profileName))
                    {
                        CreateDefaultUploadConfig(profileName);
                        Save();
                    }
                });
            });

            menu.ShowAsContext();
        }

        private void ShowUploadConfigMenu(UploadConfig uploadConfig)
        {
            GenericMenu menu = new GenericMenu();
            AddUploadConfigMenuItems(menu, uploadConfig);
            menu.ShowAsContext();
        }

        private void AddUploadConfigMenuItems(GenericMenu menu, UploadConfig uploadConfig)
        {
            menu.AddItem(new GUIContent("Move To Top"), false, () =>
            {
                int indexOf = m_currentUploadProfile.UploadConfigs.IndexOf(uploadConfig);
                if (indexOf > 0)
                {
                    m_currentUploadProfile.UploadConfigs.RemoveAt(indexOf);
                    m_currentUploadProfile.UploadConfigs.Insert(0, uploadConfig);

                    m_isDirty = true;
                }
            });

            menu.AddItem(new GUIContent("Move Up"), false, () =>
            {
                int indexOf = m_currentUploadProfile.UploadConfigs.IndexOf(uploadConfig);
                if (indexOf > 0)
                {
                    m_currentUploadProfile.UploadConfigs.RemoveAt(indexOf);
                    m_currentUploadProfile.UploadConfigs.Insert(indexOf - 1, uploadConfig);
                    m_isDirty = true;
                }
            });

            menu.AddItem(new GUIContent("Move Down"), false, () =>
            {
                int indexOf = m_currentUploadProfile.UploadConfigs.IndexOf(uploadConfig);
                if (indexOf < m_currentUploadProfile.UploadConfigs.Count - 1)
                {
                    m_currentUploadProfile.UploadConfigs.RemoveAt(indexOf);
                    m_currentUploadProfile.UploadConfigs.Insert(indexOf + 1, uploadConfig);
                    m_isDirty = true;
                }
            });

            menu.AddItem(new GUIContent("Move To Bottom"), false, () =>
            {
                int indexOf = m_currentUploadProfile.UploadConfigs.IndexOf(uploadConfig);
                if (indexOf < m_currentUploadProfile.UploadConfigs.Count - 1)
                {
                    m_currentUploadProfile.UploadConfigs.RemoveAt(indexOf);
                    m_currentUploadProfile.UploadConfigs.Add(uploadConfig);

                    m_isDirty = true;
                }
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Duplicate"), false, () =>
            {
                // Clone it using JSON because just using serialize data uses concrete types for collections
                // TODO: Fix the concrete types to avoid this JSON serialization
                Dictionary<string, object> serialized = uploadConfig.Serialize();
                string json = JSON.SerializeObject(serialized);
                serialized = JSON.DeserializeObject<Dictionary<string, object>>(json);

                UploadConfig copy = new UploadConfig();
                copy.Deserialize(serialized);
                copy.NewGUID();
                copy.Context.SetParent(m_context);
                m_currentUploadProfile.UploadConfigs.Add(copy);

                m_isDirty = true;
            });

            menu.AddSeparator("");

            uploadConfig.AddSectionMenuItems(menu, UploadConfig.Section.Sources, "Sources/");
            uploadConfig.AddSectionMenuItems(menu, UploadConfig.Section.Modifiers, "Modifiers/");
            uploadConfig.AddSectionMenuItems(menu, UploadConfig.Section.Destinations, "Destinations/");
            uploadConfig.AddSectionMenuItems(menu, UploadConfig.Section.Actions, "Actions/");

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Reset All Settings"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Reset all Upload Config",
                        "Are you sure you want to reset all settings in this Upload Config?", "Reset", "Cancel"))
                {
                    uploadConfig.Clear();
                    m_isDirty = true;
                }
            });

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Delete Upload Config",
                        "Are you sure you want to remove this Upload Config?", "Delete", "Cancel"))
                {
                    m_currentUploadProfile.UploadConfigs.Remove(uploadConfig);
                    m_isDirty = true;
                }
            });
        }

        private void DrawRunAllButton(float height)
        {
            bool canUpload = CanStartUpload(out string reason);
            using (new EditorGUILayout.HorizontalScope())
            {
                // Run all
                using (new EditorGUILayout.VerticalScope())
                {
                    if (!canUpload)
                    {
                        EditorGUILayout.HelpBox(reason, MessageType.Error);
                    }

                    string runAllText = m_dryRun ? "Dry Run All" : "Run All";
                    Rect buttonRect = GUILayoutUtility.GetRect(new GUIContent(runAllText), GUI.skin.button, GUILayout.Height(height));
                    Rect dryRunRect = GetDryRunToggleRect(buttonRect);

                    // Takes the click before the button so pressing the toggle doesn't also run all
                    HandleDryRunToggleClick(dryRunRect);

                    // Also disabled while the description autocomplete dropdown is open so the
                    // button (which sits beneath the dropdown) doesn't show hover / press state.
                    using (new EditorGUI.DisabledScope(!canUpload || FormatStringFieldDropdowns.IsDropdownOpen))
                    {
                        if (GUI.Button(buttonRect, runAllText))
                        {
                            string dryRunText = m_dryRun ? "\n\nDry run: local changes are made but nothing will leave this PC." : "";
                            if (EditorUtility.DisplayDialog(
                                runAllText,
                                "Are you sure you want to run all enabled builds using their configured destinations?" +
                                dryRunText +
                                "\n\nNOTE: You cannot cancel this operation once started!",
                                "Yes", "Cancel"))
                            {
                                try
                                {
                                    DownloadAndUpload();
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogException(ex);
                                }
                            }
                        }
                    }

                    DrawDryRunToggle(dryRunRect);
                }
            }
        }

        /// <summary>
        /// The Dry Run toggle sits in the Run All button's bottom-right corner.
        /// </summary>
        private static Rect GetDryRunToggleRect(Rect buttonRect)
        {
            Vector2 size = EditorStyles.toggle.CalcSize(DryRunContent);
            float inset = Mathf.Min(6, Mathf.Max(0, (buttonRect.height - size.y) / 2));
            return new Rect(buttonRect.xMax - size.x - 8, buttonRect.yMax - size.y - inset, size.x, size.y);
        }

        private void HandleDryRunToggleClick(Rect rect)
        {
            Event current = Event.current;
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                m_dryRun = !m_dryRun;
                current.Use();
                UploaderWindow.Repaint();
            }
        }

        /// <summary>
        /// Drawn after the button so it shows on top of it. HandleDryRunToggleClick handles its input.
        /// </summary>
        private void DrawDryRunToggle(Rect rect)
        {
            Event current = Event.current;
            if (current.type == EventType.Repaint)
            {
                EditorStyles.toggle.Draw(rect, DryRunContent, rect.Contains(current.mousePosition), false, m_dryRun, false);
            }

            GUI.Label(rect, new GUIContent("", DryRunContent.tooltip), GUIStyle.none);
        }

        /// <param name="fieldOnOwnRow">Puts the folded description's text field under its label instead of beside it</param>
        private void DrawDescriptionTextArea(ref bool foldoutCollapsed, bool fieldOnOwnRow)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (CustomSettingsIcon.OnGUI())
                {
                    ShowEditDescriptionMenu();
                }
                    
                if (CustomFoldoutButton.OnGUI(foldoutCollapsed))
                {
                    foldoutCollapsed = !foldoutCollapsed;
                }
                    
                    
                GUIContent label = new GUIContent("Description", "A description of what's been uploaded." +
                                                                 "\nDescription is included in some destinations such as Steamworks so keep it short." +
                                                                 "\nGood practice is to include the version number and a short summary of the changes since the last build." +
                                                                 "\nexample: v1.2.9 - Hotfix for missing player texture and balance changes.");
                GUILayout.Label(label, GUILayout.MaxWidth(100));
                    
                GUIContent content = new GUIContent("F", EditorUtils.GetFormatStringTextFieldTooltip(m_context));
                m_showFormattedDescription = GUILayout.Toggle(m_showFormattedDescription, content, "ToolbarButton", GUILayout.Width(20), GUILayout.Height(20));
                if (foldoutCollapsed && !fieldOnOwnRow)
                {
                    DrawDescriptionLine();
                }
                else
                {
                    GUILayout.FlexibleSpace();
                }
            }

            if (foldoutCollapsed && fieldOnOwnRow)
            {
                DrawDescriptionLine();
            }

            if(!foldoutCollapsed){
                m_descriptionScrollPosition = GUILayout.BeginScrollView(m_descriptionScrollPosition, GUILayout.Height(100));
                if (m_showFormattedDescription)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        string formattedDescription = m_context.FormatString(m_buildDescription);
                        GUILayout.TextArea(formattedDescription, GUILayout.ExpandHeight(true));
                    }
                }
                else
                {
                    m_buildDescription = FormatStringFieldDropdowns.Draw(GUIUtility.GetControlID(FocusType.Keyboard),
                        m_buildDescription, m_context, false, null, GUILayout.ExpandHeight(true));
                }
                GUILayout.EndScrollView();
            }
        }

        /// <summary>
        /// The description on one line, shown while its foldout is collapsed.
        /// </summary>
        private void DrawDescriptionLine()
        {
            if (m_showFormattedDescription)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    string formattedDescription = m_context.FormatString(m_buildDescription);
                    GUILayout.TextArea(formattedDescription, GUILayout.ExpandWidth(true));
                }
            }
            else
            {
                m_buildDescription = FormatStringFieldDropdowns.Draw(GUIUtility.GetControlID(FocusType.Keyboard),
                    m_buildDescription, m_context, false, null, GUILayout.ExpandWidth(true));
            }
        }

        private void DrawActionsHeader(string title, List<UploadConfig.UploadActionData> actions)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, m_subTitleStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("New " + title))
                {
                    // Show a popup to select an action
                    UploadConfig.UploadActionData actionData = new UploadConfig.UploadActionData();
                    actionData.SetupDefaults();
                    actions.Add(actionData);
                }
            }
        }

        private void ShowActionMenu(string title, List<UploadConfig.UploadActionData> actions, UploadConfig.UploadActionData actionData)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("MoveUp"), false, () =>
            {
                int indexOf = actions.IndexOf(actionData);
                if (indexOf > 0)
                {
                    actions.RemoveAt(indexOf);
                    actions.Insert(indexOf - 1, actionData);
                    m_isDirty = true;
                }
            });
            menu.AddItem(new GUIContent("MoveDown"), false, () =>
            {
                int indexOf = actions.IndexOf(actionData);
                if (indexOf < actions.Count - 1)
                {
                    actions.RemoveAt(indexOf);
                    actions.Insert(indexOf + 1, actionData);
                    m_isDirty = true;
                }
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Remove " + title,
                        "Are you sure you want to remove this " + title + "?", "Delete", "Cancel"))
                {
                    actions.Remove(actionData);
                    m_isDirty = true;
                }
            });
            menu.ShowAsContext();
        }

        private void DrawActionTypePopup(UploadConfig.UploadActionData actionData, params GUILayoutOption[] options)
        {
            if (UIHelpers.ActionsPopup.DrawPopup(ref actionData.ActionType, m_context, options))
            {
                m_isDirty = true;
                Utils.CreateInstance(actionData.ActionType?.Type, out actionData.UploadAction);
                if (actionData.UploadAction != null)
                {
                    actionData.UploadAction.Context.SetParent(m_context);
                    actionData.StopTaskIfFailed = actionData.UploadAction.DefaultStopTaskIfFailed;
                }
            }
        }

        private static void DrawActionLogs(UploadConfig.UploadActionData actionData)
        {
            if (actionData.UploadAction != null && actionData.WhenToExecute != UploadConfig.UploadActionData.UploadCompleteStatus.Never)
            {
                using (new GUILayout.VerticalScope())
                {
                    List<GUIContent> errors = new List<GUIContent>();
                    actionData.UploadAction.TryGetErrors(errors);
                    foreach (GUIContent error in errors)
                    {
                        UploadConfig.DrawError(error);
                    }

                    List<GUIContent> warnings = new List<GUIContent>();
                    actionData.UploadAction.TryGetWarnings(warnings);
                    foreach (GUIContent warning in warnings)
                    {
                        UploadConfig.DrawWarning(warning);
                    }
                }
            }
        }

        private void AddNewUploadConfig()
        {
            UploadConfig newConfig = new UploadConfig();
            newConfig.Context.SetParent(m_context);
            newConfig.SetupDefaults();
            m_currentUploadProfile.UploadConfigs.Add(newConfig);
            m_isDirty = true;
        }

        private void ShowEditDescriptionMenu()
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Clear Description"), false, () => m_buildDescription = "");
            menu.AddItem(new GUIContent("Reset (" + Preferences.DefaultDescriptionFormat + ")"), false, () => m_buildDescription = Preferences.DefaultDescriptionFormat);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Set/Text file"), false, () =>
            {
                // Choose file
                string path = EditorUtility.OpenFilePanel("Choose File", "", "");
                if (string.IsNullOrEmpty(path))
                    return;
                
                string text = File.ReadAllText(path);
                m_buildDescription = text;
            });
            menu.AddItem(new GUIContent("Append/Text file"), false, () =>
            {
                // Choose file
                string path = EditorUtility.OpenFilePanel("Choose File", "", "");
                if (string.IsNullOrEmpty(path))
                    return;
                
                string text = File.ReadAllText(path);
                m_buildDescription += "\n\n" + text;
            });
            menu.ShowAsContext();
        }

        private async Task DownloadAndUpload()
        {
            if (m_isDirty)
            {
                if (EditorUtility.DisplayDialog("Un-saved changes",
                        "Are you sure you want to start uploading?\nThe unsaved changes will not be applied to this upload.", "Save Now", "Cancel"))
                {
                    Save();
                }
                else
                {
                    Debug.Log("[BuildUploader] Upload Task cancelled at user request. Unsaved changes.");
                    return;
                }
            }
            
            // Setup Task
            UploadProfile uploadProfile = UploadProfile.FromPath(m_currentUploadProfileData.FilePath);
            uploadProfile.ProfileName = m_context.FormatString(uploadProfile.ProfileName);
            
            string description = m_context.FormatString(m_buildDescription);
            UploadTask uploadTask;
            try
            {
                uploadTask = new UploadTask(uploadProfile);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildUploader] Failed to create upload task: {ex.Message}");
                return;
            }

            uploadTask.SetBuildDescription(description);
            uploadTask.SetDryRun(m_dryRun);
            uploadTask.StartedFromWindow = true;
            ShowResultWhenComplete(uploadTask);
            UploadTask.AllTasks.Add(uploadTask);
            
            // Start task
            Debug.Log("[BuildUploader] Upload Task started.... Grab a coffee... this could take a while.");
            if (Preferences.AutoFocusNewUploadTask)
            {
                BuildUploaderTaskWindow.FocusTask(uploadTask);
            }

            await Task.Yield(); // Yield to allow the UI to update before starting the task
            await Task.Yield(); // Yield to allow the UI to update before starting the task
            await Task.Yield(); // Yield to allow the UI to update before starting the task
            Task asyncBuildTask = uploadTask.StartAsync(true);
            
            // Wait for task to complete
            while (!asyncBuildTask.IsCompleted)
            {
                // Wait for the task to complete
                await Task.Yield();
                UploaderWindow.Repaint();
            }
        }

        /// <summary>
        /// Tells the user how the upload went. Attached through OnComplete, because a script reload ends this window's
        /// wait for the task and the resumed task attaches it again.
        /// </summary>
        internal static void ShowResultWhenComplete(UploadTask uploadTask)
        {
            // Shown on the next update so the dialog doesn't hold up the task finishing
            uploadTask.OnComplete += report => EditorApplication.delayCall += () => ShowResult(report);
        }

        private static void ShowResult(UploadTaskReport report)
        {
            // The report is written to disk by UploadTask itself, so every entry point records a run.
            string taskReport = report.GetReport();

            // Report back to the user
            if (report.Successful)
            {
                Debug.Log($"[BuildUploader] Upload Task successful!");
                Debug.Log($"[BuildUploader] {taskReport}");
                if (Preferences.ShowConfirmationWindowAfterUpload == Preferences.ShowIf.Always || 
                    Preferences.ShowConfirmationWindowAfterUpload == Preferences.ShowIf.Successful)
                {
                    EditorUtility.DisplayDialog("Build Uploader", "All builds uploaded successfully!", "Yay!");
                }
            }
            else
            {
                Debug.LogError($"[BuildUploader] Build Task Failed! See logs for more info");
                Debug.Log($"[BuildUploader] {taskReport}");

                if (Preferences.ShowConfirmationWindowAfterUpload == Preferences.ShowIf.Always || 
                    Preferences.ShowConfirmationWindowAfterUpload == Preferences.ShowIf.Failed)
                {
                    // Get the first 3 failed lines from the report
                    StringBuilder sb = new StringBuilder();

                    int logs = 0;
                    foreach (var (stepType, log) in report.GetFailReasons())
                    {
                        sb.AppendLine($"{stepType}: {log}");
                        logs++;
                        if (logs >= 3)
                        {
                            break;
                        }
                    }

                    sb.Append("\n\nSee logs for more info.");

                    EditorUtility.DisplayDialog("Build Uploader", sb.ToString(), "Okay");
                }
            }
        }

        private bool CanStartUpload(out string reason)
        {
            if (m_currentUploadProfile.UploadConfigs == null)
            {
                reason = "No builds to upload!";
                return false;
            }

            int validConfigs = 0;
            for (int i = 0; i < m_currentUploadProfile.UploadConfigs.Count; i++)
            {
                if (!m_currentUploadProfile.UploadConfigs[i].Enabled)
                    continue;

                if (!m_currentUploadProfile.UploadConfigs[i].CanStartBuild(out string buildReason))
                {
                    reason = $"Build {i+1}: {buildReason}";
                    return false;
                }

                validConfigs++;
            }

            // Make sure there is at least 1 build to build
            if (validConfigs == 0)
            {
                reason = "Need at least 1 Upload Config";
                return false;
            }
            
            for (int i = 0; i < m_currentUploadProfile.Actions.Count; i++)
            {
                if (!m_currentUploadProfile.Actions[i].CanStartBuild(out string buildReason, m_context))
                {
                    reason = $"Action {i+1}: {buildReason}";
                    return false;
                }
            }
            
            if (string.IsNullOrEmpty(m_buildDescription))
            {
                reason = "No Description";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public override void Save()
        {
            m_isDirty = false;
            if (m_currentUploadProfile == null)
            {
                return;
            }

            UploadProfileSavedData data = UploadProfileSavedData.FromUploadProfile(m_currentUploadProfile);
            
            string filePath = m_currentUploadProfileData.FilePath;
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JSON.SerializeObject(data);
            if (!File.Exists(filePath))
            {
                var stream = File.Create(filePath);
                stream.Close();
            }

            File.WriteAllText(filePath, json);
        }

        private void Load()
        {
            m_unloadedUploadProfiles = new List<UploadProfileMeta>();
            m_currentUploadProfile = null;

            if (!Directory.Exists(UploadReportSaveDirectory))
            {
                MoveOldUploadReportsToNewFolder();
            }
            
            if (!Directory.Exists(UploadProfilePath))
            {
                Directory.CreateDirectory(UploadProfilePath);
                
                // In v3.0.0 we moved the using multiple upload profiles and put them in the Projects/BuildUploader folder
                if (File.Exists(Application.persistentDataPath + "/BuildUploader/WindowUploadTab.json"))
                {
                    LoadOldUploadTabDataFromPath(Application.persistentDataPath + "/BuildUploader/WindowUploadTab.json");
                    
                    // TODO: Delete the old file
                    Save();
                    return;
                }
                else if (File.Exists(Application.persistentDataPath + "/SteamBuilder/WindowUploadTab.json"))
                {
                    // In v2.0.0 we renamed from UnitySteamBuildBuilder to BuildUploader
                    Debug.Log("SteamBuildData exists from a previous version. Migrating it over");
                    LoadOldUploadTabDataFromPath(Application.persistentDataPath + "/SteamBuilder/WindowUploadTab.json");
                    
                    Save();
                    return;
                }
            }
            else
            {
                List<UploadProfileMeta> metas = UploadProfileMeta.LoadFromProjectSettings();
                if (metas.Count > 0)
                {
                    m_unloadedUploadProfiles.Clear();
                    m_unloadedUploadProfiles.AddRange(metas);

                    string previouslySelectedGUID = ProjectEditorPrefs.GetString("BuildUploader.LastSelectedUploadProfileGUID", string.Empty);
                    if (string.IsNullOrEmpty(previouslySelectedGUID))
                    {
                        LoadMetaDataFromPath(m_unloadedUploadProfiles[0]);
                    }
                    else if (m_unloadedUploadProfiles.Any(x => x.GUID == previouslySelectedGUID))
                    {
                        // Load the previously selected profile
                        UploadProfileMeta metaData = m_unloadedUploadProfiles.First(x => x.GUID == previouslySelectedGUID);
                        LoadMetaDataFromPath(metaData);
                    }
                    else
                    {
                        // Load the first profile
                        LoadMetaDataFromPath(m_unloadedUploadProfiles[0]);
                    }
                    return;
                }
            }
            
            Debug.Log("No Upload profiles exist. Creating new file");
            CreateDefaultUploadConfig();
            Save();
        }

        private void MoveOldUploadReportsToNewFolder()
        {
            if (!Directory.Exists(UploadReportSaveDirectory))
            {
                Directory.CreateDirectory(UploadReportSaveDirectory);
            }
            
            string oldReportPath = Path.Combine(Preferences.CacheFolderPath);
            string[] reportFiles = Directory.GetFiles(oldReportPath, "UploadReport_*.txt", SearchOption.TopDirectoryOnly);
            foreach (string file in reportFiles)
            {
                // Move to new directory
                string fileName = Path.GetFileName(file);
                string newFilePath = Path.Combine(UploadReportSaveDirectory, fileName);
                if (!File.Exists(newFilePath))
                {
                    try{
                        File.Move(file, newFilePath);
                        Debug.Log($"Moved old upload report from {file} to {newFilePath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Failed to move old upload report from {file} to {newFilePath}");
                        Debug.LogException(e);
                    }
                }
                else
                {
                    Debug.LogWarning($"Upload report file already exists in new directory: {newFilePath}. Skipping move.");
                }
            }
        }

        private void CreateDefaultUploadConfig(string profileName = "Default")
        {
            UploadProfile defaultProfile = new UploadProfile();
            defaultProfile.GUID = Guid.NewGuid().ToString().Substring(0, 6);
            defaultProfile.ProfileName = profileName;
            m_currentUploadProfile = defaultProfile;
            
            UploadProfileMeta meta = CreateMetaData(defaultProfile);
            m_unloadedUploadProfiles.Add(meta);
            m_currentUploadProfileData = meta;
        }

        private static UploadProfileMeta CreateMetaData(UploadProfile defaultProfile)
        {
            UploadProfileMeta defaultMetaData = new UploadProfileMeta();
            defaultMetaData.GUID = defaultProfile.GUID;
            defaultMetaData.ProfileName = defaultProfile.ProfileName;
            defaultMetaData.FilePath = Path.Combine(UploadProfilePath, $"{defaultMetaData.GUID}.json");
            return defaultMetaData;
        }

        private void LoadMetaDataFromPath(UploadProfileMeta metaData)
        {
            string json = File.ReadAllText(metaData.FilePath);
            UploadProfileSavedData savedData = UploadProfileSavedData.FromJSON(json);
            if (string.IsNullOrEmpty(savedData.GUID))
            {
                savedData.GUID = metaData.GUID;
            }
            
            UploadProfile loadedProfile = savedData.ToUploadProfile();
            m_currentUploadProfile = loadedProfile;
            m_currentUploadProfileData = metaData;
            
            foreach (UploadConfig uploadConfig in loadedProfile.UploadConfigs)
            {
                uploadConfig.Context.SetParent(m_context);
            }
            
            foreach (UploadConfig.UploadActionData action in loadedProfile.Actions)
            {
                if (action.UploadAction != null)
                {
                    action.UploadAction.Context.SetParent(m_context);
                }
            }
            
            ProjectEditorPrefs.SetString("BuildUploader.LastSelectedUploadProfileGUID", metaData.GUID);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private void LoadOldUploadTabDataFromPath(string filePath)
        {
            string json = File.ReadAllText(filePath);
            UploadTabData config = JSON.DeserializeObject<UploadTabData>(json);
            if (config == null)
            {
                Debug.Log("Could not deserialize old JSON. Creating new default Profile");
                CreateDefaultUploadConfig();
                Save();
            }
            else
            {
                CreateDefaultUploadConfig();
                UploadProfile defaultProfile = m_currentUploadProfile;
                for (int i = 0; i < config.Data.Count; i++)
                {
                    try
                    {
                        UploadConfig uploadConfig = new UploadConfig();
                        uploadConfig.Context.SetParent(m_context);
                        var jObject = config.Data[i];
                        uploadConfig.Deserialize(jObject);
                        defaultProfile.UploadConfigs.Add(uploadConfig);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Failed to load build config: #" + (i+1));
                        Debug.LogException(e);
                        UploadConfig uploadConfig = new UploadConfig();
                        uploadConfig.Context.SetParent(m_context);
                        defaultProfile.UploadConfigs.Add(uploadConfig);
                    }
                }
                
                for (int i = 0; i < config.PostUploads.Count; i++)
                {
                    try
                    {
                        UploadConfig.UploadActionData actionData = new UploadConfig.UploadActionData();
                        actionData.Deserialize(config.PostUploads[i]);
                        defaultProfile.Actions.Add(actionData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Failed to load post upload action: #" + (i+1));
                        Debug.LogException(e);
                    }
                }
            }
        }

#pragma warning restore CS0618 // Type or member is obsolete
    }
}