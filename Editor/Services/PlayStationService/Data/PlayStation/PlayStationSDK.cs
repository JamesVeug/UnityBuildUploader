using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
    public partial class PlayStationSDK
    {
        public static PlayStationSDK Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new PlayStationSDK();
                }

                return m_instance;
            }
        }

        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("playstationbuild_Enabled", true);
            set => ProjectEditorPrefs.SetBool("playstationbuild_Enabled", value);
        }

        public static string PlayStationSDKPath
        {
            get => ProjectEditorPrefs.GetString("playstationbuild_SDKPath");
            set => ProjectEditorPrefs.SetString("playstationbuild_SDKPath", value);
        }

        private static string UserNameKey => ProjectEditorPrefs.ProjectID + "PlayStationBuildUploader";
        public static string UserName
        {
            get => EncodedEditorPrefs.GetString(UserNameKey, "");
            set => EncodedEditorPrefs.SetString(UserNameKey, value);
        }

        private static string PasswordKey => ProjectEditorPrefs.ProjectID + "PlayStationBuildUploaderPassword";
        public static string Password
        {
            get => EncodedEditorPrefs.GetString(PasswordKey, "");
            set => EncodedEditorPrefs.SetString(PasswordKey, value);
        }

        public static string PlayStationPublishingToolPath
        {
            get => Instance.m_publishingToolPath;
        }

        public static string PlayStationScriptPath
        {
            get => Instance.m_scriptPath;
        }

        public bool IsInitialized => m_initialized;

        private static PlayStationSDK m_instance;

        // Publishing tools fail if multiple instances run at the same time, so lock uploads to one at a time.
        private static SemaphoreSlim m_lock = new SemaphoreSlim(1);

        private string m_scriptPath;
        private string m_publishingToolPath;
        private bool m_initialized;

        private PlayStationSDK()
        {
            if (!string.IsNullOrEmpty(PlayStationSDKPath))
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            m_initialized = false;
            if (!Directory.Exists(PlayStationSDKPath))
            {
                return;
            }

            string toolsRoot = null;

            // PS5 (Prospero) and PS4 (Orbis) SDKs both ship the publishing tool under host_tools/bin
            string hostToolsBin = Path.Combine(PlayStationSDKPath, "host_tools", "bin");
            if (Directory.Exists(hostToolsBin))
            {
                toolsRoot = hostToolsBin;
            }
            else
            {
                foreach (string directory in Directory.GetDirectories(PlayStationSDKPath, "*", SearchOption.AllDirectories))
                {
                    string leaf = Path.GetFileName(directory);
                    if (string.Equals(leaf, "bin", StringComparison.OrdinalIgnoreCase))
                    {
                        toolsRoot = directory;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(toolsRoot))
            {
                Debug.LogError("Could not find host_tools/bin folder in PlayStation SDK path!");
                return;
            }

            string scripts = Path.Combine(PlayStationSDKPath, "BuildUploaderScripts");
            if (!Directory.Exists(scripts))
            {
                Directory.CreateDirectory(scripts);
            }

            string exePath = null;
            string[] candidates;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                candidates = new[] { "prospero-pub-cmd.exe", "orbis-pub-cmd.exe" };
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.LinuxEditor)
            {
                candidates = new[] { "prospero-pub-cmd", "orbis-pub-cmd" };
            }
            else
            {
                Debug.LogError("Unsupported platform for PlayStation SDK: " + Application.platform);
                return;
            }

            foreach (string candidate in candidates)
            {
                string path = Path.Combine(toolsRoot, candidate);
                if (File.Exists(path))
                {
                    exePath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(exePath))
            {
                Debug.LogError("Could not find PlayStation publishing tool (prospero-pub-cmd / orbis-pub-cmd) inside: " + toolsRoot);
                return;
            }

            if (!ProcessUtils.EnsureExecutable(exePath, out string error))
            {
                Debug.LogError("[PlayStation] " + error);
                return;
            }

            m_publishingToolPath = exePath;
            m_scriptPath = scripts;
            m_initialized = true;
        }

        /// <summary>
        /// Creates the GP4 / publishing project file used to package and upload the build to PlayStation Partners.
        /// </summary>
        public Task<string> CreateAppFiles(PlayStationApp app, PlayStationBranch branch, string description,
            string sourceFilePath, UploadTaskReport.StepResult result, string fileSuffix = "")
        {
            string fileName = GetAppScriptOutputPath(app, branch, fileSuffix);

            try
            {
                string contents = string.Join("\n",
                    "# PlayStation Build Uploader command file",
                    $"title_id={app.TitleID}",
                    $"content_id={app.ContentID}",
                    $"branch={(branch.name.Equals("none", StringComparison.OrdinalIgnoreCase) ? "" : branch.name)}",
                    $"description={description}",
                    $"source_path={sourceFilePath}");

                string directory = Path.GetDirectoryName(fileName);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fileName, contents);
                result.AddLog("Created PlayStation authoring file: " + fileName);
            }
            catch (Exception e)
            {
                result.SetFailed("Failed to create PlayStation authoring file: " + e.Message);
                return Task.FromResult<string>(null);
            }

            return Task.FromResult(fileName);
        }

        public string GetAppScriptOutputPath(PlayStationApp app, PlayStationBranch branch, string fileNameSuffix = "")
        {
            string fileName;
            if (branch == null || string.IsNullOrEmpty(branch.name))
            {
                fileName = string.Format("playstation_build_{0}", app.TitleID);
            }
            else
            {
                fileName = string.Format("playstation_build_{0}_{1}", app.TitleID, branch.name);
            }

            if (!string.IsNullOrEmpty(fileNameSuffix))
            {
                fileName = fileName + "_" + fileNameSuffix;
            }

            string fullPath = Path.Combine(m_scriptPath, fileName + ".cmd");
            return fullPath;
        }

        public async Task<bool> Upload(PlayStationApp app, PlayStationBranch branch, string appFilePath, UploadTaskReport.StepResult stepResult)
        {
            await m_lock.WaitAsync();

            try
            {
                stepResult.AddLog("[PlayStation] Uploading to PlayStation Partners...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                string args = CreateUploadArguments(app, branch, appFilePath);
                // Pass username + password through hideText so they never leak into the upload log.
                var result = await ProcessUtils.RunTask(stepResult, m_publishingToolPath, args, null, UserName, Password);
                stopwatch.Stop();
                stepResult.AddLog($"[PlayStation] Upload took {stopwatch.ElapsedMilliseconds}ms");
                if (!result.IsSuccessful)
                {
                    return false;
                }

                var outputResults = LogOutPlayStationResult(result.Output, app.TitleID);
                if (!outputResults.successful)
                {
                    stepResult.SetFailed("[PlayStation] " + outputResults.errorText);
                }
                else
                {
                    stepResult.AddLog("[PlayStation] Upload successful!");
                }
            }
            catch (Exception e)
            {
                stepResult.AddException(e);
                stepResult.SetFailed("[PlayStation] Could not upload Title ID: " + app.TitleID + "\n" + e.Message);
            }
            finally
            {
                m_lock.Release();
            }

            return stepResult.Successful;
        }

        private string CreateUploadArguments(PlayStationApp app, PlayStationBranch branch, string appFilePath)
        {
            string username = UserName;
            string password = Password;
            string branchArg = (branch == null || string.IsNullOrEmpty(branch.name) || branch.name.Equals("none", StringComparison.OrdinalIgnoreCase))
                ? ""
                : $" --branch \"{branch.name}\"";

            string credentialArg = string.IsNullOrEmpty(password)
                ? $"--user \"{username}\""
                : $"--user \"{username}\" --password \"{password}\"";

            return $"submit {credentialArg} --content-id \"{app.ContentID}\" --command-file \"{appFilePath}\"{branchArg}";
        }

        private class OutputResultArgs
        {
            public bool successful;
            public string errorText;
        }

        private OutputResultArgs LogOutPlayStationResult(string text, string titleId)
        {
            OutputResultArgs result = new OutputResultArgs();

            if (text.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("not authorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("invalid credentials", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.errorText = "Your computer is not authorised to upload with this PlayStation Partners account.\nCheck the username/password are correct or open the publishing tool manually to authorise it and try again.";
                return result;
            }

            int errorTextStartIndex = text.IndexOf("Error", StringComparison.OrdinalIgnoreCase);
            if (errorTextStartIndex >= 0)
            {
                int errorStartOfLine = text.LastIndexOf('\n', errorTextStartIndex);
                if (errorStartOfLine < 0)
                {
                    errorStartOfLine = 0;
                }

                int errorEndOfLine = text.IndexOf('\n', errorTextStartIndex);
                if (errorEndOfLine < 0)
                {
                    errorEndOfLine = text.Length;
                }

                result.errorText = text.Substring(errorTextStartIndex, errorEndOfLine - errorStartOfLine).Trim();
                return result;
            }

            if (text.Contains($"Successfully uploaded Title {titleId}") ||
                text.Contains("Upload complete") ||
                text.Contains("Submission complete"))
            {
                result.successful = true;
                return result;
            }

            if (text.Contains("Fail") || text.Contains("FAILED"))
            {
                result.errorText = "Failed to upload to PlayStation Partners. Check logs for info!";
                return result;
            }

            // Default to success if no explicit failure markers were found and the process returned cleanly.
            result.successful = true;
            return result;
        }
    }
}
