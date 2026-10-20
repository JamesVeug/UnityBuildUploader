using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Dry run against Partner Center: a submission is created and the dummy file uploaded as its package, then the
    /// submission is deleted instead of committed, so nothing reaches certification.
    /// </summary>
    public class XboxUploadDestinationTests
    {
        [UnityTest, Category("BuildUploader.Destinations")]
        public IEnumerator OneDummyFileDryRunSubmission()
        {
            string tenantId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_XBOX_TENANT_ID");
            string clientId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_XBOX_CLIENT_ID");
            string clientSecret = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_XBOX_CLIENT_SECRET");
            string productId = TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_XBOX_PRODUCT_ID");

            var app = new XboxConfig.XboxApp(0, DestinationTestSupport.AppName)
            {
                TenantId = tenantId,
                ClientId = clientId,
                ProductId = productId
            };
            string previousSecret = app.ClientSecret;
            bool previousEnabled = Xbox.Enabled;
            try
            {
                app.ClientSecret = clientSecret;
                Xbox.Enabled = true;

                var destination = new XboxUploadDestination(app);
                destination.SetDryRun(true);
                UploadTask task = DestinationTestSupport.DummyFileTask("Xbox destination test",
                    DestinationTestSupport.DummyFile("Xbox"), destination);
                yield return TestSupport.Execute(task, 600);

                string report = task.Report.GetReport();
                Assert.IsTrue(task.IsSuccessful, report);
                StringAssert.Contains("Xbox: Package uploaded successfully.", report);
                StringAssert.Contains("Xbox: Dry run, deleting submission", report);
                StringAssert.DoesNotContain("Xbox: Committing submission", report);
            }
            finally
            {
                app.ClientSecret = previousSecret;
                Xbox.Enabled = previousEnabled;
            }
        }
    }
}
