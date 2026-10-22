using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class SteamUploadDestination
    {
        private bool m_showFormattedLocalPath = Preferences.DefaultShowFormattedTextToggle;
        private bool m_showFormattedDescription = Preferences.DefaultShowFormattedTextToggle;
        private bool m_queuedDirty; // Workaround for changing channels via GenericMenu since it can't reference isDirty

        public override void OnPreGUI(ref bool isDirty, Context ctx)
        {
            base.OnPreGUI(ref isDirty, ctx);

            if (m_createAppFile || string.IsNullOrEmpty(m_appFileName))
            {
                return;
            }

            string appFileName = GetVDFFullPath(m_appFileName);
            if (!File.Exists(appFileName))
            {
                Debug.LogError("Steam Upload Destination: App file does not exist at path: " + appFileName);
                return;
            }

            AppVDFFile vdfFile = VDFFile.Load<AppVDFFile>(appFileName);
            if (vdfFile == null)
            {
                Debug.LogError("Steam Upload Destination: Failed to load app VDF file at path: " + appFileName);
                return;
            }

            m_destinationBranch = null;
            m_depots.Clear();
            
            // App
            m_app = SteamUIUtils.GetSteamBuildData().Configs.FirstOrDefault(a=>a.App.appid == vdfFile.appid);
            if (m_app == null)
            {
                return;
            }

            // Branch
            string branchName = vdfFile.setlive;
            if (string.IsNullOrEmpty(branchName))
            {
                m_destinationBranch = new SteamBranch("none");
            }
            else
            {
                m_destinationBranch = m_app.ConfigBranches.FirstOrDefault(b => b.DisplayName == branchName);
                if (m_destinationBranch == null)
                {
                    Debug.LogError("Steam Upload Destination: Failed to load branch name: " + branchName);
                }
            }
            
            // Depot
            string directory = Path.GetDirectoryName(appFileName);
            foreach (VdfMap<int, string>.MapData depotData in vdfFile.depots.GetData())
            {
                int depotID = depotData.Key;
                string path = depotData.Value;
                string fullPath = Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(directory, path));
                DepotVDFFile depotFile = VDFFile.Load<DepotVDFFile>(fullPath);
                if(depotFile == null)
                {
                    Debug.LogError("Steam Upload Destination: Failed to load depot file at path: " + fullPath);
                    continue;
                }

                SteamDepot depot = m_app.Depots.FirstOrDefault(d => d.Depot.DepotID == depotFile.DepotID);
                if (depot != null)
                {
                    if (!m_depots.Contains(depot))
                    {
                        m_depots.Add(depot);
                    }
                }
                else
                {
                    Debug.LogError("Steam Upload Destination: Failed to find depot with ID: " + depotFile.DepotID);
                }
            }
        }

        protected internal override void OnGUICollapsed(ref bool isDirty, float maxWidth)
        {
            float segmentLength;
            if (!m_createAppFile)
            {
                segmentLength = maxWidth / 4f;
                string appFileName = GetVDFFullPath(m_appFileName);
                if (!string.IsNullOrEmpty(appFileName) && File.Exists(appFileName))
                {
                    appFileName = Path.GetFileName(appFileName);
                }

                int length = Mathf.FloorToInt(maxWidth/ (4 * 8)); // 8'ish pixels per character
                if (appFileName.Length > length)
                {
                    appFileName = appFileName.Substring(0, length) + "...";
                }

                GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.alignment = TextAnchor.MiddleLeft;
                if (GUILayout.Button(appFileName, buttonStyle, GUILayout.Width(segmentLength)))
                {
                    var newName = EditorUtility.OpenFilePanel("Select AppFile", "", "vdf");
                    if (!string.IsNullOrEmpty(newName))
                    {
                        m_appFileName = newName;
                        isDirty = true;
                    }
                }
            }
            else
            {
                segmentLength = maxWidth / 3f;
            }
            
            using (new EditorGUI.DisabledScope(!m_createAppFile))
            {
                isDirty |= SteamUIUtils.ConfigPopup.DrawPopup(ref m_app, m_context, GUILayout.Width(segmentLength));
                isDirty |= SteamUIUtils.BranchPopup.DrawPopup(m_app, ref m_destinationBranch, m_context, GUILayout.Width(segmentLength));
                EditorUtils.DrawPopup(m_depots, m_app?.Depots, "Choose Depots",
                    (newDepots) =>
                    {
                        m_depots = newDepots;
                        m_queuedDirty = true;
                    }, GUILayout.Width(segmentLength));
            }

            isDirty |= m_queuedDirty;
            m_queuedDirty = false;
        }
        
        protected internal override void OnGUIExpanded(ref bool isDirty)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Preview Upload:", GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_previewUpload);
            }
            // Tools
            using (new GUILayout.HorizontalScope())
            {
                GUIContent appFileLabel = new GUIContent("Create AppFile:", "If enabled will create a new app file for specified app and depots. If false will use an existing app file and containing depots that you can specify.");
                GUILayout.Label(appFileLabel, GUILayout.Width(120));
                isDirty |= CustomToggle.DrawToggle(ref m_createAppFile, GUILayout.Width(20));
            }

            using(new EditorGUI.DisabledScope(m_createAppFile))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent appFilePathLabel = new GUIContent("    Path:", "The path to an existing app file to use for the upload. This is only used if 'Create AppFile' is disabled.");
                    GUILayout.Label(appFilePathLabel, GUILayout.Width(160));
                    isDirty |= CustomFilePathTextField.OnGUI(ref m_appFileName, ref m_showFormattedLocalPath, m_context, "vdf");
                }
                
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent overwriteDescLabel = new GUIContent("    Overwrite Desc:", "If enabled the chosen appFile will be copied and modified to include the description from the build uploader.");
                    GUILayout.Label(overwriteDescLabel, GUILayout.Width(160));
                    isDirty |= CustomToggle.DrawToggle(ref m_appFileOverwriteDesc, GUILayout.Width(20));
                }
            }
            
            // App
            using(new EditorGUI.DisabledScope(!m_createAppFile))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent label = new GUIContent("App:", "The Steam App (game) to upload to. This is the App ID you set up in your Steamworks partner account.");
                    GUILayout.Label(label, GUILayout.Width(120));
                    isDirty |= SteamUIUtils.ConfigPopup.DrawPopup(ref m_app, m_context);
                }

                // Branch
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent label = new GUIContent("Branch:", "The Steam Branch to upload to. Branches are defined in your Steamworks partner account and represent different release channels (for example public, beta, alpha)." +
                                                                 "\nNOTE: You can not upload to 'default' branch" +
                                                                 "\nNOTE: Uploading to 'none' will upload the build to steamworks but not assign to a branch.");
                    GUILayout.Label(label, GUILayout.Width(120));
                    isDirty |= SteamUIUtils.BranchPopup.DrawPopup(m_app, ref m_destinationBranch, m_context);
                }

                // Depots
                using (new GUILayout.HorizontalScope())
                {
                    GUIContent label = new GUIContent("Depots:", "The Steam Depot to upload to. Depots are defined in your Steamworks partner account and represent a build target (for example Windows, Mac, Linux).");
                    GUILayout.Label(label, GUILayout.Width(120));
                    EditorUtils.DrawPopup(m_depots, m_app?.Depots, "Choose Depots",
                        (newDepots) =>
                        {
                            m_depots = newDepots;
                            m_queuedDirty = true;
                        });
                }
            }

            using (new GUILayout.HorizontalScope())
            {
                GUIContent label = new GUIContent("Description Format:", "Description for developers that appears on Steamworks.");
                GUILayout.Label(label, GUILayout.Width(120));
                isDirty |= EditorUtils.FormatStringTextArea(ref m_descriptionFormat, ref m_showFormattedDescription, m_context);
            }
            
            isDirty |= m_queuedDirty;
            m_queuedDirty = false;
        }

        private string GetVDFFullPath(string fileName)
        {
            if (Path.IsPathRooted(fileName))
                return fileName;
            else
                return Path.GetFullPath(Path.Combine(SteamSDK.SteamScriptPath, fileName));
        }

        public override string Summary()
        {
            // Branch: Staging, Depots: Windows, Mac
            return $"Branch: {m_uploadBranch.DisplayName} Depots: {string.Join(", ", m_depots.Select(a=>a.DisplayName))}";
        }
    }
}
