using System.Collections.Generic;
using System.IO;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// A Steamworks SDK folder that SteamSDK.Initialize accepts, without the real SteamCMD.
    /// On macOS and Linux steamcmd.sh is a script that records the HOME it ran with and prints FakeOutput.
    /// On Windows steamcmd.exe is an empty placeholder and cannot be run.
    /// </summary>
    public class FakeSteamSdk
    {
        public string Root { get; private set; }
        public string Builder { get; private set; }
        public string SteamCmd { get; private set; }

        /// <summary>Files SteamSDK.Initialize must make executable.</summary>
        public List<string> Executables { get; } = new List<string>();

        /// <summary>What the fake SteamCMD prints to stdout.</summary>
        public string FakeOutputPath => Path.Combine(Builder, "fake-output.txt");

        /// <summary>Where the fake SteamCMD writes the HOME it was started with.</summary>
        public string HomeSeenPath => Path.Combine(Builder, "home-seen.txt");

        public static FakeSteamSdk Create()
        {
            var sdk = new FakeSteamSdk { Root = TestSupport.OutputDirectory("FakeSteamSdk") };
            string contentBuilder = Path.Combine(sdk.Root, "tools", "ContentBuilder");
            Directory.CreateDirectory(Path.Combine(contentBuilder, "content"));
            Directory.CreateDirectory(Path.Combine(contentBuilder, "scripts"));
#if UNITY_EDITOR_WIN
            sdk.Builder = Path.Combine(contentBuilder, "builder");
            sdk.SteamCmd = Path.Combine(sdk.Builder, "steamcmd.exe");
            Directory.CreateDirectory(sdk.Builder);
            File.WriteAllText(sdk.SteamCmd, "");
            sdk.Executables.Add(sdk.SteamCmd);
#else
#if UNITY_EDITOR_OSX
            sdk.Builder = Path.Combine(contentBuilder, "builder_osx");
            string binary = Path.Combine(sdk.Builder, "steamcmd");
#else
            sdk.Builder = Path.Combine(contentBuilder, "builder_linux");
            string binary = Path.Combine(sdk.Builder, "linux32", "steamcmd");
#endif
            sdk.SteamCmd = Path.Combine(sdk.Builder, "steamcmd.sh");
            Directory.CreateDirectory(Path.GetDirectoryName(binary));
            // Written without the exec bit, as archives and cross-drive copies often leave them.
            File.WriteAllText(sdk.SteamCmd, "#!/bin/sh\ndir=$(dirname \"$0\")\nprintf '%s' \"$HOME\" > \"$dir/home-seen.txt\"\ncat \"$dir/fake-output.txt\"\n");
            File.WriteAllText(binary, "#!/bin/sh\n");
            sdk.Executables.Add(sdk.SteamCmd);
            sdk.Executables.Add(binary);
#endif
            File.WriteAllText(sdk.FakeOutputPath, "");
            return sdk;
        }
    }
}
