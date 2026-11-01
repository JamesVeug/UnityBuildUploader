using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload the build to a folder on Google Drive
    ///
    /// NOTE: This class's name path is saved in the JSON file so avoid renaming.
    /// </summary>
    [Experimental]
    [Wiki(nameof(GoogleDriveDestination), "destinations", "Upload a build to Google Drive.")]
    [UploadDestination("Google Drive")]
    public partial class GoogleDriveDestination : AUploadDestination
    {
        [Wiki("App", "Which Google App to use to upload.", 1)]
        private GoogleConfig.GoogleApp m_app;

        [Wiki("Folder", "Which Google Drive folder to upload to. Leave unset to upload to the root of My Drive.", 2)]
        private GoogleConfig.GoogleDriveFolder m_folder;

        [Wiki("File Name", "Name the uploaded file will appear as on Drive. Supports string formatting like {version}, {buildTarget} and {time}.", 3)]
        private string m_fileNameFormat = "{taskProfileName}_{version}_{buildTarget}";

        [Wiki("Zip Contents", "When uploading a folder: if true, the folder is zipped and uploaded as a single file. If false, each top-level file is uploaded individually.", 4)]
        private bool m_zipContents = true;

        public GoogleDriveDestination() : base()
        {
            // Required for reflection
        }

        public GoogleDriveDestination(string appName, string folderId) : base()
        {
            m_app = new GoogleConfig.GoogleApp { Name = appName };
            m_folder = new GoogleConfig.GoogleDriveFolder { FolderId = folderId };
        }

        public void SetApp(string appName)
        {
            m_app = new GoogleConfig.GoogleApp { Name = appName };
        }

        public void SetFolder(string folderId)
        {
            m_folder = new GoogleConfig.GoogleDriveFolder { FolderId = folderId };
        }

        public void SetFileNameFormat(string fileNameFormat)
        {
            m_fileNameFormat = fileNameFormat;
        }

        public void SetZipContents(bool zipContents)
        {
            m_zipContents = zipContents;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            string contentPath = m_context.FormatString(m_taskContentsFolder);
            string folderId = m_folder != null ? m_folder.FolderId : "";
            string token = m_app.Token;
            string nameBase = m_context.FormatString(m_fileNameFormat);

            int processID = ProgressUtils.Start("Google Drive", "Uploading to Google Drive");
            try
            {
                if (Directory.Exists(contentPath))
                {
                    if (m_zipContents)
                    {
                        // Zip the contents into a temp file and upload as a single file
                        string zipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
                        if (!await ZipUtils.Zip(contentPath, zipPath, result))
                        {
                            return false;
                        }

                        string zipName = string.IsNullOrEmpty(nameBase)
                            ? Path.GetFileName(contentPath) + ".zip"
                            : nameBase + ".zip";

                        GoogleDriveUploadResponse zipResponse = await GoogleDrive.UploadFile(zipPath, zipName, folderId, token, result);
                        m_recordedFileId = zipResponse.FileId;
                        m_recordedWebViewLink = zipResponse.WebViewLink;

                        try { File.Delete(zipPath); } catch { /* best effort cleanup */ }
                        return zipResponse.Successful;
                    }

                    // Upload each top-level file individually. Sub-folders are ignored
                    // Use a Zip Modifier earlier in the pipeline to flatten sub-folders.
                    string[] files = Directory.GetFiles(contentPath, "*.*", SearchOption.TopDirectoryOnly);
                    if (files.Length == 0)
                    {
                        result.SetFailed($"No files to upload at: {contentPath}");
                        return false;
                    }

                    bool allSucceeded = true;
                    foreach (string file in files)
                    {
                        string uploadName = string.IsNullOrEmpty(nameBase)
                            ? Path.GetFileName(file)
                            : nameBase + Path.GetExtension(file);
                        GoogleDriveUploadResponse response = await GoogleDrive.UploadFile(file, uploadName, folderId, token, result);
                        if (!response.Successful)
                        {
                            allSucceeded = false;
                        }
                        else
                        {
                            // Record the most recent successful upload
                            m_recordedFileId = response.FileId;
                            m_recordedWebViewLink = response.WebViewLink;
                        }
                    }

                    return allSucceeded;
                }

                if (File.Exists(contentPath))
                {
                    string uploadName = string.IsNullOrEmpty(nameBase)
                        ? Path.GetFileName(contentPath)
                        : nameBase + Path.GetExtension(contentPath);
                    GoogleDriveUploadResponse response = await GoogleDrive.UploadFile(contentPath, uploadName, folderId, token, result);
                    m_recordedFileId = response.FileId;
                    m_recordedWebViewLink = response.WebViewLink;
                    return response.Successful;
                }

                result.SetFailed($"Path does not exist: {contentPath}");
                return false;
            }
            finally
            {
                ProgressUtils.Remove(processID);
            }
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            GoogleService service = InternalUtils.GetService<GoogleService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Google App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.Token))
            {
                errors.Add(service.PreferencesLink($"Google App {m_app.Name} does not have an OAuth2 access token set.", ""));
            }

            if (string.IsNullOrEmpty(m_fileNameFormat))
            {
                errors.Add(new GUIContent("File Name Format is not set."));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["app"] = m_app?.Id ?? 0,
                ["folder"] = m_folder?.Id ?? 0,
                ["fileNameFormat"] = m_fileNameFormat,
                ["zipContents"] = m_zipContents
            };
            return dict;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            GoogleConfig.GoogleApp[] apps = GoogleUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object appId) && appId != null)
            {
                m_app = apps.FirstOrDefault(a => a.Id == (long)appId);
            }

            GoogleConfig.GoogleDriveFolder[] folders = GoogleUIUtils.DriveFolderPopup.Values;
            if (data.TryGetValue("folder", out object folderId) && folderId != null)
            {
                m_folder = folders.FirstOrDefault(f => f.Id == (long)folderId);
            }

            if (data.TryGetValue("fileNameFormat", out object fileNameObj) && fileNameObj != null)
            {
                m_fileNameFormat = fileNameObj.ToString();
            }
            else
            {
                m_fileNameFormat = "{taskProfileName}_{version}_{buildTarget}";
            }

            if (data.TryGetValue("zipContents", out object zipObj) && zipObj is bool zipBool)
            {
                m_zipContents = zipBool;
            }
            else
            {
                m_zipContents = true;
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            Dictionary<string, object> state = base.CaptureRunState();
            state["recordedFileId"] = m_recordedFileId;
            state["recordedWebViewLink"] = m_recordedWebViewLink;
            return state;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            base.RestoreRunState(state);
            m_recordedFileId = state["recordedFileId"] as string;
            m_recordedWebViewLink = state["recordedWebViewLink"] as string;
        }
    }
}
