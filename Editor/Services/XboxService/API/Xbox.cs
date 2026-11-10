using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Microsoft Store Submission API v2 wrapper for Xbox / PC game packages.
    /// https://docs.microsoft.com/en-us/windows/uwp/monetize/create-and-manage-submissions-using-windows-store-services
    /// </summary>
    public static class Xbox
    {
        private const string TokenEndpoint = "https://login.microsoftonline.com/{0}/oauth2/token";
        private const string ApiBase = "https://manage.devcenter.microsoft.com/v1.0/my/applications";

        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("xbox_enabled", false);
            set => ProjectEditorPrefs.SetBool("xbox_enabled", value);
        }

        /// <summary>
        /// Exchange client credentials for an Azure AD access token.
        /// POST https://login.microsoftonline.com/{tenantId}/oauth2/token
        /// </summary>
        public static async Task<string> GetAccessToken(
            string tenantId,
            string clientId,
            string clientSecret,
            UploadTaskReport.StepResult result = null)
        {
            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                result?.SetFailed("Xbox: Tenant ID, Client ID, or Client Secret is empty.");
                return null;
            }

            string url = string.Format(TokenEndpoint, Uri.EscapeDataString(tenantId));
            string formBody =
                "grant_type=client_credentials" +
                "&client_id=" + Uri.EscapeDataString(clientId) +
                "&client_secret=" + Uri.EscapeDataString(clientSecret) +
                "&resource=https%3A%2F%2Fmanage.devcenter.microsoft.com";

            using (RequestWrapper www = RequestWrapper.Post(url, "https://login.microsoftonline.com/xxx/oauth2/token"))
            {
                www.SetFormData(formBody);

                RequestResult response = await www.SendAsync(result, false);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Xbox: Failed to acquire access token.");
                    return null;
                }

                Dictionary<string, string> json = JSON.DeserializeObject<Dictionary<string, string>>(response.Data);
                if (json == null || !json.TryGetValue("access_token", out string token) ||
                    string.IsNullOrEmpty(token))
                {
                    result?.SetFailed("Xbox: Access token not found in response.");
                    return null;
                }

                result?.AddLog("Xbox: Access token acquired.");
                return token;
            }
        }

        /// <summary>
        /// Returns the pending submission ID for the app, or null if none exists.
        /// GET https://manage.devcenter.microsoft.com/v1.0/my/applications/{productId}
        /// </summary>
        public static async Task<string> GetPendingSubmissionId(
            string productId,
            string token,
            UploadTaskReport.StepResult result = null)
        {
            string url = $"{ApiBase}/{productId}";

            using (RequestWrapper www = RequestWrapper.Get(url, $"{ApiBase}/xxx"))
            {
                www.SetRequestHeader("Authorization", $"Bearer {token}");

                RequestResult response = await www.SendAsync(result, false);
                if (!response.IsSuccessful)
                {
                    result?.AddLog("Xbox: Could not retrieve app info — assuming no pending submission.");
                    return null;
                }

                // Response shape: { "pendingApplicationSubmission": { "id": "...", ... }, ... }
                // Use a nested dictionary approach via a helper struct.
                var appInfo = JSON.DeserializeObject<XboxAppInfoResponse>(response.Data);
                return appInfo?.pendingApplicationSubmission?.id;
            }
        }

        /// <summary>
        /// Deletes a pending submission so a fresh one can be created.
        /// DELETE https://manage.devcenter.microsoft.com/v1.0/my/applications/{productId}/submissions/{submissionId}
        /// </summary>
        public static async Task<bool> DeletePendingSubmission(
            string productId,
            string submissionId,
            string token,
            UploadTaskReport.StepResult result = null)
        {
            string url = $"{ApiBase}/{productId}/submissions/{submissionId}";

            using (RequestWrapper www = RequestWrapper.Delete(url, $"{ApiBase}/xxx/submissions/xxx"))
            {
                www.SetRequestHeader("Authorization", $"Bearer {token}");

                RequestResult response = await www.SendAsync(result, false);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed($"Xbox: Failed to delete pending submission {submissionId}.");
                    return false;
                }

                result?.AddLog($"Xbox: Pending submission {submissionId} deleted.");
                return true;
            }
        }

        /// <summary>
        /// Creates a new submission for the app.
        /// POST https://manage.devcenter.microsoft.com/v1.0/my/applications/{productId}/submissions
        /// Returns (submissionId, fileUploadUrl).
        /// </summary>
        public static async Task<XboxCreateSubmissionResponse> CreateSubmission(
            string productId,
            string token,
            UploadTaskReport.StepResult result = null)
        {
            string url = $"{ApiBase}/{productId}/submissions";

            using (RequestWrapper www = RequestWrapper.Post(url, $"{ApiBase}/xxx/submissions"))
            {
                www.SetRequestHeader("Authorization", $"Bearer {token}");
                // Body can be empty — the API clones the last published submission
                www.SetJSONData("{}");

                RequestResult response = await www.SendAsync(result, false);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Xbox: Failed to create submission.");
                    return new XboxCreateSubmissionResponse(false);
                }

                var submission = JSON.DeserializeObject<XboxSubmissionData>(response.Data);
                if (submission == null || string.IsNullOrEmpty(submission.id))
                {
                    result?.SetFailed("Xbox: Submission ID not found in create response.");
                    return new XboxCreateSubmissionResponse(false);
                }

                result?.AddLog($"Xbox: Submission created: {submission.id}");
                return new XboxCreateSubmissionResponse(true, submission.id, submission.fileUploadUrl);
            }
        }

        /// <summary>
        /// Uploads the package zip to the Azure Blob SAS URL returned by CreateSubmission.
        /// PUT {fileUploadUrl}  Content-Type: application/x-zip
        /// </summary>
        public static async Task<bool> UploadPackage(
            string fileUploadUrl,
            string zipFilePath,
            UploadTaskReport.StepResult result = null)
        {
            if (!File.Exists(zipFilePath))
            {
                result?.SetFailed($"Xbox: Package file not found: {zipFilePath}");
                return false;
            }

            result?.AddLog($"Xbox: Reading package from {zipFilePath}...");
            byte[] zipBytes = File.ReadAllBytes(zipFilePath);

            using (RequestWrapper www = RequestWrapper.Put(fileUploadUrl, "https://xxx.blob.core.windows.net/xxx?xxx"))
            {
                www.SetOctetStreamData(zipBytes);
                www.SetRequestHeader("x-ms-blob-type", "BlockBlob");
                www.SetRequestHeader("Content-Type", "application/x-zip");

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Xbox: Failed to upload package to Azure Blob Storage.");
                    return false;
                }

                result?.AddLog("Xbox: Package uploaded successfully.");
                return true;
            }
        }

        /// <summary>
        /// Commits a submission, triggering certification.
        /// POST https://manage.devcenter.microsoft.com/v1.0/my/applications/{productId}/submissions/{submissionId}/commit
        /// </summary>
        public static async Task<bool> CommitSubmission(
            string productId,
            string submissionId,
            string token,
            UploadTaskReport.StepResult result = null)
        {
            string url = $"{ApiBase}/{productId}/submissions/{submissionId}/commit";

            using (RequestWrapper www = RequestWrapper.Post(url, $"{ApiBase}/xxx/submissions/xxx/commit"))
            {
                www.SetRequestHeader("Authorization", $"Bearer {token}");
                www.SetJSONData("{}");

                RequestResult response = await www.SendAsync(result, false);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Xbox: Failed to commit submission.");
                    return false;
                }

                result?.AddLog("Xbox: Submission committed. Certification in progress.");
                return true;
            }
        }

        /// <summary>
        /// Polls submission status until it leaves "CommitStarted" or a timeout is reached.
        /// Returns the final status string.
        /// </summary>
        public static async Task<string> PollSubmissionStatus(
            string productId,
            string submissionId,
            string token,
            UploadTaskReport.StepResult result = null,
            int maxRetries = 30,
            int delaySeconds = 10)
        {
            string url = $"{ApiBase}/{productId}/submissions/{submissionId}/status";

            for (int i = 0; i < maxRetries; i++)
            {
                using (RequestWrapper www = RequestWrapper.Get(url, $"{ApiBase}/xxx/submissions/xxx/status"))
                {
                    www.SetRequestHeader("Authorization", $"Bearer {token}");

                    RequestResult response = await www.SendAsync(result, false);
                    if (!response.IsSuccessful)
                    {
                        result?.AddLog($"Xbox: Status poll failed (attempt {i + 1}). Retrying...");
                    }
                    else
                    {
                        var statusData = JSON.DeserializeObject<Dictionary<string, string>>(response.Data);
                        if (statusData != null && statusData.TryGetValue("status", out string status))
                        {
                            result?.AddLog($"Xbox: Submission status = {status}");
                            if (status != "CommitStarted")
                                return status;
                        }
                    }
                }

                await Task.Delay(delaySeconds * 1000);
            }

            result?.AddLog("Xbox: Timed out waiting for submission status to leave CommitStarted.");
            return "Unknown";
        }

        [Serializable]
        private class XboxAppInfoResponse
        {
            public XboxPendingSubmission pendingApplicationSubmission;
        }

        [Serializable]
        private class XboxPendingSubmission
        {
            public string id;
        }

        [Serializable]
        private class XboxSubmissionData
        {
            public string id;
            public string fileUploadUrl;
        }
    }

    public struct XboxCreateSubmissionResponse
    {
        public bool Successful;
        public string SubmissionId;
        public string FileUploadUrl;

        public XboxCreateSubmissionResponse(bool successful, string submissionId = "", string fileUploadUrl = "")
        {
            Successful    = successful;
            SubmissionId  = submissionId;
            FileUploadUrl = fileUploadUrl;
        }
    }
}
