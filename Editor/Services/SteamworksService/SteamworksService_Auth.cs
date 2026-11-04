using System.Threading.Tasks;

namespace Wireframe
{
    internal partial class SteamworksService : IAuthenticatedService
    {
        public string AuthServiceName => "Steam";

        public bool CanAuthenticate => SteamSDK.Enabled &&
                                       SteamSDK.Instance.IsInitialized &&
                                       !string.IsNullOrEmpty(SteamSDK.UserName);

        public string AuthStatusMessage => SteamSDK.LastProbeMessage;

        public AuthStatus GetAuthStatus()
        {
            return SteamSDK.GetDisplayStatus();
        }

        public async Task VerifyAuthAsync()
        {
            await SteamSDK.ProbeLoginAsync();
        }

        public void StartAuthentication()
        {
            // Asks for the password and any Steam Guard code, then SteamCMD logs in with them and saves the login.
            // The CMD button in Preferences still opens SteamCMD's own console for anything this can't answer.
            SteamLoginWindow.ShowWindow();
        }
    }
}
