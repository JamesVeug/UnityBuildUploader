using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wireframe.UnitTest
{
    // SteamCMD authenticates with its saved machine credentials, not an API key.
    internal sealed class SteamTestCredentials : IDisposable
    {
        private readonly Dictionary<string, byte[]> previousFiles = new Dictionary<string, byte[]>();

        public static SteamTestCredentials FromEnvironment(string builderDirectory)
        {
            string json = Environment.GetEnvironmentVariable("BUILDUPLOADER_TEST_STEAM_CREDENTIALS");
            if (string.IsNullOrWhiteSpace(json))
            {
                if (Environment.GetEnvironmentVariable("BUILDUPLOADER_TEST_REQUIRE_CONFIGURATION") == "1")
                    TestSupport.RequiredEnvironment("BUILDUPLOADER_TEST_STEAM_CREDENTIALS");
                return null; // Local editor runs may use an existing SteamCMD login.
            }

            Dictionary<string, string> files;
            try { files = JSON.DeserializeObject<Dictionary<string, string>>(json); }
            catch { throw new AssertionException("BUILDUPLOADER_TEST_STEAM_CREDENTIALS must be a JSON object of base64 file contents."); }
            Assert.IsNotNull(files, "Steam credential JSON must be an object.");
            Assert.IsTrue(files.ContainsKey("config.vdf"), "Steam credentials must include config.vdf.");

            // Validate all input before writing; never put secret values in assertion messages.
            var decoded = new Dictionary<string, byte[]>();
            foreach (var pair in files)
            {
                Assert.IsTrue(pair.Key == "config.vdf" || Regex.IsMatch(pair.Key, @"\Assfn[0-9]+\z"),
                    "Steam credentials may contain only config.vdf and ssfn files.");
                try { decoded.Add(pair.Key, Convert.FromBase64String(pair.Value)); }
                catch { throw new AssertionException("Steam credential file contents must be base64 encoded."); }
            }

            var credentials = new SteamTestCredentials();
            try
            {
                string credentialRoot = builderDirectory;
#if UNITY_EDITOR_OSX
                // macOS SteamCMD stores its login cache in the user's Steam folder,
                // even when the executable is installed in ContentBuilder/builder_osx.
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                Assert.IsFalse(string.IsNullOrEmpty(home), "Cannot locate the macOS user's home directory for SteamCMD credentials.");
                credentialRoot = Path.Combine(home, "Library", "Application Support", "Steam");
#endif
                string config = Path.Combine(credentialRoot, "config");
                Directory.CreateDirectory(config);
                foreach (var pair in decoded)
                {
                    credentials.Write(Path.Combine(config, pair.Key), pair.Value);
                    if (pair.Key != "config.vdf") credentials.Write(Path.Combine(credentialRoot, pair.Key), pair.Value);
                }
                return credentials;
            }
            catch
            {
                credentials.Dispose();
                throw;
            }
        }

        private void Write(string path, byte[] contents)
        {
            previousFiles.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
            File.WriteAllBytes(path, contents);
        }

        public void Dispose()
        {
            foreach (var pair in previousFiles)
            {
                if (pair.Value == null) File.Delete(pair.Key);
                else File.WriteAllBytes(pair.Key, pair.Value);
            }
            previousFiles.Clear();
        }
    }
}
