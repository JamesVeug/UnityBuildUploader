using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Wireframe
{
    /// <summary>
    /// Google Drive REST API v3 wrapper.
    ///
    /// Uploads use the multipart upload form (single round trip, metadata + bytes).
    /// This is suitable for files up to 5GB; larger files should be split or use
    /// a resumable upload (not implemented here yet).
    ///
    /// https://developers.google.com/drive/api/reference/rest/v3/files/create
    /// </summary>
    internal static partial class GoogleDrive
    {
        /// <summary>
        /// Upload a single file to Google Drive.
        /// </summary>
        /// <param name="filePath">Absolute path to the file on disk.</param>
        /// <param name="fileName">Name the file should appear as in Drive.</param>
        /// <param name="parentFolderId">Drive folder ID to upload to. Empty/null uploads to "My Drive" root.</param>
        /// <param name="accessToken">OAuth2 access token (Bearer).</param>
        /// <param name="result">StepResult for logging.</param>
        public static async Task<GoogleDriveUploadResponse> UploadFile(
            string filePath,
            string fileName,
            string parentFolderId,
            string accessToken,
            UploadTaskReport.StepResult result = null)
        {
            if (!File.Exists(filePath))
            {
                result?.SetFailed($"File does not exist: {filePath}");
                return new GoogleDriveUploadResponse(false);
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
                return new GoogleDriveUploadResponse(false);
            }

            // Build metadata
            Dictionary<string, object> metadata = new Dictionary<string, object>
            {
                { "name", fileName }
            };
            if (!string.IsNullOrEmpty(parentFolderId))
            {
                metadata["parents"] = new List<string> { parentFolderId };
            }
            string metadataJson = JSON.SerializeObject(metadata);

            // Build multipart/related body
            // https://developers.google.com/drive/api/guides/manage-uploads#multipart
            string boundary = "buildUploaderBoundary_" + Guid.NewGuid().ToString("N");
            string newLine = "\r\n";
            StringBuilder header = new StringBuilder();
            header.Append("--").Append(boundary).Append(newLine);
            header.Append("Content-Type: application/json; charset=UTF-8").Append(newLine).Append(newLine);
            header.Append(metadataJson).Append(newLine);
            header.Append("--").Append(boundary).Append(newLine);
            header.Append("Content-Type: application/octet-stream").Append(newLine).Append(newLine);
            byte[] headerBytes = Encoding.UTF8.GetBytes(header.ToString());

            string footerString = newLine + "--" + boundary + "--" + newLine;
            byte[] footerBytes = Encoding.UTF8.GetBytes(footerString);

            byte[] body = new byte[headerBytes.Length + fileContent.Length + footerBytes.Length];
            Buffer.BlockCopy(headerBytes, 0, body, 0, headerBytes.Length);
            Buffer.BlockCopy(fileContent, 0, body, headerBytes.Length, fileContent.Length);
            Buffer.BlockCopy(footerBytes, 0, body, headerBytes.Length + fileContent.Length, footerBytes.Length);

            string url = "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,webViewLink";

            using (RequestWrapper www = RequestWrapper.Post(url, "https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,webViewLink"))
            {
                // SetOctetStreamData sets the upload body. We override Content-Type
                // immediately afterwards because Drive requires multipart/related.
                www.SetOctetStreamData(body);
                www.SetRequestHeader("Content-Type", $"multipart/related; boundary={boundary}");
                www.SetRequestHeader("Authorization", $"Bearer {accessToken}");

                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed("Failed to upload file to Google Drive");
                    return new GoogleDriveUploadResponse(false);
                }

                result?.AddLog(response.Data);

                // {
                //   "id": "1A2B3C...",
                //   "name": "myFile.zip",
                //   "webViewLink": "https://drive.google.com/file/d/1A2B3C.../view?usp=drivesdk"
                // }
                Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                string fileId = "";
                string webViewLink = "";
                if (responseDict != null)
                {
                    if (responseDict.TryGetValue("id", out object idObj) && idObj != null)
                    {
                        fileId = idObj.ToString();
                    }
                    if (responseDict.TryGetValue("webViewLink", out object linkObj) && linkObj != null)
                    {
                        webViewLink = linkObj.ToString();
                    }
                }

                result?.AddLog($"Upload Successful: {fileName} (id: {fileId})");
                return new GoogleDriveUploadResponse(true, fileId, webViewLink);
            }
        }
    }
}
