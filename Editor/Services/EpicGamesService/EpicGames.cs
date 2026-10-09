using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    internal static class EpicGames
    {
        public static bool Enabled
        {
            get => ProjectEditorPrefs.GetBool("epicgames_enabled", false);
            set => ProjectEditorPrefs.SetBool("epicgames_enabled", value);
        }

        public static string SDKPath
        {
            get => EditorPrefs.GetString("epicgames_sdkpath");
            set => EditorPrefs.SetString("epicgames_sdkpath", value);
        }

        public static string CloudPath
        {
            get => ProjectEditorPrefs.GetString("epicgames_cloudpath", Path.Combine("{cacheFolderPath}", "EpicGamesCloud"));
            set => ProjectEditorPrefs.SetString("epicgames_cloudpath", value);
        }

        public static async Task<bool> Upload(
            UploadTaskReport.StepResult result, 
            Context ctx,
            string buildPath,
            string organizationID,
            string productID,
            string artifactID,
            string clientID,
            string cloudDir,
            string buildVersion,
            string appLaunch,
            EpicGamesProduct.SecretTypes secretType,
            string clientSecret,
            string appArgs = "")
        {
            string exePath = GetEXEPath();
            if (string.IsNullOrEmpty(exePath))
            {
                result.SetFailed("Unsupported platform for BuildPatchTool: " + Application.platform);
                return false;
            }

            if (!File.Exists(exePath))
            {
                result.SetFailed("BuildPatchTool not found at path: " + exePath);
                return false;
            }

            if (!ProcessUtils.EnsureExecutable(exePath, out string executableError))
            {
                result.SetFailed(executableError);
                return false;
            }
            
            string formattedBuildVersion = ctx.FormatString(buildVersion).Trim();
            
            string args = "-mode=UploadBinary";
            args += $" -BuildRoot=\"{ctx.FormatString(buildPath)}\"";
            args += $" -OrganizationId=\"{ctx.FormatString(organizationID)}\"";
            args += $" -ProductId=\"{ctx.FormatString(productID)}\"";
            args += $" -ArtifactId=\"{ctx.FormatString(artifactID)}\"";
            args += $" -ClientId=\"{ctx.FormatString(clientID)}\"";
            
            switch (secretType)
            {
                case EpicGamesProduct.SecretTypes.EnvVar:
                    args += $" -ClientSecretEnvVar=\"{ctx.FormatString(clientSecret)}\"";
                    break;
                case EpicGamesProduct.SecretTypes.ClientSecret:
                    args += $" -ClientSecret=\"{ctx.FormatString(clientSecret)}\"";
                    break;
                default:
                    result.SetFailed("Invalid secret type selected: " + secretType);
                    return false;
            }

            args += $" -CloudDir=\"{ctx.FormatString(cloudDir)}\"";
            args += $" -BuildVersion=\"{formattedBuildVersion}\"";
            args += $" -AppLaunch=\"{ctx.FormatString(appLaunch)}\"";
            args += $" -AppArgs=\"{ctx.FormatString(appArgs)}\"";

            ProcessUtils.ProcessResult uploadResult = await ProcessUtils.RunTask(result, exePath, args, null);
            if (!uploadResult.IsSuccessful)
            {
                if (uploadResult.Output.Contains("BuildVersion string should only contain"))
                {
                    int indexOf = uploadResult.Output.IndexOf("BuildVersion string should only contain", StringComparison.Ordinal);
                    int end = uploadResult.Output.IndexOf('\n', indexOf);
                    string errorMessage = uploadResult.Output.Substring(indexOf, end - indexOf) + ": '" + formattedBuildVersion + "'";
                    result.SetFailed(errorMessage);
                    return false;
                }

                if (uploadResult.Output.Contains("Raw={"))
                {
                    int indexOf = uploadResult.Output.IndexOf("Raw={", StringComparison.Ordinal) + 4;
                    int end = uploadResult.Output.IndexOf('\n', indexOf);
                    
                    // {"errorCode":"errors.com.epicgames.account.invalid_client_credentials",
                    // "errorMessage":"Sorry the client credentials you are using are invalid",
                    // "messageVars":[],
                    // "numericErrorCode":18033,
                    // "originatingService":"com.epicgames.account.public",
                    // "intent":"prod",
                    // "error_description":"Sorry the client credentials you are using are invalid",
                    // "error":"invalid_client"}
                    string json = uploadResult.Output.Substring(indexOf, end - indexOf);
                    Dictionary<string, string> errorData = JSON.DeserializeObject<Dictionary<string, string>>(json);
                    if (errorData != null)
                    {
                        string err = "";
                        if (errorData.TryGetValue("error", out string error) && !string.IsNullOrEmpty(error))
                        {
                            err = error;
                        }
                        if (errorData.TryGetValue("numericErrorCode", out string errorCode) && !string.IsNullOrEmpty(error))
                        {
                            err += $" (Code: {errorCode})";
                        }
                        
                        string errorMessage = "";
                        if ((errorData.TryGetValue("errorMessage", out var message) && !string.IsNullOrEmpty(message))
                            || (errorData.TryGetValue("error_description", out message) && !string.IsNullOrEmpty(message)))
                        {
                            errorMessage = message;
                        }

                        string finalError = "";
                        if (!string.IsNullOrEmpty(err))
                        {
                            finalError = err;
                        }

                        if (!string.IsNullOrEmpty(errorMessage))
                        {
                            if (!string.IsNullOrEmpty(finalError))
                            {
                                finalError += ": ";
                            }
                            finalError += errorMessage;
                        }

                        if (!string.IsNullOrEmpty(finalError))
                        {
                            result.SetFailed(finalError);
                            return false;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(uploadResult.Errors))
                {
                    result.SetFailed(uploadResult.Errors);
                }
                else
                {
                    result.SetFailed("Unhandled reason. Check logs for info");
                }

                return false;
            }

            if (uploadResult.Output.Contains("Successfully registered binary in artifact service"))
            {
                return true;
            }
            
            // TODO: Check for errors that we may have missed
            return true;
        }

        public static void ShowConsole()
        {
            ProcessUtils.ShowConsole(GetEXEPath(), "-help");
        }

        public static string GetEXEPath()
        {
            string exePath = "";
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                exePath = Path.Combine(SDKPath, "Engine", "Binaries", "Win64", "BuildPatchTool.exe");
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                exePath = Path.Combine(SDKPath, "Engine", "Binaries", "Mac", "BuildPatchTool");
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                exePath = Path.Combine(SDKPath, "Engine", "Binaries", "Linux", "BuildPatchTool");
            }
            
            return exePath;
        }
    }
}
