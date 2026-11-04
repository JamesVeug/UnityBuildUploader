namespace Wireframe
{
    /// <summary>
    /// What happened when SteamSDK.LoginAsync logged into Steam with a password.
    /// </summary>
    public readonly struct SteamLoginResult
    {
        /// <summary>Authorized when the login worked. Unknown when SteamCMD's output didn't say.</summary>
        public readonly AuthStatus Status;

        /// <summary>Steam wants a Steam Guard code with the password, or the one given was wrong.</summary>
        public readonly bool NeedsSteamGuardCode;

        /// <summary>Why the login failed, or a warning when it worked. Never holds the password or code.</summary>
        public readonly string Message;

        public SteamLoginResult(AuthStatus status, bool needsSteamGuardCode, string message)
        {
            Status = status;
            NeedsSteamGuardCode = needsSteamGuardCode;
            Message = message ?? "";
        }
    }
}
