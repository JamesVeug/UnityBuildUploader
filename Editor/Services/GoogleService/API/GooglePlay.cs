using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Google Play Developer API v3 wrapper.
    ///
    /// Publishing a build is a four-step transactional flow:
    ///   1. Insert  - POST /edits                                  → returns editId
    ///   2. Upload  - POST /edits/{editId}/{bundles|apks}          → returns versionCode
    ///   3. Track   - PUT  /edits/{editId}/tracks/{track}          → attaches versionCode to a track
    ///   4. Commit  - POST /edits/{editId}:commit                  → publishes the changes
    ///
    /// Authentication uses an OAuth2 access token with the androidpublisher scope.
    /// The token is obtained via OAuth Playground (or a service-account JWT exchange)
    /// and stored in the shared GoogleConfig.GoogleApp.Token field.
    ///
    /// https://developers.google.com/android-publisher/api-ref/rest
    /// </summary>
    internal static partial class GooglePlay
    {
        /// <summary>
        /// Run the full insert → upload → track → commit flow for a single AAB or APK.
        /// </summary>
        /// <param name="binaryPath">Absolute path to the .aab or .apk on disk.</param>
        /// <param name="packageName">The application id (e.g. com.example.MyGame).</param>
        /// <param name="track">Target track: "internal", "alpha", "beta", "production", or a custom closed-testing track name.</param>
        /// <param name="releaseStatus">Track release status: "completed", "draft", "halted", or "inProgress".</param>
        /// <param name="releaseName">Optional user-visible name of the release.</param>
        /// <param name="releaseNotes">Optional release notes, applied to en-US.</param>
        /// <param name="accessToken">OAuth2 access token with the androidpublisher scope.</param>
        /// <param name="result">StepResult for logging.</param>
        public static async Task<GooglePlayUploadResponse> PublishBinary(
            string binaryPath,
            string packageName,
            string track,
            string releaseStatus,
            string releaseName,
            string releaseNotes,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            if (!File.Exists(binaryPath))
            {
                result?.SetFailed($"File does not exist: {binaryPath}");
                return new GooglePlayUploadResponse(false);
            }

            // 1. Create a new edit
            string editId = await InsertEdit(packageName, accessToken, result);
            if (string.IsNullOrEmpty(editId))
            {
                return new GooglePlayUploadResponse(false);
            }

            // 2. Upload the binary
            long versionCode = await UploadBinary(binaryPath, packageName, editId, accessToken, result);
            if (versionCode <= 0)
            {
                return new GooglePlayUploadResponse(false);
            }

            // 3. Attach the version to the requested track
            bool tracked = await AssignToTrack(packageName, editId, track, versionCode, releaseStatus, releaseName, releaseNotes, accessToken, result);
            if (!tracked)
            {
                return new GooglePlayUploadResponse(false);
            }

            // 4. Commit the edit so the changes go live
            bool committed = await CommitEdit(packageName, editId, accessToken, result);
            if (!committed)
            {
                return new GooglePlayUploadResponse(false);
            }

            result?.AddLog($"Google Play publish complete. package={packageName} track={track} versionCode={versionCode}");
            return new GooglePlayUploadResponse(true, versionCode, packageName, track, editId);
        }

        /// <summary>
        /// Check the credentials and package name without changing anything: open an edit, then delete it.
        /// The binary is not uploaded, because Play rejects anything that is not a signed build with a new versionCode.
        /// </summary>
        public static async Task<GooglePlayUploadResponse> DryRun(
            string binaryPath,
            string packageName,
            string track,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            if (!File.Exists(binaryPath))
            {
                result?.SetFailed($"File does not exist: {binaryPath}");
                return new GooglePlayUploadResponse(false);
            }

            string editId = await InsertEdit(packageName, accessToken, result);
            if (string.IsNullOrEmpty(editId))
            {
                return new GooglePlayUploadResponse(false);
            }

            long size = new FileInfo(binaryPath).Length;
            result?.AddLog($"Dry run: would upload {Path.GetFileName(binaryPath)} ({size} bytes) to track '{track}' of {packageName}");

            if (!await DeleteEdit(packageName, editId, accessToken, result))
            {
                return new GooglePlayUploadResponse(false);
            }

            return new GooglePlayUploadResponse(true, 0, packageName, track);
        }

        private static async Task<bool> DeleteEdit(string packageName, string editId, string accessToken, UploadTaskReport.StepResult result)
        {
            string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}";

            using (RequestWrapper www = RequestWrapper.Delete(url))
            {
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed($"Failed to delete Google Play edit {editId}");
                    return false;
                }

                result?.AddLog($"Deleted Google Play edit {editId} without committing it");
                return true;
            }
        }

        private static async Task<string> InsertEdit(string packageName, string accessToken, UploadTaskReport.StepResult result)
        {
            string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits";

            using (RequestWrapper www = RequestWrapper.Post(url))
            {
                // The body must be a valid empty JSON object; the API rejects a zero-length body.
                www.SetJSONData(new Dictionary<string, object>());
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to create Google Play edit");
                    return null;
                }

                result?.AddLog(response.Data);

                // { "id": "12345...", "expiryTimeSeconds": "..." }
                Dictionary<string, object> dict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                if (dict != null && dict.TryGetValue("id", out object idObj) && idObj != null)
                {
                    return idObj.ToString();
                }

                result?.SetFailed("Google Play edit response did not contain an id");
                return null;
            }
        }

        private static async Task<long> UploadBinary(string binaryPath, string packageName, string editId, string accessToken, UploadTaskReport.StepResult result)
        {
            string extension = Path.GetExtension(binaryPath);
            bool isBundle = string.Equals(extension, ".aab", StringComparison.OrdinalIgnoreCase);
            bool isApk = string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase);
            if (!isBundle && !isApk)
            {
                result?.SetFailed($"Google Play uploads must be a .aab or .apk file (got {extension}).");
                return 0;
            }

            string segment = isBundle ? "bundles" : "apks";
            string mimeType = isBundle
                ? "application/octet-stream"
                : "application/vnd.android.package-archive";

            byte[] fileContent;
            try
            {
                fileContent = await IOUtils.ReadAllBytesAsync(binaryPath);
            }
            catch (Exception e)
            {
                result?.AddException(e);
                result?.SetFailed($"Failed to read binary {binaryPath}: {e.Message}");
                return 0;
            }

            string url = $"https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications/{packageName}/edits/{editId}/{segment}?uploadType=media";

            using (RequestWrapper www = RequestWrapper.Post(url))
            {
                // SetOctetStreamData sets both body and content-type; for APKs we override
                // the content-type to the Android package mime so Play recognizes the
                // payload kind.
                www.SetOctetStreamData(fileContent);
                if (!isBundle)
                {
                    www.SetRequestHeader("Content-Type", mimeType);
                }
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to upload binary to Google Play");
                    return 0;
                }

                result?.AddLog(response.Data);

                // { "versionCode": 12345, "binary": { "sha1": "...", "sha256": "..." } }
                Dictionary<string, object> dict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                if (dict != null && dict.TryGetValue("versionCode", out object versionObj) && versionObj != null)
                {
                    if (versionObj is long versionLong)
                    {
                        return versionLong;
                    }
                    if (long.TryParse(versionObj.ToString(), out long parsed))
                    {
                        return parsed;
                    }
                }

                result?.SetFailed("Google Play upload response did not contain a versionCode");
                return 0;
            }
        }

        private static async Task<bool> AssignToTrack(
            string packageName,
            string editId,
            string track,
            long versionCode,
            string releaseStatus,
            string releaseName,
            string releaseNotes,
            string accessToken,
            UploadTaskReport.StepResult result)
        {
            string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}/tracks/{track}";

            Dictionary<string, object> release = new Dictionary<string, object>
            {
                { "versionCodes", new List<string> { versionCode.ToString() } },
                { "status", string.IsNullOrEmpty(releaseStatus) ? "completed" : releaseStatus }
            };

            if (!string.IsNullOrEmpty(releaseName))
            {
                release["name"] = releaseName;
            }

            if (!string.IsNullOrEmpty(releaseNotes))
            {
                release["releaseNotes"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        { "language", "en-US" },
                        { "text", releaseNotes }
                    }
                };
            }

            Dictionary<string, object> body = new Dictionary<string, object>
            {
                { "track", track },
                { "releases", new List<Dictionary<string, object>> { release } }
            };

            using (RequestWrapper www = RequestWrapper.Put(url))
            {
                www.SetJSONData(body);
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed($"Failed to assign versionCode {versionCode} to track '{track}'");
                    return false;
                }

                result?.AddLog(response.Data);
                return true;
            }
        }

        private static async Task<bool> CommitEdit(string packageName, string editId, string accessToken, UploadTaskReport.StepResult result)
        {
            string url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{packageName}/edits/{editId}:commit";

            using (RequestWrapper www = RequestWrapper.Post(url))
            {
                // Commit takes no body.
                www.SetJSONData(new Dictionary<string, object>());
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to commit Google Play edit");
                    return false;
                }

                result?.AddLog(response.Data);
                return true;
            }
        }

        /// <summary>Map the GooglePlayTrack enum to the API track id strings.</summary>
        public static string TrackName(GooglePlayTrack track)
        {
            switch (track)
            {
                case GooglePlayTrack.Internal:   return "internal";
                case GooglePlayTrack.Alpha:      return "alpha";
                case GooglePlayTrack.Beta:       return "beta";
                case GooglePlayTrack.Production: return "production";
                default: return "internal";
            }
        }
    }
}
