using System.Linq;

namespace Wireframe
{
    public partial class SteamUploadDestination
    {
        protected override Context CreateContext()
        {
            Context context = base.CreateContext();
            context.AddCommand(Context.STEAM_APP_NAME_KEY, GetAppName);
            context.AddCommand(Context.STEAM_BRANCH_NAME_KEY, GetBranchName);
            context.AddCommand(Context.STEAM_DEPOT_NAME_KEY, GetDepotNames);
            return context;
        }

        private string GetDepotNames()
        {
            if (m_uploadDepots != null && m_uploadDepots.Count > 0)
            {
                return string.Join(",", m_uploadDepots.Select(a=>a.Name));
            }

            if (m_depots != null)
            {
                return string.Join(",", m_depots.Select(a=>a.Name));
            }

            return "Unspecified Depots";
        }

        private string GetBranchName()
        {
            if (m_uploadBranch != null)
            {
                return m_uploadBranch.DisplayName;
            }

            if (m_destinationBranch != null)
            {
                return m_destinationBranch.DisplayName;
            }

            return "Unspecified Branch";
        }

        private string GetAppName()
        {
            if (m_uploadApp != null)
            {
                return m_uploadApp.DisplayName;
            }

            if (m_app != null)
            {
                return m_app.DisplayName;
            }
            
            return "Unspecified App";
        }
    }
}