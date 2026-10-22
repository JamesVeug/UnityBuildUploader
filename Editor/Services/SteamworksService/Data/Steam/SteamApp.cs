using System;
using System.Collections.Generic;
using System.Linq;

namespace Wireframe
{
    [Serializable]
    public class SteamApp : DropdownElement
    {
        public int Id => ID;
        public string DisplayName => Name;
        
        public int ID;
        public string Name = "Template";
        public AppVDFFile App = new AppVDFFile();
        public List<SteamDepot> Depots = new List<SteamDepot>();
        public List<SteamBranch> ConfigBranches = new List<SteamBranch>();

        // Deprecated in v1.1.3 in favor of ConfigBranches so we can add more fields for a branch
        [Obsolete("Use ConfigBranches instead")]
        public List<string> Branches;
        
        public SteamApp()
        {
            ConfigBranches.Add(new SteamBranch(1, "none"));
        }

        public SteamApp(SteamApp current)
        {
            ID = current.ID;
            Name = current.Name;
            App = new AppVDFFile(current.App);
            
            Depots = new List<SteamDepot>(current.Depots);
            ConfigBranches = new List<SteamBranch>(current.ConfigBranches);
        }

        public SteamApp(AppVDFFile appFile)
        {
            App = new AppVDFFile(appFile);
            
            ConfigBranches.Add(new SteamBranch(1, "none"));
            
            if (!string.IsNullOrEmpty(appFile.setlive) && !appFile.setlive.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                ConfigBranches.Add(new SteamBranch(2, appFile.setlive));
            }
            
            foreach (VdfMap<int, string>.MapData depotData in appFile.depots.GetData())
            {
                DepotVDFFile depotVDF = VDFFile.Load<DepotVDFFile>(depotData.Value);
                if (depotVDF == null)
                {
                    continue;
                }
                
                SteamDepot depot = new SteamDepot(Depots.Count + 1, depotVDF.FileName);
                depot.Depot = depotVDF;
                Depots.Add(depot);
            }
        }

        /// <summary>
        /// Depot problems that block uploads: a missing Depot ID (#116), or two depots that Steam or a saved
        /// upload config cannot tell apart (#115).
        /// </summary>
        public List<string> GetDepotErrors()
        {
            List<string> errors = new List<string>();
            foreach (SteamDepot depot in Depots)
            {
                if (!depot.HasDepotID)
                {
                    errors.Add($"Depot '{depot.Name}' has no Depot ID. Enter the numeric ID from Steamworks.");
                }
            }

            foreach (IGrouping<int, SteamDepot> group in Depots.Where(a => a.HasDepotID).GroupBy(a => a.Depot.DepotID))
            {
                if (group.Count() > 1)
                {
                    errors.Add($"Depots {JoinNames(group)} all use Depot ID {group.Key}. Each depot needs its own ID.");
                }
            }

            foreach (IGrouping<int, SteamDepot> group in Depots.GroupBy(a => a.ID))
            {
                if (group.Count() > 1)
                {
                    errors.Add($"Depots {JoinNames(group)} share internal ID {group.Key}, so upload configs cannot tell them apart. " +
                               "Remove all but one, add them again, then re-select them in your upload configs.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Depot problems that do not block uploads but are probably mistakes.
        /// </summary>
        public List<string> GetDepotWarnings()
        {
            List<string> warnings = new List<string>();
            foreach (IGrouping<string, SteamDepot> group in Depots.GroupBy(a => a.Name ?? ""))
            {
                if (group.Count() > 1)
                {
                    warnings.Add($"{group.Count()} depots are named '{group.Key}'. Popups show their Depot IDs to tell them apart.");
                }
            }

            return warnings;
        }

        private static string JoinNames(IEnumerable<SteamDepot> depots)
        {
            return string.Join(", ", depots.Select(a => $"'{a.Name}'"));
        }
    }

    [Serializable]
    public class SteamDepot : DropdownElement
    {
        public int Id => ID;

        /// <summary>
        /// Name plus Steam depot ID, so depots that share a name stay distinguishable in popups (#115).
        /// </summary>
        public string DisplayName => $"{Name} ({(HasDepotID ? Depot.DepotID.ToString() : "no Depot ID")})";

        public int ID;
        public string Name = "Template";
        public DepotVDFFile Depot = new DepotVDFFile();
        
        public SteamDepot()
        {
            
        }
        
        public SteamDepot(int id, string name)
        {
            ID = id;
            Name = name;
        }
        
        public SteamDepot(SteamDepot currentDepot)
        {
            ID = currentDepot.ID;
            Name = currentDepot.Name;
            Depot = new DepotVDFFile(currentDepot.Depot);
        }

        /// <summary>
        /// False until the user enters the numeric depot ID from Steamworks (#116).
        /// </summary>
        public bool HasDepotID => Depot != null && (Depot.DepotID > 0 || Depot.DepotID == 999999);
    }
}