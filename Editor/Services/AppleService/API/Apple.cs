using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    /// <summary>
    /// App Store Connect REST API client.
    ///
    /// Authenticates with a JWT signed by an App Store Connect API Key (see Apple.JWT.cs).
    ///
    /// The binary IPA upload itself does NOT live here — Apple's REST API does not expose
    /// public binary upload. See Apple.Altool.cs for the xcrun altool subprocess wrapper.
    ///
    /// References:
    ///   https://developer.apple.com/documentation/appstoreconnectapi
    ///   https://developer.apple.com/documentation/appstoreconnectapi/list_apps
    ///   https://developer.apple.com/documentation/appstoreconnectapi/list_builds
    ///   https://developer.apple.com/documentation/appstoreconnectapi/list_all_beta_groups_for_an_app
    ///   https://developer.apple.com/documentation/appstoreconnectapi/add_builds_to_a_beta_group
    /// </summary>
    public static partial class Apple
    {
        public const string ApiBaseUrl = "https://api.appstoreconnect.apple.com";

        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("apple_enabled", false);
            set => ProjectEditorPrefs.SetBool("apple_enabled", value);
        }

        /// <summary>
        /// Returns true when the current Unity Editor is running on macOS. Required for
        /// any upload step because xcrun altool ships with Xcode and is macOS-only.
        /// </summary>
        public static bool IsRunningOnMac => Application.platform == RuntimePlatform.OSXEditor;

        internal static string PlatformToAltoolType(ApplePlatform platform)
        {
            switch (platform)
            {
                case ApplePlatform.iOS:      return "ios";
                case ApplePlatform.tvOS:     return "appletvos";
                case ApplePlatform.macOS:    return "macos";
                case ApplePlatform.visionOS: return "visionos";
                default: return "ios";
            }
        }

        /// <summary>
        /// Polls App Store Connect for the Build resource matching the supplied version
        /// and build number. Apple's processing pipeline can take several minutes between
        /// altool completing and the build appearing in the REST API.
        /// </summary>
        public static async Task<AppleFindBuildResponse> FindBuildByVersion(
            AppleConfig.AppleApiKey apiKey,
            string appStoreConnectAppId,
            string shortVersion,
            string buildNumber,
            int timeoutSeconds,
            UploadTaskReport.StepResult result = null)
        {
            string jwt = GetJWT(apiKey, result);
            if (string.IsNullOrEmpty(jwt))
            {
                return new AppleFindBuildResponse(false);
            }

            // App Store Connect's /v1/builds endpoint filters builds across all of the
            // organization's apps; we narrow to one app + version + buildNumber.
            string url = ApiBaseUrl + "/v1/builds" +
                         "?filter[app]=" + Uri.EscapeDataString(appStoreConnectAppId) +
                         "&filter[preReleaseVersion.version]=" + Uri.EscapeDataString(shortVersion) +
                         "&filter[version]=" + Uri.EscapeDataString(buildNumber) +
                         "&sort=-uploadedDate" +
                         "&limit=1";

            DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            const int pollIntervalMs = 15_000;

            while (DateTime.UtcNow < deadline)
            {
                using (RequestWrapper www = RequestWrapper.Get(url))
                {
                    www.SetRequestHeader("Authorization", "Bearer " + jwt);
                    www.SetRequestHeader("Accept", "application/json");

                    RequestResult response = await www.SendAsync(result);
                    if (response.IsSuccessful)
                    {
                        string buildId = ExtractFirstDataId(response.Data);
                        if (!string.IsNullOrEmpty(buildId))
                        {
                            result?.AddLog($"Found App Store Connect Build {buildId} for version {shortVersion} ({buildNumber})");
                            return new AppleFindBuildResponse(true, buildId);
                        }

                        result?.AddLog($"Build {shortVersion} ({buildNumber}) not yet visible in App Store Connect, retrying in {pollIntervalMs / 1000}s...");
                    }
                    else
                    {
                        // Don't fail-fast on transient errors during polling — Apple's API
                        // occasionally 5xxs during processing.
                        result?.AddLog($"Transient error while polling for build: {response.Data}");
                    }
                }

                await Task.Delay(pollIntervalMs);
            }

            result?.SetFailed($"Timed out waiting for App Store Connect to register build {shortVersion} ({buildNumber}) after {timeoutSeconds}s.");
            return new AppleFindBuildResponse(false);
        }

        /// <summary>
        /// Reads an app to check that the API Key can see it.
        /// GET /v1/apps/{id}
        /// </summary>
        public static async Task<AppleSimpleResponse> GetApp(
            AppleConfig.AppleApiKey apiKey,
            string appStoreConnectAppId,
            UploadTaskReport.StepResult result = null)
        {
            string jwt = GetJWT(apiKey, result);
            if (string.IsNullOrEmpty(jwt))
            {
                result?.SetFailed($"Could not sign an App Store Connect token with API Key '{apiKey?.Name}'.");
                return new AppleSimpleResponse(false);
            }

            string url = $"{ApiBaseUrl}/v1/apps/{Uri.EscapeDataString(appStoreConnectAppId)}";
            using (RequestWrapper www = RequestWrapper.Get(url))
            {
                www.SetRequestHeader("Authorization", "Bearer " + jwt);
                www.SetRequestHeader("Accept", "application/json");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed($"App Store Connect app {appStoreConnectAppId} could not be read with API Key '{apiKey.Name}': {response.Data}");
                    return new AppleSimpleResponse(false);
                }

                // { "data": { "id": "...", "type": "apps", "attributes": { "name": "...", "bundleId": "..." } } }
                string name = "";
                string bundleId = "";
                Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(response.Data);
                if (responseDict != null
                    && responseDict.TryGetValue("data", out object dataObj) && dataObj is Dictionary<string, object> data
                    && data.TryGetValue("attributes", out object attributesObj) && attributesObj is Dictionary<string, object> attributes)
                {
                    name = attributes.TryGetValue("name", out object nameObj) ? nameObj?.ToString() : "";
                    bundleId = attributes.TryGetValue("bundleId", out object bundleObj) ? bundleObj?.ToString() : "";
                }

                result?.AddLog($"Found App Store Connect app {appStoreConnectAppId}: {name} ({bundleId})");
                return new AppleSimpleResponse(true);
            }
        }

        /// <summary>
        /// Associates an existing build with a TestFlight beta group.
        /// POST /v1/betaGroups/{id}/relationships/builds
        /// </summary>
        public static async Task<AppleSimpleResponse> AddBuildToBetaGroup(
            AppleConfig.AppleApiKey apiKey,
            string betaGroupId,
            string buildId,
            UploadTaskReport.StepResult result = null)
        {
            string jwt = GetJWT(apiKey, result);
            if (string.IsNullOrEmpty(jwt))
            {
                return new AppleSimpleResponse(false);
            }

            string url = $"{ApiBaseUrl}/v1/betaGroups/{Uri.EscapeDataString(betaGroupId)}/relationships/builds";

            var body = new Dictionary<string, object>
            {
                {
                    "data", new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "type", "builds" },
                            { "id", buildId }
                        }
                    }
                }
            };

            using (RequestWrapper www = RequestWrapper.Post(url))
            {
                www.SetJSONData(body);
                www.SetRequestHeader("Authorization", "Bearer " + jwt);
                www.SetRequestHeader("Accept", "application/json");

                RequestResult response = await www.SendAsync(result);
                if (!response.IsSuccessful)
                {
                    result?.SetFailed($"Failed to add build {buildId} to beta group {betaGroupId}: {response.Data}");
                    return new AppleSimpleResponse(false);
                }

                result?.AddLog($"Added build {buildId} to beta group {betaGroupId}.");
                return new AppleSimpleResponse(true);
            }
        }

        /// <summary>
        /// Extracts the "id" of the first object in a JSON:API "data" array. App Store
        /// Connect responses use a wrapping envelope:
        /// { "data": [ { "id": "...", "type": "builds", "attributes": {...} } ] }
        /// </summary>
        private static string ExtractFirstDataId(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            Dictionary<string, object> responseDict = JSON.DeserializeObject<Dictionary<string, object>>(json);
            if (responseDict == null || !responseDict.TryGetValue("data", out object dataObj))
            {
                return null;
            }

            List<object> data = dataObj as List<object>;
            if (data == null || data.Count <= 0)
            {
                return null;
            }

            Dictionary<string, object> first = data[0] as Dictionary<string, object>;
            if (first == null)
            {
                return null;
            }
            
            if (first.TryGetValue("id", out object idObj) && idObj != null)
            {
                return idObj.ToString();
            }

            return null;
        }
    }
}
