using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// Upload the build to a folder on Dropbox using a long-lived access token.
    ///
    /// NOTE: This class's name path is saved in the JSON file so avoid renaming.
    /// </summary>
    [Wiki(nameof(DropboxDestination), "destinations", "Upload to a folder on Dropbox.")]
    [UploadDestination("Dropbox")]
    public partial class DropboxDestination : AUploadDestination
    {
        [Wiki("App", "Which Dropbox App to upload to.", 1)]
        private DropboxConfig.DropboxApp m_app;

        [Wiki("Folder", "Which Dropbox folder to upload to. Leave unset to upload to the root.", 2)]
        private DropboxConfig.DropboxFolder m_folder;

        [Wiki("File Name", "Name the uploaded file will appear as on Dropbox. Supports {keys} like {version}, {buildTarget} and {time}.", 3)]
        private string m_fileNameFormat = "{taskProfileName}_{version}_{buildTarget}";

        [Wiki("Zip Contents", "When uploading a folder: if true, the folder is zipped and uploaded as a single file. If false, each top-level file is uploaded individually.", 4)]
        private bool m_zipContents = true;

        [Wiki("Create Shared Link", "When true, a public shared link is created for the uploaded file and exposed via {dropboxShareLink}.", 5)]
        private bool m_createShareLink = false;

        public DropboxDestination() : base()
        {
            // Required for reflection
        }

        public DropboxDestination(string appName, string folderPath) : base()
        {
            m_app = new DropboxConfig.DropboxApp { Name = appName };
            m_folder = new DropboxConfig.DropboxFolder { Path = folderPath };
        }

        public void SetApp(string appName)
        {
            m_app = new DropboxConfig.DropboxApp { Name = appName };
        }

        public void SetFolder(string folderPath)
        {
            m_folder = new DropboxConfig.DropboxFolder { Path = folderPath };
        }

        public void SetFileNameFormat(string fileNameFormat)
        {
            m_fileNameFormat = fileNameFormat;
        }

        public void SetZipContents(bool zipContents)
        {
            m_zipContents = zipContents;
        }

        public void SetCreateShareLink(bool createShareLink)
        {
            m_createShareLink = createShareLink;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            string contentPath = m_context.FormatString(m_taskContentsFolder);
            string token = m_app.Token;
            string nameBase = m_context.FormatString(m_fileNameFormat);

            int processID = ProgressUtils.Start("Dropbox", "Uploading to Dropbox");
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

                        bool success = await UploadSingleFile(zipPath, zipName, token, result);

                        try { File.Delete(zipPath); } catch { /* best effort cleanup */ }
                        return success;
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
                        if (!await UploadSingleFile(file, uploadName, token, result))
                        {
                            allSucceeded = false;
                        }
                    }

                    return allSucceeded;
                }

                if (File.Exists(contentPath))
                {
                    string uploadName = string.IsNullOrEmpty(nameBase)
                        ? Path.GetFileName(contentPath)
                        : nameBase + Path.GetExtension(contentPath);
                    return await UploadSingleFile(contentPath, uploadName, token, result);
                }

                result.SetFailed($"Path does not exist: {contentPath}");
                return false;
            }
            finally
            {
                ProgressUtils.Remove(processID);
            }
        }

        private async Task<bool> UploadSingleFile(string filePath, string fileName, string token, UploadTaskReport.StepResult result)
        {
            string dropboxPath = CombineDropboxPath(m_folder != null ? m_folder.Path : "", fileName);
            DropboxUploadResponse response = await Dropbox.UploadFile(filePath, dropboxPath, token, result);
            if (!response.Successful)
            {
                return false;
            }

            m_recordedPath = response.PathDisplay;

            if (m_createShareLink)
            {
                DropboxSharedLinkResponse linkResponse = await Dropbox.CreateSharedLink(response.PathDisplay, token, result);
                if (linkResponse.Successful)
                {
                    m_recordedShareLink = linkResponse.Url;
                }
            }

            return true;
        }

        /// <summary>
        /// Combine a configured folder path with a file name into a Dropbox-rooted path.
        /// An empty folder uploads to the root. Dropbox paths must start with "/".
        /// </summary>
        private static string CombineDropboxPath(string folderPath, string fileName)
        {
            string trimmedFolder = string.IsNullOrEmpty(folderPath) ? "" : "/" + folderPath.Trim('/');
            return trimmedFolder + "/" + fileName.TrimStart('/');
        }

        public override string Summary()
        {
            string app = m_app != null ? m_app.Name : "<no app>";
            string folder = m_folder != null && !string.IsNullOrEmpty(m_folder.Name) ? m_folder.Name : "Root";
            return $"Dropbox: {app} → {folder}";
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            DropboxService service = InternalUtils.GetService<DropboxService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_app == null)
            {
                errors.Add(new GUIContent("Dropbox App is not set."));
            }
            else if (string.IsNullOrEmpty(m_app.Token))
            {
                errors.Add(service.PreferencesLink($"Dropbox App {m_app.Name} does not have an access token set.", ""));
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
                ["zipContents"] = m_zipContents,
                ["createShareLink"] = m_createShareLink
            };
            return dict;
        }

        public override void Deserialize(Dictionary<string, object> data)
        {
            DropboxConfig.DropboxApp[] apps = DropboxUIUtils.AppPopup.Values;
            if (data.TryGetValue("app", out object appId) && appId != null)
            {
                m_app = apps.FirstOrDefault(a => a.Id == (long)appId);
            }

            DropboxConfig.DropboxFolder[] folders = DropboxUIUtils.FolderPopup.Values;
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

            if (data.TryGetValue("createShareLink", out object linkObj) && linkObj is bool linkBool)
            {
                m_createShareLink = linkBool;
            }
            else
            {
                m_createShareLink = false;
            }
        }

        /// <summary>
        /// Serializes the runtime state of an Upload Task so it can be rerun later (ie: if there is a mid-run recompile
        /// which clears all tasks and static data)
        /// </summary>
        public override Dictionary<string, object> CaptureRunState()
        {
            Dictionary<string, object> state = base.CaptureRunState();
            state["recordedPath"] = m_recordedPath;
            state["recordedShareLink"] = m_recordedShareLink;
            return state;
        }

        /// <summary>
        /// Applies previously created fields so a task can continue it.
        /// </summary>
        public override void RestoreRunState(Dictionary<string, object> state)
        {
            base.RestoreRunState(state);
            m_recordedPath = state["recordedPath"] as string;
            m_recordedShareLink = state["recordedShareLink"] as string;
        }
    }
}
