using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload a build to Steamworks
    /// 
    /// NOTE: This classes name path is saved in the JSON file so avoid renaming
    /// </summary>
    [Wiki("Steamworks", "destinations", "Uploads files to Steamworks")]
    [UploadDestination("Steamworks")]
    public partial class SteamUploadDestination : AUploadDestination
    {
        [Wiki("App", "Which Steam App to upload to. eg: 1141030", 1)]
        private SteamApp m_app;
        
        [Wiki("Branch", "Which Branch to upload to. eg: internal", 2)]
        private SteamBranch m_destinationBranch;
        
        [Wiki("Depots", "Which Depots to upload to. eg: 1141031", 3)]
        private List<SteamDepot> m_depots = new List<SteamDepot>();
        
        [Wiki("Create AppFile", "If true, a new App File is creating to upload the build to Steam.", 4)]
        private bool m_createAppFile = true;
        
        [Wiki("AppFile Path", "If Create AppFile is false then use a file with this name that will be found in the SteamSDKs path to upload a build to Steam.", 5)]
        private string m_appFileName = "";
        
        [Wiki("Overwrite AppFile Description", "If Create AppFile is false and this is true, the the chosen appFile will be copied and description changed to fit selected Build Uploader description.", 6)]
        private bool m_appFileOverwriteDesc = true;

        [Wiki("Description Format", "What description to upload to steam to appear on steamworks.", 9)]
        private string m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;

        [Wiki("Preview Upload", "Ask SteamCMD to validate and process the build without committing its content.", 10)]
        private bool m_previewUpload;
        
        private SteamApp m_uploadApp;
        private List<SteamDepot> m_uploadDepots = new List<SteamDepot>();
        private SteamBranch m_uploadBranch;
        private string m_appPath;
        private List<string> m_depotPaths = new List<string>();

        public SteamUploadDestination() : base()
        {
            // Required for reflection
        }
        
        public SteamUploadDestination(int appID, string branchName, params int[] depotIDs) : base()
        {
            SetSteamApp(appID);
            SetSteamBranch(branchName);
            foreach (int despotID in depotIDs)
            {
                AddSteamDepot(despotID);
            }
        }

        public void SetSteamApp(int appID)
        {
            m_app = new SteamApp()
            {
                App = new AppVDFFile()
                {
                    appid = appID
                }
            };
        }
        
        public void AddSteamDepot(int depotID)
        {
            m_depots.Add(new SteamDepot()
            {
                Depot = new DepotVDFFile()
                {
                    DepotID = depotID
                }
            });
        }
        
        public void SetSteamBranch(string branchName)
        {
            m_destinationBranch = new SteamBranch(branchName);
        }
        
        public void UseExistingAppFile(string appFilePath)
        {
            m_createAppFile = true;
            m_appFileName = appFilePath;
        }

        public void SetDescriptionFormat(string format)
        {
            m_descriptionFormat = format;
        }

        public void SetPreviewUpload(bool previewUpload)
        {
            m_previewUpload = previewUpload;
        }

        public override async Task<bool> Prepare(string taskGUID, int configIndex, int destinationIndex,
            string taskContentsFolder, UploadTaskReport.StepResult result)
        {
            await base.Prepare(taskGUID, configIndex, destinationIndex, taskContentsFolder, result);

            if (m_app == null)
            {
                result.SetFailed("No App selected");
                return false;
            }
            
            if (m_depots == null || m_depots.Count == 0)
            {
                result.SetFailed("No Depot selected");
                return false;
            }
            
            if (m_destinationBranch == null)
            {
                result.SetFailed("No Branch selected");
                return false;
            }
            

            string buildDescription = m_context.FormatString(m_descriptionFormat);
            string suffix = $"buildUploader_{taskGUID}_{configIndex}_{destinationIndex}";
            if (m_createAppFile)
            {
                result.AddLog("Creating new app file: " + m_appPath);
                m_uploadApp = new SteamApp(m_app);
                m_uploadApp.App.preview = m_previewUpload;
                m_uploadBranch = new SteamBranch(m_destinationBranch);
                m_uploadDepots = m_depots.Select(a=>new SteamDepot(a)).ToList();
                
                var depots = m_uploadDepots.Select(a=>a.Depot).ToList();
                string appFiles = await SteamSDK.Instance.CreateAppFiles(m_uploadApp.App, depots, m_uploadBranch.name, buildDescription, m_taskContentsFolder, result, suffix);
                if (string.IsNullOrEmpty(appFiles))
                {
                    // NOTE: SetFailed called in CreateAppFiles
                    return false;
                }
                m_appPath = appFiles; 
                
                result.AddLog("Creating new depot files");
                foreach (DepotVDFFile file in depots)
                {
                    string depotFiles = await SteamSDK.Instance.CreateDepotFiles(file, m_uploadBranch.name, result, suffix);
                    if (string.IsNullOrEmpty(depotFiles))
                    {
                        return false;
                    }
                    m_depotPaths.Add(depotFiles);
                    result.AddLog("Created new depot file: " + depotFiles);
                }
            }
            else
            {
                result.SetFailed("using existing add file: '" + m_appFileName + "'");
                
                // Use the provided app file name
                string[] files = GetVDFFile(m_appFileName);
                if (files.Length == 0)
                {
                    result.SetFailed("App file not found: '" + m_appFileName + "'");
                    return false;
                }
                else if (files.Length > 1)
                {
                    result.SetFailed("Multiple App files found with name: '" + m_appFileName + "'. Specify a unique App File name.");
                    return false;
                }
                
                m_appPath = files[0];
                if (m_appFileOverwriteDesc)
                {
                    if (!SteamSDK.TryCopyAppFileAndModifyDescAtPath(m_appPath, out m_appPath, buildDescription, result))
                    {
                        return false;
                    }
                }
                
                // Load app file
                AppVDFFile appFile = VDFFile.Load<AppVDFFile>(m_appPath);
                if (appFile == null)
                {
                    result.SetFailed("Failed to load app file to get branch: " + m_appPath);
                    return false;
                }

                m_uploadApp = new SteamApp(appFile);
                
                // Get branch
                m_uploadBranch = new SteamBranch(appFile.setlive);

                // Get depots
                foreach (VdfMap<int, string>.MapData depots in appFile.depots.GetData())
                {
                    string[] depotFiles = GetVDFFile(depots.Value);
                    if (depotFiles.Length == 0)
                    {
                        result.SetFailed("Depot file not found: '" + depots.Value + "'");
                        return false;
                    }
                    else if (depotFiles.Length > 1)
                    {
                        result.SetFailed("Multiple Depot files found with name: '" + depots.Value + "'. Specify a unique Depot File name.");
                        return false;
                    }
                    
                    DepotVDFFile depotFile = VDFFile.Load<DepotVDFFile>(depotFiles[0]);
                    if (depotFile == null)
                    {
                        result.SetFailed("Failed to load depot file: " + depotFiles[0]);
                        return false;
                    }
                    else if (depotFile.DepotID != depots.Key)
                    {
                        result.SetFailed("Depot ID in depot file does not match depot ID in app file: " + depotFiles[0]);
                        return false;
                    }
                    
                    m_depotPaths.Add(depotFiles[0]);
                }
            }
            
            if (string.IsNullOrEmpty(m_appPath) || !File.Exists(m_appPath))
            {
                result.SetFailed("Failed to create app file or app file does not exist: " + m_appPath);
                return false;
            }

            if (m_depotPaths == null || m_depotPaths.Count == 0)
            {
                result.SetFailed("No depots specified");
                return false;
            }

            foreach (string depotPath in m_depotPaths)
            {
                if (string.IsNullOrEmpty(depotPath) || !File.Exists(depotPath))
                {
                    result.SetFailed("Failed to create depot file or depot file does not exist: " + depotPath);
                    return false;
                }
            }

            return true;
        }

        private string[] GetVDFFile(string fileName)
        {
            string appFileName = fileName;
            if (!appFileName.EndsWith(".vdf", StringComparison.OrdinalIgnoreCase))
            {
                appFileName += ".vdf"; // Ensure it has .vdf extension
            }
            appFileName = m_context.FormatString(appFileName);

            if (Path.IsPathRooted(appFileName))
            {
                // NOTE: At this time i only support specifying files that are in the Scripts folder
                // TODO: Copy the selected file over to the scripts folder or modify the .vdfs to use absolute paths
                appFileName = Path.GetFileName(appFileName);
            }
            
            return Directory.GetFiles(SteamSDK.SteamScriptPath, appFileName, SearchOption.AllDirectories);
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            return await SteamSDK.Instance.Upload(m_uploadApp.App, m_appPath, result);
        }

        public override Task CleanUp(UploadTaskReport.StepResult stepResult)
        {
            base.CleanUp(stepResult);
            
            m_uploadApp = null;
            m_uploadDepots = new List<SteamDepot>();
            m_uploadBranch = null;
            
            if (m_createAppFile)
            {
                if (SteamworksService.DeleteVDFFilesDuringCleanup)
                {
                    if (File.Exists(m_appPath))
                    {
                        stepResult.AddLog("Deleting app file: " + m_appPath);
                        File.Delete(m_appPath);
                    }

                    foreach (string m_depotPath in m_depotPaths)
                    {
                        if (File.Exists(m_depotPath))
                        {
                            stepResult.AddLog("Deleting depot file: " + m_depotPath);
                            File.Delete(m_depotPath);
                        }
                    }
                }
                else
                {
                    stepResult.AddLog("Skipping deletion of app and depot files as per preferences.");
                }
            }
            m_appPath = null;
            
            m_depotPaths.Clear();
            
            return Task.CompletedTask;
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                ["m_createAppFile"] = m_createAppFile,
                ["m_appFileName"] = m_appFileName,
                ["m_appFileOverwriteDesc"] = m_appFileOverwriteDesc,
                ["configID"] = m_app?.Id,
                ["depotIDs"] = m_depots.Select(a=>a.ID).ToArray(),
                ["branchID"] = m_destinationBranch?.Id,
                ["m_descriptionFormat"] = m_descriptionFormat,
                ["m_previewUpload"] = m_previewUpload
            };

            return data;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            m_previewUpload = data.TryGetValue("m_previewUpload", out object previewValue) && previewValue is bool preview && preview;
            m_createAppFile = (bool)data["m_createAppFile"];
            if (data.TryGetValue("m_appFileName", out object appFileNameObj) && appFileNameObj != null)
            {
                m_appFileName = appFileNameObj.ToString();
            }
            else
            {
                m_appFileName = "";
            }

            if (data.TryGetValue("m_appFileOverwriteDesc", out object appFileOverwriteDescObj) && appFileOverwriteDescObj != null)
            {
                m_appFileOverwriteDesc = appFileOverwriteDescObj is bool b && b;
            }
            else
            {
                m_appFileOverwriteDesc = true;
            }
            
            // Note: In 1.2.2 the serialization data was changed from the Name to ID
            
            // Config
            SteamApp[] buildConfigs = SteamUIUtils.ConfigPopup.Values;
            if (data.TryGetValue("configID", out object configIDString) && configIDString != null && configIDString is long configID)
            {
                m_app = buildConfigs.FirstOrDefault(a=> a.Id == configID);
            }
            else if (data.TryGetValue("m_currentConfig", out object m_currentConfigName))
            {
                m_app = buildConfigs.FirstOrDefault(a=>a.Name == m_currentConfigName.ToString());
            }

            if (m_app == null)
            {
                // No config found, so don't continue.
                return;
            }
            
            // Depots
            m_depots = new List<SteamDepot>();
            List<long> depotIDs = new List<long>();
            if (data.TryGetValue("depotIDs", out object depotIDsObj))
            {
                // v3.1.0 changed from depotID to depotIDs array
                if (depotIDsObj is List<object> depotIDsArray){
                    depotIDs.AddRange(depotIDsArray.Cast<long>());
                }
                else
                {
                    Debug.LogError("Unexpected depotIDs data format.");
                }
            }
            else if(data.TryGetValue("depotID", out var depotIDString))
            {
                // v1.2.2 changed from m_buildDepot to depotID
                if(depotIDString != null && depotIDString is long depotID)
                {
                    depotIDs.Add(depotID);
                }
            }
            else if (data.TryGetValue("m_buildDepot", out object m_buildDepotName))
            {
                // Added in v1v1.2.2
                if (m_buildDepotName != null && m_buildDepotName is long depotIDLong)
                {
                    depotIDs.Add(depotIDLong);
                }
            }
            
            foreach (long depotID in depotIDs)
            {
                SteamDepot depot = m_app.Depots.FirstOrDefault(a => a.Id == depotID);
                if (depot != null)
                {
                    m_depots.Add(depot);
                }
            }
            
            // Branch
            if (data.TryGetValue("branchID", out object branchIDString) && branchIDString != null)
            {
                m_destinationBranch = m_app.ConfigBranches.FirstOrDefault(a=>a.Id == (long)branchIDString);
            }
            else if (data.TryGetValue("m_destinationBranch", out object m_destinationBranchName))
            {
                m_destinationBranch = m_app.ConfigBranches.FirstOrDefault(a=>a.name == m_destinationBranchName.ToString());
            }
            
            // Build Description Format - Added in v3.1.0
            if (data.TryGetValue("m_descriptionFormat", out object descriptionFormatObj) && descriptionFormatObj != null)
            {
                m_descriptionFormat = descriptionFormatObj.ToString();
            }
            else
            {
                m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;
            }


        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            if (m_previewUpload && !m_createAppFile)
            {
                errors.Add(new GUIContent("Preview upload requires Create AppFile so the preview setting can be written safely."));
            }

            SteamworksService service = InternalUtils.GetService<SteamworksService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("No App selected"));
            }

            if(!m_createAppFile)
            {
                if (string.IsNullOrEmpty(m_appFileName))
                {
                    errors.Add(new GUIContent("No App File name specified. Either create a new App File or specify an existing App File name."));
                }
                else
                {
                    string[] appFiles = GetVDFFile(m_appFileName);
                    if (appFiles.Length == 0)
                    {
                        errors.Add(new GUIContent($"App File '{m_appFileName}' not found in path '{SteamSDK.SteamScriptPath}'!"));
                    }
                    else if(appFiles.Length > 1)
                    {
                        errors.Add(new GUIContent($"Multiple App Files found with name: '{m_appFileName}'. Specify a unique App File name."));
                    }
                }
            }

            if (m_depots == null || m_depots.Count == 0)
            {
                errors.Add(new GUIContent("No Depot selected"));
            }
            else
            {
                foreach (SteamDepot depot in m_depots)
                {
                    if (!depot.HasDepotID)
                    {
                        errors.Add(new GUIContent($"Depot '{depot.Name}' has no Depot ID",
                            "Enter the numeric Depot ID from Steamworks in Project Settings > Build Uploader > Steamworks."));
                    }
                }

                foreach (IGrouping<int, SteamDepot> group in m_depots.Where(a => a.HasDepotID).GroupBy(a => a.Depot.DepotID))
                {
                    if (group.Count() > 1)
                    {
                        errors.Add(new GUIContent($"Depot ID {group.Key} is selected more than once",
                            string.Join(", ", group.Select(a => a.Name)) + " share the same Depot ID."));
                    }
                }
            }

            if (m_destinationBranch == null)
            {
                errors.Add(new GUIContent("No Branch selected"));
            }
            else if (m_destinationBranch.name == "default")
            {
                errors.Add(new GUIContent("Uploading to the 'default' branch is not allowed by the SteamSDK.\nUse none or an empty branch name instead and use the Steamworks dashboard to assign to default."));
            }

            if (string.IsNullOrEmpty(m_descriptionFormat))
            {
                errors.Add(new GUIContent("No build description specified."));
            }
        }
    }
}
