using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Partial class for GUI content
    /// TODO: Move to UIToolkit
    /// </summary>
    public partial class UploadConfig
    {
        internal const float StopTaskIfFailedToggleWidth = 150;
        
        internal bool Collapsed { get; set; } = true;
        
        private GUIStyle m_titleStyle;
        private bool m_deferredIsDirty;

        public void SetupDefaults()
        {
            AddSource(new SourceData()
            {
                Enabled = true,
                Source = null,
                SourceType = null
            });
            
            AddDestination(new DestinationData()
            {
                Enabled = true,
                Destination = null,
                DestinationType = null
            });
            
            AddDefaultModifiers();
        }

        public void AddDefaultModifiers()
        {
            // All Unity builds include a X_BurstDebugInformation_DoNotShip folder
            // This isn't needed so add it as a default modifier
            ExcludeFoldersModifier regexBuildModifier = new ExcludeFoldersModifier(AExcludePathsByRegex_UploadModifier.WhenToExclude.DeleteFromCache);
            regexBuildModifier.Add(".*DoNotShip", true, false);
            regexBuildModifier.Add(".*ButDontShipItWithYourGame", true, false);
            AddModifier(new ModifierData(regexBuildModifier, true));
        }

        private void SetupGUI()
        {
            if (m_titleStyle == null)
            {
                m_titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
        }

        internal void OnGUI(float windowWidth, ref bool isDirty)
        {
            SetupGUI();

            using (new EditorGUI.DisabledScope(!Enabled))
            {
                if (Collapsed)
                {
                    OnGUICollapsed(windowWidth, ref isDirty);
                }
                else
                {
                    OnGUIExpanded(windowWidth, ref isDirty);
                }
            }
        }

        private void OnGUICollapsed(float windowWidth, ref bool isDirty)
        {
            float splitWidth = 100;
            float maxWidth = windowWidth - splitWidth - 120;
            float parts = maxWidth / 2 - splitWidth;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (SourceData source in m_buildSources)
                    {
                        if (!source.Enabled)
                        {
                            continue;
                        }
                        
                        // Draw the build but on one line
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Source Type
                            if (UIHelpers.SourcesPopup.DrawPopup(ref source.SourceType, m_context, GUILayout.MaxWidth(120)))
                            {
                                isDirty = true;
                                Utils.CreateInstance(source.SourceType?.Type, out source.Source);
                                if (source.Source != null)
                                {
                                    source.Source.Context.SetParent(m_context);
                                }
                            }

                            // Source
                            float sourceWidth = parts;
                            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(sourceWidth)))
                            {
                                if (source.Source != null)
                                {
                                    source.Source.OnGUICollapsed(ref isDirty, sourceWidth);
                                }
                            }
                        }
                    }

                    if (Enabled)
                    {
                        List<GUIContent> sourceErrors = GetSourceErrors();
                        if (sourceErrors.Count > 0)
                        {
                            foreach (GUIContent error in sourceErrors)
                            {
                                DrawError(error);
                            }
                        }

                        List<GUIContent> sourceWarnings = GetSourceWarnings();
                        if (sourceWarnings.Count > 0)
                        {
                            foreach (GUIContent warning in sourceWarnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }

                // Progress / Modifiers
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        string progressText = "->";
                        GUILayout.Label(progressText, m_titleStyle, GUILayout.Width(splitWidth));
                    }

                    if (Enabled)
                    {
                        List<GUIContent> modifierErrors = GetModifierErrors();
                        if (modifierErrors.Count > 0)
                        {
                            foreach (GUIContent error in modifierErrors)
                            {
                                DrawError(error);
                            }
                        }

                        List<GUIContent> modifierWarnings = GetModifierWarnings();
                        if (modifierWarnings.Count > 0)
                        {
                            foreach (GUIContent warning in modifierWarnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    foreach (DestinationData destinationData in m_buildDestinations)
                    {
                        if (!destinationData.Enabled)
                        {
                            continue;
                        }
                        
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            // Destination Type
                            if (UIHelpers.DestinationsPopup.DrawPopup(ref destinationData.DestinationType, m_context))
                            {
                                isDirty = true;
                                Utils.CreateInstance(destinationData.DestinationType?.Type, out destinationData.Destination);
                                if (destinationData.Destination != null)
                                {
                                    destinationData.Destination.Context.SetParent(m_context);
                                }
                            }

                            // Destination
                            float destinationWidth = parts;
                            using (new EditorGUILayout.HorizontalScope(GUILayout.MaxWidth(destinationWidth)))
                            {
                                if (destinationData.Destination != null)
                                {
                                    destinationData.Destination.OnPreGUI(ref isDirty, m_context);
                                    destinationData.Destination.OnGUICollapsed(ref isDirty, parts);
                                }
                            }
                        }

                        if (destinationData.Destination != null)
                        {
                            List<GUIContent> logs = new List<GUIContent>();
                            destinationData.Destination.TryGetErrors(logs);
                            if (logs.Count > 0)
                            {
                                foreach (GUIContent error in logs)
                                {
                                    DrawError(error);
                                }
                            }
                            logs.Clear();
                            destinationData.Destination.TryGetWarnings(logs, m_context);
                            if (logs.Count > 0)
                            {
                                foreach (GUIContent warning in logs)
                                {
                                    DrawWarning(warning);
                                }
                            }
                        }
                    }
                }

                if (Enabled && m_postActions.Count > 0)
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        List<GUIContent> modifierErrors = GetPostActionErrors();
                        if (modifierErrors.Count > 0)
                        {
                            foreach (GUIContent error in modifierErrors)
                            {
                                DrawError(error);
                            }
                        }

                        List<GUIContent> modifierWarnings = GetPostActionWarnings();
                        if (modifierWarnings.Count > 0)
                        {
                            foreach (GUIContent warning in modifierWarnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }
            }
        }

        public static void DrawWarning(GUIContent content)
        {
            Color color = GUI.color;
            GUI.color = Color.yellow;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIContent iconContent = new GUIContent(Utils.WarningIcon, "Warning: " + content.tooltip);
                GUILayout.Label(iconContent, EditorStyles.label, GUILayout.Width(15), GUILayout.Height(15));
                GUILayout.Label(content);
                DrawSettingsLinkButton(content, color);
            }
            GUI.color = color;
        }

        public static void DrawError(GUIContent content)
        {
            Color color = GUI.color;
            GUI.color = Color.red;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIContent iconContent = new GUIContent(Utils.ErrorIcon, "Error: " + content.tooltip);
                GUILayout.Label(iconContent, EditorStyles.label, GUILayout.Width(15), GUILayout.Height(15));
                GUILayout.Label(content);
                DrawSettingsLinkButton(content, color);
            }
            GUI.color = color;
        }

        private static void DrawSettingsLinkButton(GUIContent content, Color originalColor)
        {
            if (content is SettingsLinkGUIContent settingsLink)
            {
                GUILayout.FlexibleSpace();
                Color tint = GUI.color;
                GUI.color = originalColor;

                // Logging in is usually the whole fix, so offer it here rather than making the user go
                // to Preferences to press the same button.
                AuthStatusButton.Draw(settingsLink.AuthService);

                if (GUILayout.Button(settingsLink.ButtonText, GUILayout.ExpandWidth(false)))
                {
                    settingsLink.OpenSettings();
                }
                GUI.color = tint;
            }
        }

        private void OnGUIExpanded(float windowWidth, ref bool isDirty)
        {
            if (m_deferredIsDirty)
            {
                isDirty = true;
                m_deferredIsDirty = false;
            }
            
            using (new GUILayout.HorizontalScope())
            {
                float maxWidth = windowWidth / 4;
                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawExpandedSources(ref isDirty);
                }

                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawExpandedModifiers(ref isDirty);
                }

                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawExpandedDestinations(ref isDirty);
                }
                
                using (new GUILayout.VerticalScope("box", GUILayout.MaxWidth(maxWidth)))
                {
                    DrawExpandedActions(ref isDirty);
                }
            }
        }

        private void DrawExpandedSources(ref bool isDirty)
        {
            GUILayout.Label("Sources", m_titleStyle);
            for (var i = 0; i < m_buildSources.Count; i++)
            {
                var source = m_buildSources[i];
                using (new GUILayout.HorizontalScope())
                {
                    isDirty |= CustomToggle.DrawToggle(ref source.Enabled, GUILayout.Width(20));

                    using (new EditorGUI.DisabledScope(!source.Enabled))
                    {
                        GUILayout.Label("Source Type: ", GUILayout.Width(100));
                        if (UIHelpers.SourcesPopup.DrawPopup(ref source.SourceType, m_context))
                        {
                            isDirty = true;
                            Utils.CreateInstance(source.SourceType?.Type, out source.Source);
                            source.Source.Context.SetParent(m_context);
                        }
                    }

                    if (source.SourceType != null)
                    {
                        if (source.SourceType.Type.TryGetWikiLink(out string sourceURL))
                        {
                            if (GUILayout.Button("?", GUILayout.Width(20)))
                            {
                                Application.OpenURL(sourceURL);
                            }
                        }
                    }
                            
                    if (CustomSettingsIcon.OnGUI())
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove Source"), false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Remove Source",
                                    "Are you sure you want to remove this source?",
                                    "Yes", "Oops No!"))
                            {
                                m_buildSources.Remove(source);
                                m_deferredIsDirty = true;
                            }
                        });
                        menu.ShowAsContext();
                    }
                }

                if (source.Source != null)
                {
                    using (new EditorGUI.DisabledScope(!source.Enabled))
                    {
                        source.Source.OnGUIExpanded(ref isDirty, source);

                        using (new GUILayout.HorizontalScope())
                        {
                            GUIContent subFolderContent = new GUIContent("Sub Folder: ", 
                                "A sub-path in the cached directory of which this source will be saved to before being modified and uploaded. Leave empty to save to the root folder.");
                            GUILayout.Label(subFolderContent, GUILayout.Width(120));
                            if (EditorUtils.FormatStringTextField(ref source.SubFolder, ref source.ShowFormattedExportFolder, m_context))
                            {
                                isDirty = true;
                            }
                        }

                        if (source.Source.CanCacheContents)
                        {
                            using (new GUILayout.HorizontalScope())
                            {
                                GUIContent doNotCacheLabel = new GUIContent("Do Not Cache: ",
                                    "If enabled, all files will be saved directly to the folder for the upload task and not cached for future reference. This will speed up uploading in some cases at the cost of having to re-get the source contents every upload." +
                                    "\nExample: If using BuildConfigSource it will make a full new build every upload");
                                GUILayout.Label(doNotCacheLabel, GUILayout.Width(120));
                                isDirty |= CustomToggle.DrawToggle(ref source.DoNotCache, GUILayout.Width(20));
                            }
                        }

                        foreach (ModifierData modifer in m_modifiers)
                        {
                            if (modifer.Modifier == null || !modifer.Enabled)
                            {
                                continue;
                            }
                                    
                            List<GUIContent> errors = new List<GUIContent>();
                            modifer.Modifier.TryGetErrors(source.Source, errors);
                            foreach (GUIContent warning in errors)
                            {
                                DrawError(warning);
                            }
                                    
                            List<GUIContent> warnings = new List<GUIContent>();
                            modifer.Modifier.TryGetWarnings(source.Source, warnings);
                            foreach (GUIContent warning in warnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }

                if (i > 0)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label("Duplicate Files: ", GUILayout.Width(120));
                        var newHandler = (Utils.FileExistHandling)EditorGUILayout.EnumPopup(source.DuplicateFileHandling);
                        if (source.DuplicateFileHandling != newHandler)
                        {
                            source.DuplicateFileHandling = newHandler;
                            isDirty = true;
                        }
                    }
                        
                }
                        
                if (source.Enabled)
                {
                    if (source.Source != null)
                    {
                        List<GUIContent> errors = new List<GUIContent>();
                        source.Source.TryGetErrors(errors);
                        foreach (GUIContent error in errors)
                        {
                            DrawError(error);
                        }
                            
                        List<GUIContent> warnings = new List<GUIContent>();
                        source.Source.TryGetWarnings(warnings);
                        foreach (GUIContent warning in warnings)
                        {
                            DrawWarning(warning);
                        }
                    }
                    else
                    {
                        DrawError(new GUIContent("No source selected"));
                    }
                }
                        
                GUILayout.Space(10);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Source"))
                {
                    AddSource(new SourceData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        private void DrawExpandedModifiers(ref bool isDirty)
        {
            GUILayout.Label("Modifiers", m_titleStyle);
            for (var i = 0; i < m_modifiers.Count; i++)
            {
                ModifierData modifiers = m_modifiers[i];
                using (new GUILayout.HorizontalScope())
                {
                    isDirty |= CustomToggle.DrawToggle(ref modifiers.Enabled, GUILayout.Width(20));

                    using (new EditorGUI.DisabledScope(!modifiers.Enabled))
                    {
                        GUILayout.Label("Modifier Type: ", GUILayout.Width(100));
                        if (UIHelpers.ModifiersPopup.DrawPopup(ref modifiers.ModifierType, m_context))
                        {
                            isDirty = true;
                            Utils.CreateInstance(modifiers.ModifierType?.Type, out modifiers.Modifier);
                            if (modifiers.Modifier != null)
                            {
                                modifiers.Modifier.Context.SetParent(m_context);
                            }
                        }
                    }
                            
                    if (modifiers.ModifierType != null)
                    {
                        if (modifiers.ModifierType.Type.TryGetWikiLink(out string sourceURL))
                        {
                            if (GUILayout.Button("?", GUILayout.Width(20)))
                            {
                                Application.OpenURL(sourceURL);
                            }
                        }
                    }

                    if (CustomSettingsIcon.OnGUI())
                    {
                        GenericMenu menu = new GenericMenu();
                                
                        menu.AddSeparator("");
                                
                        menu.AddItem(new GUIContent("Remove Modifier"), false, () =>
                        {
                            if (EditorUtility.DisplayDialog("Remove Modifier",
                                    "Are you sure you want to remove this Modifier?",
                                    "Yes", "Oops No!"))
                            {
                                m_modifiers.Remove(modifiers);
                                m_deferredIsDirty = true;
                            }
                        });
                        menu.ShowAsContext();
                    }
                }
                        
                if (modifiers.Modifier != null)
                {
                    using (new EditorGUI.DisabledScope(!modifiers.Enabled))
                    {
                        modifiers.Modifier.OnGUIExpanded(ref isDirty, m_context);
                    }


                    if (modifiers.Enabled)
                    {
                        List<GUIContent> errors = new List<GUIContent>();
                        modifiers.Modifier.TryGetErrors(this, errors);
                        foreach (GUIContent error in errors)
                        {
                            DrawError(error);
                        }

                        List<GUIContent> warnings = new List<GUIContent>();
                        modifiers.Modifier.TryGetWarnings(this, warnings);
                        foreach (GUIContent warning in warnings)
                        {
                            DrawWarning(warning);
                        }
                    }
                }
                else if (modifiers.Enabled)
                {
                    DrawError(new GUIContent("No modifier selected"));
                }
                        
                GUILayout.Space(10);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Modifier"))
                {
                    AddModifier(new ModifierData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        private void DrawExpandedDestinations(ref bool isDirty)
        {
            GUILayout.Label("Destinations", m_titleStyle);
            for (var i = 0; i < m_buildDestinations.Count; i++)
            {
                var destinationData = m_buildDestinations[i];
                using (new GUILayout.HorizontalScope())
                {
                    isDirty |= CustomToggle.DrawToggle(ref destinationData.Enabled, GUILayout.Width(20));

                    GUILayout.Label("Destination Type: ", GUILayout.Width(120));
                    using (new EditorGUI.DisabledScope(!destinationData.Enabled))
                    {
                        if (UIHelpers.DestinationsPopup.DrawPopup(ref destinationData.DestinationType, m_context))
                        {
                            isDirty = true;
                            Utils.CreateInstance(destinationData.DestinationType?.Type, out destinationData.Destination);
                            destinationData.Destination.Context.SetParent(m_context);
                        }
                    }
                            
                    if (destinationData.DestinationType != null)
                    {
                        if (destinationData.DestinationType.Type.TryGetWikiLink(out string sourceURL))
                        {
                            if (GUILayout.Button("?", GUILayout.Width(20)))
                            {
                                Application.OpenURL(sourceURL);
                            }
                        }
                    }
                            
                            
                    if (CustomSettingsIcon.OnGUI())
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove Destination"), false, () =>
                        {
                            if(EditorUtility.DisplayDialog("Remove Destination",
                                   "Are you sure you want to remove this destination?", 
                                   "Yes", "Oops No!"))
                            {
                                m_buildDestinations.Remove(destinationData);
                                m_deferredIsDirty = true;
                            }
                        });
                        menu.ShowAsContext();
                    }
                }

                if (destinationData.Destination != null)
                {
                    using (new EditorGUI.DisabledScope(!destinationData.Enabled))
                    {
                        destinationData.Destination.OnPreGUI(ref isDirty, m_context);
                        destinationData.Destination.OnGUIExpanded(ref isDirty);

                        if (destinationData.Enabled)
                        {
                            List<GUIContent> errors = new List<GUIContent>();
                            destinationData.Destination.TryGetErrors(errors);
                            foreach (GUIContent error in errors)
                            {
                                DrawError(error);
                            }

                            List<GUIContent> warnings = new List<GUIContent>();
                            destinationData.Destination.TryGetWarnings(warnings, m_context);
                            foreach (ModifierData modifier in m_modifiers)
                            {
                                if (modifier.ModifierType == null || !modifier.Enabled)
                                {
                                    continue;
                                }

                                modifier.Modifier.TryGetWarnings(destinationData.Destination, warnings);
                            }

                            foreach (GUIContent warning in warnings)
                            {
                                DrawWarning(warning);
                            }
                        }
                    }
                }
                else if(destinationData.Enabled)
                {
                    DrawError(new GUIContent("No destination selected"));
                }
                        
                GUILayout.Space(10);
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Destination"))
                {
                    AddDestination(new DestinationData()
                    {
                        Enabled = true,
                    });
                    isDirty = true;
                }
            }
        }

        /// <summary>
        /// Draws the "Stop task if this fails" toggle for an action entry.
        /// </summary>
        /// <returns>True if the value changed</returns>
        internal static bool DrawStopTaskIfFailedToggle(UploadActionData actionData)
        {
            GUIContent content = new GUIContent("Stop task if this fails",
                "If this action fails, the task fails and skips its remaining build and upload steps. " +
                "Cleanup and actions that run when the task finishes still run.");
            bool stop = GUILayout.Toggle(actionData.StopTaskIfFailed, content, GUILayout.Width(StopTaskIfFailedToggleWidth));
            if (stop == actionData.StopTaskIfFailed)
            {
                return false;
            }

            actionData.StopTaskIfFailed = stop;
            return true;
        }

        private void DrawExpandedActions(ref bool isDirty)
        {
            GUILayout.Label("Actions", m_titleStyle);
            for (var i = 0; i < m_postActions.Count; i++)
            {
                var actionData = m_postActions[i];
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Action Type: ", GUILayout.Width(120));
                    var status =
                        (UploadActionData.UploadCompleteStatus)EditorGUILayout.EnumPopup(actionData.WhenToExecute,
                            GUILayout.Width(100));
                    if (status != actionData.WhenToExecute)
                    {
                        actionData.WhenToExecute = status;
                        isDirty = true;
                    }

                    isDirty |= DrawStopTaskIfFailedToggle(actionData);

                    bool disabled = actionData.WhenToExecute == UploadActionData.UploadCompleteStatus.Never;
                    using (new EditorGUI.DisabledScope(disabled))
                    {
                        if (UIHelpers.ActionsPopup.DrawPopup(ref actionData.ActionType, m_context,
                                GUILayout.Width(200)))
                        {
                            isDirty = true;
                            Utils.CreateInstance(actionData.ActionType?.Type, out actionData.UploadAction);
                            if (actionData.UploadAction != null)
                            {
                                actionData.UploadAction.Context.SetParent(m_context);
                                actionData.StopTaskIfFailed = actionData.UploadAction.DefaultStopTaskIfFailed;
                            }
                        }
                    }
                    
                    if (CustomSettingsIcon.OnGUI())
                    {
                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove Action"), false, () =>
                        {
                            if(EditorUtility.DisplayDialog("Remove Action",
                                   "Are you sure you want to remove this Post Action?", 
                                   "Yes", "Oops No!"))
                            {
                                m_postActions.Remove(actionData);
                                m_deferredIsDirty = true;
                            }
                        });
                        menu.ShowAsContext();
                    }
                }

                using (new GUILayout.VerticalScope())
                {
                    if (actionData.ActionType != null)
                    {
                        using (new EditorGUI.DisabledScope(actionData.WhenToExecute == UploadActionData.UploadCompleteStatus.Never))
                        {
                            actionData.UploadAction.OnGUIExpanded(ref isDirty);

                            if (actionData.WhenToExecute != UploadActionData.UploadCompleteStatus.Never)
                            {
                                List<GUIContent> errors = new List<GUIContent>();
                                actionData.UploadAction.TryGetErrors(errors);
                                foreach (GUIContent error in errors)
                                {
                                    DrawError(error);
                                }

                                List<GUIContent> warnings = new List<GUIContent>();
                                actionData.UploadAction.TryGetWarnings(warnings);
                                foreach (GUIContent warning in warnings)
                                {
                                    DrawWarning(warning);
                                }
                            }
                        }
                    }
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add New Action"))
                {
                    AddPostAction(new UploadActionData()
                    {
                        WhenToExecute = UploadActionData.UploadCompleteStatus.Always
                    });
                    isDirty = true;
                }
            }
        }
    }
}