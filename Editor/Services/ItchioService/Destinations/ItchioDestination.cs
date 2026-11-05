using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Wireframe
{
    [Wiki("Itchio", "destinations", "Upload a file or folder for a game.")]
    [UploadDestination("Itchio")]
    public partial class ItchioDestination : AUploadDestination
    {
        /// <summary>
        /// A dry run runs Butler's validation and upload calculation, but uploads nothing
        /// </summary>
        public override bool SkippedDuringDryRun => false;
        
        [Wiki("User", "Account that owns owns the game.")]
        private ItchioUser m_user;
        
        [Wiki("Game", "ID of the game as seen in the URL.")]
        private ItchioGameData m_game;
        
        [Wiki("Channels", "Which platforms to upload to (lower case). eg: windows,mac,linux,android.")]
        private List<ItchioChannel> m_channels;
        
        [Wiki("Description Format", "What description to appear on Itchio attached to the build. Typically short and listed as the version eg: 'v1.0.0'")]
        private string m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;

        public ItchioDestination() : base()
        {
            // Required for reflection
            m_channels = new List<ItchioChannel>();
        }
        
        public ItchioDestination(string user, string game, string[] channels=null) : this()
        {
            m_user = new ItchioUser(){Name = user};
            m_game = new ItchioGameData(){Name = game};
            SetChannels(channels);
        }
        
        public void SetGame(string user, string game)
        {
            m_user = new ItchioUser(){Name = user};
            m_game = new ItchioGameData(){Name = game};
        }
        
        public void SetChannels(params string[] channels)
        {
            if(channels == null || channels.Length == 0)
            {
                // Default channels if none specified
                m_channels = new List<ItchioChannel>();
            }
            else
            {
                m_channels = channels.Select(c => new ItchioChannel(){Name = c}).ToList();
            }
        }

        public void SetDescriptionFormat(string format)
        {
            m_descriptionFormat = format;
        }

        public override async Task<bool> Upload(UploadTaskReport.StepResult result)
        {
            string filePath = m_context.FormatString(m_taskContentsFolder);
            string user = m_context.FormatString(m_user.Name);
            string game = m_context.FormatString(m_game.Name);
            string apikey = m_context.FormatString(m_user.APIKey);
            string version = m_context.FormatString(m_descriptionFormat);
            List<string> channels = m_channels.ConvertAll((a)=>m_context.FormatString(a.Name));
            
            int processID = ProgressUtils.Start("Itchio", "Uploading to Itchio");
            bool success = await Itchio.Instance.Upload(filePath, user, game,channels, version, apikey, result, m_dryRun);
            ProgressUtils.Remove(processID);
            
            return success;
        }

        public override void TryGetErrors(List<GUIContent> errors)
        {
            base.TryGetErrors(errors);

            ItchioService service = InternalUtils.GetService<ItchioService>();
            if (!service.IsReadyToStartBuild(out GUIContent serviceReason))
            {
                errors.Add(serviceReason);
            }

            if (m_user == null)
            {
                errors.Add(new GUIContent("User not specified"));
            }
            else if (m_user == null || string.IsNullOrEmpty(m_user.Name))
            {
                errors.Add(service.ProjectSettingsLink("User is not set", ""));
            }

            if (m_game == null)
            {
                errors.Add(new GUIContent("Game not specified"));
            }
            else if (string.IsNullOrEmpty(m_game.Name))
            {
                errors.Add(service.ProjectSettingsLink("Game name is not set", ""));
            }

            if (m_channels.Count == 0)
            {
                errors.Add(service.ProjectSettingsLink("No channels specified", ""));
            }

            if (string.IsNullOrEmpty(m_descriptionFormat))
            {
                errors.Add(new GUIContent("Description format is empty"));
            }
        }

        public override Dictionary<string, object> Serialize()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["user"] = m_user?.Id,
                ["game"] = m_game?.Id,
                ["channels"] = m_channels.Select(a=>a.Id).ToList(),
                ["descriptionFormat"] = m_descriptionFormat,
                ["dryRun"] = m_dryRun
            };
            return dict;
        }

        public override void Deserialize(Dictionary<string, object> s)
        {
            ItchioUser[] users = ItchioUIUtils.UserPopup.Values;
            if (s.TryGetValue("user", out object user))
            {
                m_user = users.FirstOrDefault(a=>a.Id == (long)user);
            }
            
            if (s.TryGetValue("game", out object game) && m_user != null)
            {
                m_game = m_user.GameIds.FirstOrDefault(a=>a.ID == (long)game);
            }

            if (s.TryGetValue("channels", out object channels))
            {
                List<ItchioChannel> allChannels = ItchioUIUtils.GetItchioBuildData().Channels;
                List<long> channelNames = ((List<object>)channels).Select(a=>(long)a).ToList();
                m_channels = allChannels.Where(c => channelNames.Contains(c.ID)).ToList();
            }
            
            if (s.TryGetValue("descriptionFormat", out object descriptionFormat))
            {
                m_descriptionFormat = (string)descriptionFormat;
            }
            else
            {
                m_descriptionFormat = Context.TASK_DESCRIPTION_KEY;
            }

            m_dryRun = s.TryGetValue("dryRun", out object dryRun) && dryRun is bool value && value;
        }
    }
}
