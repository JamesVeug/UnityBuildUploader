using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// TODO: Move requests to a wrapper
#pragma warning disable CS0618 // Type or member is obsolete

namespace Wireframe
{
    internal static partial class Github
    {
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("github_enabled", false);
            set => ProjectEditorPrefs.SetBool("github_enabled", value);
        }

        private static string TokenKey => ProjectEditorPrefs.ProjectID + "GithubBuildUploader";
        public static string Token
        {
            get => EncodedEditorPrefs.GetString(TokenKey, "");
            set => EncodedEditorPrefs.SetString(TokenKey, value);
        }

        /// <summary>
        /// https://docs.github.com/en/rest/releases/releases?apiVersion=2022-11-28#create-a-release
        /// </summary>
        public static async Task<bool> NewRelease(string owner,
            string repo,
            string releaseName,
            string releaseBody,
            string tagName,
            string target,
            bool draft,
            bool prerelease,
            string token,
            UploadTaskReport.StepResult result,
            List<string> assets = null)
        {
            // Verify paths first
            if (assets != null)
            {
                foreach (string asset in assets)
                {
                    if (!File.Exists(asset) && !Directory.Exists(asset))
                    {
                        result.AddError($"Path not found: {asset}");
                        result.SetFailed("Path not found: " + asset);
                        return false;
                    }
                }
            }

            string url = $"https://api.github.com/repos/{owner}/{repo}/releases";
            Dictionary<string, object> payload = new Dictionary<string,object>()
            {
                {"tag_name", tagName},
                {"target_commitish", target}, // branch or commit hash/SHA
                {"name", releaseName},
                {"body", releaseBody},
                {"draft", draft},
                {"prerelease", prerelease},
            };
            
            string jsonPayload = JSON.SerializeObject(payload);
            
            using(RequestWrapper www = RequestWrapper.Post(url, "https://api.github.com/repos/xxx/xxx/releases"))
            {
                www.SetJSONData(jsonPayload);
                www.SetRequestHeader("Accept", "application/vnd.github+json");
                www.SetRequestHeader("Authorization", $"Bearer {token}");
                www.SetRequestHeader("X-GitHub-Api-Version", "2022-11-28");
                www.SetRequestHeader("User-Agent", "Unity-GitHub-Client");
                
                RequestResult response = await www.SendAsync(result, true);
                if (!response.IsSuccessful)
                {
                    result.SetFailed("Failed to create release: " + response.Data);
                    return false;
                }

                result.AddLog("Release created successfully.");
                string jsonResponse = response.Data;
                var release = JSON.DeserializeObject<Dictionary<string, object>>(jsonResponse);
                string uploadUrl = release["upload_url"].ToString().Split('{')[0];
                if (assets != null)
                {
                    foreach (string assetPath in assets)
                    {
                        bool uploadAssetSuccess = await UploadReleaseAsset(uploadUrl, token, assetPath, result);
                        if (!uploadAssetSuccess)
                        {
                            result.SetFailed($"Failed to upload release asset: {assetPath} but the release was made. Check Github for the status!");
                            return false;
                        }
                    }
                    result.AddLog("All assets uploaded successfully.");
                }
            }
            
            return true;
        }
        
        private static async Task<bool> UploadReleaseAsset(string uploadUrl, string token, string path, UploadTaskReport.StepResult result)
        {
            try
            {
                string assetName = Path.GetFileName(path);
                byte[] fileContent = null;
                if (File.Exists(path))
                {
                    fileContent = await IOUtils.ReadAllBytesAsync(path);
                }
                else if(Directory.Exists(path))
                {
                    // Zip the directory
                    string zipPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");
                    if (!await ZipUtils.Zip(path, zipPath, result))
                    {
                        return false;
                    }

                    fileContent = await IOUtils.ReadAllBytesAsync(zipPath);
                    assetName += ".zip";
                }
                else
                {
                    result.AddError($"Invalid Path: {path}");
                    return false;
                }

                string url = $"{uploadUrl}?name={assetName}";
                using (RequestWrapper www = RequestWrapper.Post(url, "https://uploads.github.com/repos/xxx/xxx/releases/xxx/assets?name=xxx"))
                {
                    www.SetOctetStreamData(fileContent);
                    www.SetRequestHeader("Accept", "application/vnd.github+json");
                    www.SetRequestHeader("Authorization", $"Bearer {token}");

                    RequestResult response = await www.SendAsync(result);
                    if (!response.IsSuccessful)
                    {
                        result.SetFailed("Failed to upload release asset: " + assetName);
                        return false;
                    }

                    result.AddLog("Asset " + assetName + " uploaded successfully.");
                    return true;
                }
            }
            catch (Exception e)
            {
                result.AddException(e);
                result.SetFailed($"Failed to fully create release: {e.Message}");
                return false;
            }
        }
    }
}