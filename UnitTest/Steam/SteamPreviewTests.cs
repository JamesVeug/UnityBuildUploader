using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    public class SteamPreviewTests
    {
        [UnityTest, Category("BuildUploader.SteamPreview")]
        public IEnumerator OneSteamPreviewUpload()
        {
            string sdk = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_STEAM_SDK");
            string user = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_STEAM_USER");
            int appId = int.Parse(TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_STEAM_APP_ID"));
            int depotId = int.Parse(TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_STEAM_DEPOT_ID"));
            Assert.IsTrue(Directory.Exists(sdk), "Steamworks SDK directory does not exist: " + sdk);
            Assert.Greater(appId, 0);
            Assert.Greater(depotId, 0);

            string previousSdk = SteamSDK.SteamSDKPath;
            string previousUser = SteamSDK.UserName;
            bool previousEnabled = SteamSDK.Enabled;
            AuthStatus previousStatus = SteamSDK.CachedStatus;
            SteamTestCredentials credentials = null;
            try
            {
                SteamSDK.Enabled = true;
                SteamSDK.SteamSDKPath = sdk;
                SteamSDK.UserName = user;
                SteamSDK.Instance.Initialize();
                Assert.IsTrue(SteamSDK.Instance.IsInitialized, "Steamworks SDK could not be initialized from " + sdk);
                credentials = SteamTestCredentials.FromEnvironment(Path.GetDirectoryName(SteamSDK.SteamSDKEXEPath));

                // Importing credential files does not establish a verified login on
                // this runner. Probe SteamCMD rather than assuming the offline cache
                // check (including legacy sentry files) can authorize the task.
                SteamSDK.InvalidateLoginStatus();
                var login = SteamSDK.ProbeLoginAsync();
                yield return TestSupport.Await(login);
                Assert.AreEqual(AuthStatus.Authorized, login.Result,
                    "SteamCMD login check failed before preview upload: " + SteamSDK.LastProbeMessage);

                var destination = new SteamUploadDestination(appId, "none", depotId);
                destination.SetDescriptionFormat("Build Uploader UploadTask preview");
                destination.SetPreviewUpload(true);
                var errors = new List<UnityEngine.GUIContent>();
                destination.TryGetErrors(errors);
                Assert.IsEmpty(errors, "Steam preview destination is not ready: " +
                    string.Join("\n", errors.Select(error => error.text + ": " + error.tooltip)));
                UploadTask uploadTask = TestSupport.FileUploadTask("Steam UploadTask preview",
                    TestSupport.FixtureDirectory("SteamPreviewSource"), destination);
                yield return TestSupport.Run(uploadTask);
                string report = uploadTask.Report.GetReport();
                StringAssert.Contains("[Steam] Steam upload successful!", report);
                StringAssert.Contains("Successfully finished AppID " + appId, report);
            }
            finally
            {
                if (credentials != null) credentials.Dispose();
                SteamSDK.SteamSDKPath = previousSdk;
                SteamSDK.UserName = previousUser;
                SteamSDK.Enabled = previousEnabled;
                if (!string.IsNullOrEmpty(previousUser)) SteamSDK.CachedStatus = previousStatus;
                SteamSDK.Instance.Initialize();
            }
        }
    }
}
