using System;

namespace Wireframe
{
    public partial class SteamUploadDestination
    {
        [Obsolete("Use SteamUploadDestination(int appID, string branchName, params int[] depotIDs) instead.")]
        public SteamUploadDestination(int appID, int depotID, string branchName) : base()
        {
            SetSteamApp(appID);
            AddSteamDepot(depotID);
            SetSteamBranch(branchName);
        }

        [Obsolete("Use SetDryRun(bool dryRun) instead.")]
        public void SetPreviewUpload(bool previewUpload)
        {
            m_dryRun = previewUpload;
        }
    }
}