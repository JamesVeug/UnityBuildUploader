using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
    internal class Itchio
    {
        public static Itchio Instance
        {
            get
            {
                if (m_instance == null)
                {
                    m_instance = new Itchio();
                }

                return m_instance;
            }
        }
        
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("Itchio_Enabled");
            set => ProjectEditorPrefs.SetBool("Itchio_Enabled", value);
        }

        public static string ItchioSDKPath
        {
            get => ProjectEditorPrefs.GetString("Itchio_SDKPath");
            set => ProjectEditorPrefs.SetString("Itchio_SDKPath", value);
        }
        
        public static string ItchioEXEPath
        {
            get => Instance.m_SDKCMDPath;
        }

        public bool IsInitialized => m_initialized;

        private static Itchio m_instance;

        // ItchioCMD fails if you try to run multiple instances of it at the same time.
        // So lock uploading builds to one at a time.
        private static SemaphoreSlim m_lock = new SemaphoreSlim(1);
        
        private Process m_uploadProcess;
        private string m_SDKCMDPath;
        private bool m_initialized;

        private Itchio()
        {
            if (!string.IsNullOrEmpty(ItchioSDKPath))
            {
                Initialize();
            }
        }

        public void Initialize()
        {
            m_initialized = false;
            if (string.IsNullOrEmpty(ItchioSDKPath) || !Directory.Exists(ItchioSDKPath))
            {
                return;
            }

            string exePath;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                exePath = Path.Combine(ItchioSDKPath, "butler.exe");
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                exePath = Path.Combine(ItchioSDKPath, "butler");
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                exePath = Path.Combine(ItchioSDKPath, "butler");
            }
            else
            {
                Debug.LogError("Unsupported platform for Itchio: " + Application.platform);
                return;
            }
            
            if (!File.Exists(exePath))
            {
                Debug.LogError("Could not find bitcher path!");
                return;
            }

            if (!ProcessUtils.EnsureExecutable(exePath, out string error))
            {
                Debug.LogError("[Itchio] " + error);
                return;
            }

            m_SDKCMDPath = exePath;
            m_initialized = true;
        }

        public async Task<bool> Upload(string pathToUpload, string user, string game, List<string> channels, string version, string apiKey, UploadTaskReport.StepResult stepResult, bool dryRun = false)
        {
            stepResult.AddLog("Waiting turn to upload to Itchio....");
            await m_lock.WaitAsync();
            stepResult.AddLog("Uploading to Itchio....");

            try
            {
                string path = m_SDKCMDPath;
                string args = CreateUploadBuildItchioArguments(pathToUpload, user, game, version, channels, dryRun);
                
                Dictionary<string, string> environment = null;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    environment = new Dictionary<string, string> { { "BUTLER_API_KEY", apiKey } };
                }

                ProcessUtils.ProcessResult result = await ProcessUtils.RunTask(stepResult, path, args, environment);
                if (result.IsSuccessful)
                {
                    OutputResultArgs outputParsingResult = LogOutItchioResult(result.Output);
                    if (outputParsingResult.successful)
                    {
                        stepResult.AddLog("[Itchio] Itch.io upload successful!");
                        return true;
                    }
                    else
                    {
                        stepResult.AddError(outputParsingResult.errorText);
                        stepResult.SetFailed("[Itchio] Failed to upload build to itch.io!");
                        return false;
                    }
                }
                else
                {
                    stepResult.SetFailed($"[Itchio] {result.Errors}");
                }
            }
            finally
            {
                m_lock.Release();
            }

            return false;
        }

        /// <summary>
        /// https://itch.io/docs/butler/pushing.html
        /// </summary>
        private string CreateUploadBuildItchioArguments(string pathToUpload, string user, string game, string version, List<string> channels, bool dryRun)
        {
            // push "<pathToUpload>" <user>/<game>:<channel1>-<channel2>-<channel3> --userversion <version>
            string channelArg = string.Join("-", channels.Select(a=>a.ToLower()));
            string arguments = $"push \"{pathToUpload}\" {user}/{game}:{channelArg} --userversion \"{version}\" --json";

            if (dryRun)
            {
                arguments += " --dry-run";
            }

            return arguments;
        }

        
        private readonly string[] failStrings = new string[]
        {
            "missing",
            "error",
            "failed",
            "not found",
            "not recognized",
            "invalid",
            "unauthorized"
        };
        
        private OutputResultArgs LogOutItchioResult(string textDump)
        {
            OutputResultArgs result = new OutputResultArgs();
            if (string.IsNullOrEmpty(textDump))
            {
                result.errorText = "Itchio upload failed: No output from ItchioCMD. Does your username/game_id/channel_id have spaces?";
                result.successful = false;
                return result;
            }
            
            foreach (string failString in failStrings)
            {
                int index = textDump.IndexOf(failString, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    int endLineIndex = textDump.IndexOf('\n', index);
                    if (endLineIndex < 0)
                    {
                        endLineIndex = textDump.Length; // If no newline, take the rest of the string
                    }
                    
                    string errorText = textDump.Substring(index, endLineIndex - index);
                    result.errorText = $"Itchio upload failed: {errorText}";
                    result.successful = false;
                    return result;
                }
            }

            result.successful = true;
            return result;
        }

        public void ShowConsole()
        {
            ProcessUtils.ShowConsole(m_SDKCMDPath, "--help");
        }

        private class OutputResultArgs
        {
            public bool successful;
            public bool retry = false;
            public string errorText;
        }
    }
}
