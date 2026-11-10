using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Dropbox HTTP API v2 wrapper.
    ///
    /// Uploads use the single-request /2/files/upload endpoint (suitable for files up
    /// to 150MB; larger files require the upload-session endpoints which are not
    /// implemented here yet). Authentication uses a long-lived access token generated
    /// from the Dropbox App Console - no refresh flow is required.
    ///
    /// https://www.dropbox.com/developers/documentation/http/documentation
    /// </summary>
    internal static partial class Dropbox
    {
        /// <summary>
        /// Shared Dropbox service flag. Gates the Dropbox upload destination.
        /// </summary>
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("dropbox_enabled", false);
            set => ProjectEditorPrefs.SetBool("dropbox_enabled", value);
        }

        /// <summary>
        /// Upload a single file to Dropbox.
        /// </summary>
        /// <param name="filePath">Absolute path to the file on disk.</param>
        /// <param name="dropboxPath">Destination path in Dropbox, including the file name (e.g. /Builds/build.zip).</param>
        /// <param name="accessToken">Long-lived access token (Bearer).</param>
        /// <param name="result">StepResult for logging.</param>
        public static async Task<DropboxUploadResponse> UploadFile(
            string filePath,
            string dropboxPath,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            if (!File.Exists(filePath))
            {
                result?.SetFailed($"File does not exist: {filePath}");
                return new DropboxUploadResponse(false);
            }

            byte[] fileContent;
            try
            {
                fileContent = await IOUtils.ReadAllBytesAsync(filePath);
            }
            catch (Exception e)
            {
                result?.AddException(e);
                result?.SetFailed($"Failed to read file {filePath}: {e.Message}");
                return new DropboxUploadResponse(false);
            }

            // https://www.dropbox.com/developers/documentation/http/documentation#files-upload
            Dictionary<string, object> apiArg = new Dictionary<string, object>
            {
                { "path", dropboxPath },
                { "mode", "overwrite" },
                { "autorename", false },
                { "mute", false },
                { "strict_conflict", false }
            };
            string apiArgJson = JSON.SerializeObject(apiArg);

            const string url = "https://content.dropboxapi.com/2/files/upload";
            using (RequestWrapper www = RequestWrapper.Post(url, "https://content.dropboxapi.com/2/files/upload"))
            {
                // SetOctetStreamData also sets Content-Type to application/octet-stream,
                // which is what Dropbox expects for the upload body.
                www.SetOctetStreamData(fileContent);
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                www.SetRequestHeader("Dropbox-API-Arg", apiArgJson);

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to upload file to Dropbox");
                    return new DropboxUploadResponse(false);
                }

                result?.AddLog(response.Data);

                // {
                //   "name": "build.zip",
                //   "path_display": "/Builds/build.zip",
                //   "id": "id:..."
                // }
                Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                string pathDisplay = "";
                string fileId = "";
                if (responseDict != null)
                {
                    if (responseDict.TryGetValue("path_display", out object pathObj) && pathObj != null)
                    {
                        pathDisplay = pathObj.ToString();
                    }
                    if (responseDict.TryGetValue("id", out object idObj) && idObj != null)
                    {
                        fileId = idObj.ToString();
                    }
                }

                result?.AddLog($"Upload Successful: {pathDisplay} (id: {fileId})");
                return new DropboxUploadResponse(true, pathDisplay, fileId);
            }
        }

        /// <summary>
        /// Create (or retrieve the existing) shared link for a file already uploaded to Dropbox.
        /// </summary>
        /// <param name="dropboxPath">Path of the file in Dropbox (e.g. /Builds/build.zip).</param>
        /// <param name="accessToken">Long-lived access token (Bearer).</param>
        /// <param name="result">StepResult for logging.</param>
        public static async Task<DropboxSharedLinkResponse> CreateSharedLink(
            string dropboxPath,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            // https://www.dropbox.com/developers/documentation/http/documentation#sharing-create_shared_link_with_settings
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "path", dropboxPath },
                {
                    "settings", new Dictionary<string, object>
                    {
                        { "requested_visibility", "public" },
                        { "audience", "public" },
                        { "access", "viewer" }
                    }
                }
            };

            const string url = "https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings";
            using (RequestWrapper www = RequestWrapper.Post(url, "https://api.dropboxapi.com/2/sharing/create_shared_link_with_settings"))
            {
                www.SetJSONData(body);
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result, true);
                if (response.IsSuccessful)
                {
                    string url2 = ExtractSharedLinkUrl(response.Data);
                    result?.AddLog($"Created shared link: {url2}");
                    return new DropboxSharedLinkResponse(true, url2);
                }

                // A link may already exist (HTTP 409 shared_link_already_exists). Fall back to listing it.
                result?.AddLog("Could not create a new shared link, attempting to fetch an existing one.");
                return await ListSharedLink(dropboxPath, accessToken, result);
            }
        }

        private static async Task<DropboxSharedLinkResponse> ListSharedLink(
            string dropboxPath,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            // https://www.dropbox.com/developers/documentation/http/documentation#sharing-list_shared_links
            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "path", dropboxPath },
                { "direct_only", true }
            };

            const string url = "https://api.dropboxapi.com/2/sharing/list_shared_links";
            using (RequestWrapper www = RequestWrapper.Post(url, "https://api.dropboxapi.com/2/sharing/list_shared_links"))
            {
                www.SetJSONData(body);
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.AddLog("Failed to fetch an existing shared link for the uploaded file.");
                    return new DropboxSharedLinkResponse(false);
                }

                // { "links": [ { "url": "https://www.dropbox.com/s/.../build.zip?dl=0", ... } ] }
                string linkUrl = "";
                Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                if (responseDict != null
                    && responseDict.TryGetValue("links", out object linksObj)
                    && linksObj is List<object> links
                    && links.Count > 0
                    && links[0] is Dictionary<string, object> firstLink
                    && firstLink.TryGetValue("url", out object urlObj) && urlObj != null)
                {
                    linkUrl = urlObj.ToString();
                }

                return new DropboxSharedLinkResponse(!string.IsNullOrEmpty(linkUrl), linkUrl);
            }
        }

        private static string ExtractSharedLinkUrl(string json)
        {
            Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(json);
            if (responseDict != null && responseDict.TryGetValue("url", out object urlObj) && urlObj != null)
            {
                return urlObj.ToString();
            }
            return "";
        }
    }
}
