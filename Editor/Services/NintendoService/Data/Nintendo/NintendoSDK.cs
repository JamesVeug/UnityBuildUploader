using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
    public partial class NintendoSDK
    {
        public static NintendoSDK Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new NintendoSDK();
                }

                return m_instance;
            }
        }

        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("nintendobuild_Enabled", true);
            set => ProjectEditorPrefs.SetBool("nintendobuild_Enabled", value);
        }

        public static string NintendoSDKPath
        {
            get => ProjectEditorPrefs.GetString("nintendobuild_SDKPath");
            set => ProjectEditorPrefs.SetString("nintendobuild_SDKPath", value);
        }


        private static string UserNameKey => ProjectEditorPrefs.ProjectID + "NintendoUBuildUploader";
        public static string UserName
        {
            get => EncodedEditorPrefs.GetString(UserNameKey, "");
            set => EncodedEditorPrefs.SetString(UserNameKey, value);
        }

        public static string NotificationWebhook
        {
            get => ProjectEditorPrefs.GetString("nintendobuild_NotificationWebhook");
            set => ProjectEditorPrefs.SetString("nintendobuild_NotificationWebhook", value);
        }

        private static string NotificationTokenKey => ProjectEditorPrefs.ProjectID + "NintendoNotificationToken";
        public static string NotificationToken
        {
            get => EncodedEditorPrefs.GetString(NotificationTokenKey, "");
            set => EncodedEditorPrefs.SetString(NotificationTokenKey, value);
        }

        public static string NintendoAuthoringToolPath
        {
            get => Instance.m_authoringToolPath;
        }

        public static string NintendoScriptPath
        {
            get => Instance.m_scriptPath;
        }

        public bool IsInitialized => m_initialized;

        private static NintendoSDK m_instance;

        // Authoring tools fail if multiple instances run at the same time, so lock uploads to one at a time.
        private static SemaphoreSlim m_lock = new SemaphoreSlim(1);

        private string m_scriptPath;
        private string m_authoringToolPath;
        private bool m_initialized;

        private NintendoSDK()
        {
            if (!string.IsNullOrEmpty(NintendoSDKPath))
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            m_initialized = false;
            if (!Directory.Exists(NintendoSDKPath))
            {
                return;
            }

            string toolsRoot = null;
            foreach (string directory in Directory.GetDirectories(NintendoSDKPath, "*", SearchOption.AllDirectories))
            {
                if (directory.EndsWith("Tools"))
                {
                    toolsRoot = directory;
                    break;
                }
            }

            if (string.IsNullOrEmpty(toolsRoot))
            {
                Debug.LogError("Could not find Tools folder in Nintendo SDK path!");
                return;
            }

            string scripts = Path.Combine(NintendoSDKPath, "BuildUploaderScripts");
            if (!Directory.Exists(scripts))
            {
                Directory.CreateDirectory(scripts);
            }

            string exePath;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                exePath = Path.Combine(toolsRoot, "AuthoringTool.exe");
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                exePath = Path.Combine(toolsRoot, "AuthoringTool");
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                exePath = Path.Combine(toolsRoot, "AuthoringTool");
            }
            else
            {
                Debug.LogError("Unsupported platform for Nintendo SDK: " + Application.platform);
                return;
            }

            if (!File.Exists(exePath))
            {
                Debug.LogError("Could not find AuthoringTool inside Nintendo SDK Tools path: " + exePath);
                return;
            }

            if (!ProcessUtils.EnsureExecutable(exePath, out string error))
            {
                Debug.LogError("[Nintendo] " + error);
                return;
            }

            m_authoringToolPath = exePath;
            m_scriptPath = scripts;
            m_initialized = true;
        }

        /// <summary>
        /// Creates the authoring command file used to package and upload the build to the Nintendo Developer Center.
        /// </summary>
        public Task<string> CreateAppFiles(NintendoApp app, NintendoBranch branch, string description,
            string sourceFilePath, UploadTaskReport.StepResult result, string fileSuffix = "")
        {
            string fileName = GetAppScriptOutputPath(app, branch, fileSuffix);

            try
            {
                string contents = string.Join("\n",
                    "# Nintendo Build Uploader command file",
                    $"title_id={app.TitleID}",
                    $"application_id={app.ApplicationID}",
                    $"branch={(branch.name.Equals("none", StringComparison.OrdinalIgnoreCase) ? "" : branch.name)}",
                    $"description={description}",
                    $"source_path={sourceFilePath}");

                string directory = Path.GetDirectoryName(fileName);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fileName, contents);
                result.AddLog("Created Nintendo authoring file: " + fileName);
            }
            catch (Exception e)
            {
                result.SetFailed("Failed to create Nintendo authoring file: " + e.Message);
                return Task.FromResult<string>(null);
            }

            return Task.FromResult(fileName);
        }

        public string GetAppScriptOutputPath(NintendoApp app, NintendoBranch branch, string fileNameSuffix = "")
        {
            string fileName;
            if (branch == null || string.IsNullOrEmpty(branch.name))
            {
                fileName = string.Format("nintendo_build_{0}", app.TitleID);
            }
            else
            {
                fileName = string.Format("nintendo_build_{0}_{1}", app.TitleID, branch.name);
            }

            if (!string.IsNullOrEmpty(fileNameSuffix))
            {
                fileName = fileName + "_" + fileNameSuffix;
            }

            string fullPath = Path.Combine(m_scriptPath, fileName + ".cmd");
            return fullPath;
        }

        public async Task<bool> Upload(NintendoApp app, string appFilePath, UploadTaskReport.StepResult stepResult)
        {
            await m_lock.WaitAsync();

            try
            {
                stepResult.AddLog("[Nintendo] Uploading to Nintendo Developer Center...");
                Stopwatch stopwatch = Stopwatch.StartNew();
                string args = CreateUploadArguments(appFilePath);
                var result = await ProcessUtils.RunTask(stepResult, m_authoringToolPath, args, null, UserName);
                stopwatch.Stop();
                stepResult.AddLog($"[Nintendo] Upload took {stopwatch.ElapsedMilliseconds}ms");
                if (!result.IsSuccessful)
                {
                    return false;
                }

                var outputResults = LogOutNintendoResult(result.Output, app.TitleID);
                if (!outputResults.successful)
                {
                    stepResult.SetFailed("[Nintendo] " + outputResults.errorText);
                }
                else
                {
                    stepResult.AddLog("[Nintendo] Upload successful!");
                }
            }
            catch (Exception e)
            {
                stepResult.AddException(e);
                stepResult.SetFailed("[Nintendo] Could not upload Title ID: " + app.TitleID + "\n" + e.Message);
            }
            finally
            {
                m_lock.Release();
            }

            return stepResult.Successful;
        }

        private string CreateUploadArguments(string appFilePath)
        {
            string username = UserName;
            return string.Format("--user \"{0}\" --command \"{1}\" --upload", username, appFilePath);
        }

        private class OutputResultArgs
        {
            public bool successful;
            public string errorText;
        }

        private OutputResultArgs LogOutNintendoResult(string text, string titleId)
        {
            OutputResultArgs result = new OutputResultArgs();

            if (text.IndexOf("authentication failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("not authorized", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.errorText = "Your computer is not authorised to upload with this Nintendo Developer account.\nCheck the username is correct or open the AuthoringTool manually to authorise it and try again.";
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
                text.Contains("Upload complete"))
            {
                result.successful = true;
                return result;
            }

            if (text.Contains("Fail") || text.Contains("FAILED"))
            {
                result.errorText = "Failed to upload to Nintendo Developer Center. Check logs for info!";
                return result;
            }

            // Default to success if no explicit failure markers were found and the process returned cleanly.
            result.successful = true;
            return result;
        }
    }
}
