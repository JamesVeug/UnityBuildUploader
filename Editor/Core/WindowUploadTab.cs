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
        internal static readonly string UploadReportSaveDirectory = Path.Combine(Preferences.CacheFolderPath, "Upload Task Reports");

        public override string TabName => "Upload";
        
        private Context m_context;
        private List<UploadProfileMeta> m_unloadedUploadProfiles;
        private UploadProfile m_currentUploadProfile;
        private UploadProfileMeta m_currentUploadProfileData;
        
        private GUIStyle m_titleStyle;
        private GUIStyle m_subTitleStyle;
        private Vector2 m_scrollPosition;
        private string m_buildDescription;
        private bool m_showFormattedDescription = Preferences.DefaultShowFormattedTextToggle;
        private bool m_isDirty;
        private Vector2 m_descriptionScrollPosition;
        private bool m_descriptionFoldoutCollapsed;

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
                    
                    // Dropdown to select upload profile
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

                    var newSelectedIndex = EditorGUILayout.Popup(selectedIndex, profileNames.ToArray(), GUILayout.Width(150));
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

                    if (!Preferences.AutoSaveUploadConfigsAfterChanges)
                    {
                        string text = m_isDirty ? "*Save" : "Save";
                        if (GUILayout.Button(text, GUILayout.Width(100)))
                        {
                            Save();
                        }
                    }

                    string dropdownText = m_isDirty ? "*" : "";
                    if (CustomSettingsIcon.OnGUI())
                    {
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
                            GenericMenu menu = new GenericMenu();
                            
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
                                if (EditorUtility.DisplayDialog("Duplicate Upload Config",
                                        "Are you sure you want to duplicate this Upload Config?", "Duplicate", "Cancel"))
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
                                }
                            });
                            
                            menu.AddSeparator("");
                            
                            menu.AddItem(new GUIContent("Add Source"), false, () =>
                            {
                                uploadConfig.AddSource(new UploadConfig.SourceData()
                                {
                                    Enabled = true,
                                });
                                m_isDirty = true;
                            });

                            menu.AddItem(new GUIContent("Add Default Modifiers"), false, () =>
                            {
                                uploadConfig.AddDefaultModifiers();
                                m_isDirty = true;
                            });
                            
                            menu.AddItem(new GUIContent("Add Destination"), false, () =>
                            {
                                uploadConfig.AddDestination(new UploadConfig.DestinationData()
                                {
                                    Enabled = true,
                                });
                                m_isDirty = true;
                            });
                            
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
                            menu.ShowAsContext();
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
                DrawDescriptionTextArea();

                // Draw Upload button
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

                        // Also disabled while the description autocomplete dropdown is open so the
                        // button (which sits beneath the dropdown) doesn't show hover / press state.
                        using (new EditorGUI.DisabledScope(!canUpload || FormatStringFieldDropdowns.IsDropdownOpen))
                        {
                            if (GUILayout.Button("Run All", GUILayout.Height(100)))
                            {
                                if (EditorUtility.DisplayDialog(
                                    "Run All",
                                    "Are you sure you want to run all enabled builds using their configured destinations?" +
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
                    }
                }
            }
        }

        private void DrawDescriptionTextArea()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (CustomSettingsIcon.OnGUI())
                {
                    ShowEditDescriptionMenu();
                }
                    
                if (CustomFoldoutButton.OnGUI(m_descriptionFoldoutCollapsed))
                {
                    m_descriptionFoldoutCollapsed = !m_descriptionFoldoutCollapsed;
                }
                    
                    
                GUIContent label = new GUIContent("Description", "A description of what's been uploaded." +
                                                                 "\nDescription is included in some destinations such as Steamworks so keep it short." +
                                                                 "\nGood practice is to include the version number and a short summary of the changes since the last build." +
                                                                 "\nexample: v1.2.9 - Hotfix for missing player texture and balance changes.");
                GUILayout.Label(label, GUILayout.MaxWidth(100));
                    
                GUIContent content = new GUIContent("F", EditorUtils.GetFormatStringTextFieldTooltip(m_context));
                m_showFormattedDescription = GUILayout.Toggle(m_showFormattedDescription, content, "ToolbarButton", GUILayout.Width(20), GUILayout.Height(20));
                if (m_descriptionFoldoutCollapsed)
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
                else
                {
                    GUILayout.FlexibleSpace();
                }
            }

            if(!m_descriptionFoldoutCollapsed){
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

        private void DrawActions(string title, List<UploadConfig.UploadActionData> actions)
        {
            using (new EditorGUILayout.VerticalScope("box"))
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

                for (int i = 0; i < actions.Count; i++)
                {
                    UploadConfig.UploadActionData actionData = actions[i];
                    using (new GUILayout.HorizontalScope("box"))
                    {
                        if (CustomSettingsIcon.OnGUI())
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

                        bool disabled = actionData.WhenToExecute == UploadConfig.UploadActionData.UploadCompleteStatus.Never;
                        using (new EditorGUI.DisabledScope(disabled))
                        {
                            if (UIHelpers.ActionsPopup.DrawPopup(ref actionData.ActionType, m_context, GUILayout.Width(200)))
                            {
                                m_isDirty = true;
                                Utils.CreateInstance(actionData.ActionType?.Type, out actionData.UploadAction);
                                if (actionData.UploadAction != null)
                                {
                                    actionData.UploadAction.Context.SetParent(m_context);
                                }
                            }

                            using (new GUILayout.VerticalScope())
                            {
                                if (actionData.ActionType != null && actionData.UploadAction != null)
                                {
                                    float maxWidth = UploaderWindow.position.width - (130 + 200 + 200 + 100); // Gross
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

            // The report is written to disk by UploadTask itself, so every entry point records a run.
            UploadTaskReport report = uploadTask.Report;
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