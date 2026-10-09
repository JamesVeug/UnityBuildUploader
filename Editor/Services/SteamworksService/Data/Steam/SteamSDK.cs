using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Wireframe
{
	public partial class SteamSDK
	{
		public static SteamSDK Instance
		{
			get
			{
				if (m_instance == null)
				{
					m_instance = new SteamSDK();
				}

				return m_instance;
			}
		}

		public static bool Enabled
		{
			get => ProjectEditorPrefs.GetBool("steambuild_Enabled", true);
			set => ProjectEditorPrefs.SetBool("steambuild_Enabled", value);
		}

		public static string SteamSDKPath
		{
			get => ProjectEditorPrefs.GetString("steambuild_SDKPath");
			set
			{
				if (SteamSDKPath == value)
				{
					return;
				}

				ProjectEditorPrefs.SetString("steambuild_SDKPath", value);

				// A different SDK means a different SteamCMD with its own credential cache.
				InvalidateLoginStatus();
			}
		}

		
		private static string UserNameKey => ProjectEditorPrefs.ProjectID + "SteamUBuildUploader";
		public static string UserName
		{
			get => EncodedEditorPrefs.GetString(UserNameKey, "");
			set
			{
				if (UserName == value)
				{
					return;
				}

				// Clear before writing - the status is stored against the username it was recorded for.
				InvalidateLoginStatus();
				EncodedEditorPrefs.SetString(UserNameKey, value);
			}
		}
		
		public static string SteamSDKEXEPath
		{
			get => Instance.m_steamCMDPath;
		}
		
		public static string SteamScriptPath
		{
			get => Instance.m_scriptPath;
		}

		public bool IsInitialized => m_initialized;

		private static SteamSDK m_instance;

		// SteamCMD fails if you try to run multiple instances of it at the same time.
		// So lock uploading builds to one at a time.
		private static SemaphoreSlim m_lock = new SemaphoreSlim(1);
		
		private string m_scriptPath;
		private string m_steamCMDPath;
		private bool m_initialized;

		private SteamSDK()
		{
			if (!string.IsNullOrEmpty(SteamSDKPath))
			{
				Initialize();
			}
		}

		public void Initialize()
		{
			m_initialized = false;
			if (!Directory.Exists(SteamSDKPath))
			{
				return;
			}

			string contentBuilderPath = null;
			foreach (string directory in Directory.GetDirectories(SteamSDKPath, "*", SearchOption.AllDirectories))
			{
				if (directory.EndsWith("ContentBuilder"))
				{
					contentBuilderPath = directory;
					break;
				}
			}

			if (string.IsNullOrEmpty(contentBuilderPath))
			{
				Debug.LogError("Could not find ContentBuilder in sdk path!");
				return;
			}

			string content = null;
			string scripts = null;
			foreach (string directory in Directory.GetDirectories(contentBuilderPath, "*", SearchOption.AllDirectories))
			{
				if (directory.EndsWith("content"))
				{
					content = directory;
				}
				else if (directory.EndsWith("scripts"))
				{
					scripts = directory;
				}

				if (content != null && scripts != null)
				{
					break;
				}
			}

			if (string.IsNullOrEmpty(content))
			{
				Debug.LogError("Could not find content inside ContentBuilder path!");
				return;
			}

			if (string.IsNullOrEmpty(scripts))
			{
				Debug.LogError("Could not find scripts inside ContentBuilder path!");
				return;
			}

			string exePath;
			if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				exePath = Path.Combine(contentBuilderPath, "builder", "steamcmd.exe");
			}
			else if (Application.platform == RuntimePlatform.OSXEditor)
			{
				exePath = Path.Combine(contentBuilderPath, "builder_osx", "steamcmd.sh");
			}
			else if (Application.platform == RuntimePlatform.LinuxEditor)
			{
				exePath = Path.Combine(contentBuilderPath, "builder_linux", "steamcmd.sh");
			}
			else
			{
				Debug.LogError("Unsupported platform for Steamworks SDK: " + Application.platform);
				return;
			}
			
			
			if (!File.Exists(exePath))
			{
				Debug.LogError("Could not find steamcmd.exe inside ContentBuilder/builder path!");
				return;
			}

			// steamcmd.sh launches the real binary as a child, so that needs the exec bit too.
			List<string> executables = new List<string> { exePath };
			string builderPath = Path.GetDirectoryName(exePath) ?? string.Empty;
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				executables.Add(Path.Combine(builderPath, "steamcmd"));
			}
			else if (Application.platform == RuntimePlatform.LinuxEditor)
			{
				executables.Add(Path.Combine(builderPath, "linux32", "steamcmd"));
			}

			foreach (string executable in executables)
			{
				if (File.Exists(executable) && !ProcessUtils.EnsureExecutable(executable, out string error))
				{
					Debug.LogError("[Steam] " + error);
					return;
				}
			}

			m_steamCMDPath = exePath;
			m_scriptPath = scripts;
			m_initialized = true;
		}

		public async Task<string> CreateAppFiles(AppVDFFile appFile, DepotVDFFile depot, string branch,
			string description, string sourceFilePath, UploadTaskReport.StepResult result, string fileSuffix = "")
		{
			List<DepotVDFFile> depots = new List<DepotVDFFile> { depot };
			return await CreateAppFiles(appFile, depots, branch, description, sourceFilePath, result, fileSuffix);
		}
		
		public async Task<string> CreateAppFiles(AppVDFFile appFile, List<DepotVDFFile> depots, string branch,
			string description, string sourceFilePath, UploadTaskReport.StepResult result, string fileSuffix = "")
		{
			appFile.desc = description;
			appFile.buildoutput = "..\\output\\";
			appFile.contentroot = sourceFilePath;
			appFile.setlive = branch.Equals("none", StringComparison.OrdinalIgnoreCase) ? "" : branch;
			appFile.depots.Clear();
			foreach (DepotVDFFile depot in depots)
			{
				string depotFileName = GetDepotFileName(depot, appFile.setlive, fileSuffix);
				appFile.depots.Add(depot.DepotID, depotFileName);
			}

			string fullPath = GetAppScriptOutputPath(appFile, fileSuffix);
			bool saved = await VDFFile.Save(appFile, fullPath, result);
			if (!saved)
			{
				result.SetFailed("Failed to save app file: " + fullPath);
				return null;
			}
			
			return fullPath;
		}

		public async Task<string> CreateDepotFiles(DepotVDFFile depot, string branchName, UploadTaskReport.StepResult result, string fileSuffix = "")
		{
			depot.FileExclusion = "*.pdb";
			depot.FileMapping = new DepotFileMapping
			{
				LocalPath = ".\\*",
				DepotPath = ".",
				recursive = true
			};

			string fileName = GetDepotFileName(depot, branchName, fileSuffix);
			string fullPath = Path.Combine(m_scriptPath, fileName);
			bool saved = await VDFFile.Save(depot, fullPath, result);
			if (!saved)
			{
				result.SetFailed("Failed to save depot file: " + fullPath);
				return null;
			}
			
			return fullPath;
		}

		public string GetAppScriptOutputPath(AppVDFFile appFile, string fileNameSuffix = "")
		{
			string fileName;
			if (string.IsNullOrEmpty(appFile.setlive))
			{
				fileName = string.Format("app_build_{0}", appFile.appid);
			}
			else
			{
				fileName = string.Format("app_build_{0}_{1}", appFile.appid, appFile.setlive);
			}
			
			if (!string.IsNullOrEmpty(fileNameSuffix))
			{
				fileName = fileName + "_" + fileNameSuffix;
			}

			string fullPath = Path.Combine(m_scriptPath, fileName + ".vdf");
			return fullPath;
		}

		private static string GetDepotFileName(DepotVDFFile depot, string branchName = "", string fileSuffix = "")
		{
			string fileName;
			if (string.IsNullOrEmpty(branchName) || branchName.Equals("none", StringComparison.OrdinalIgnoreCase))
			{
				fileName = string.Format("depot_build_{0}", depot.DepotID);
			}
			else
			{
				fileName = string.Format("depot_build_{0}_{1}", depot.DepotID, branchName);
			}
			
			if (!string.IsNullOrEmpty(fileSuffix))
			{
				fileName = fileName + "_" + fileSuffix;
			}

			return fileName + ".vdf";
		}

		public async Task<bool> Upload(AppVDFFile appFile, string appFilePath, UploadTaskReport.StepResult stepResult)
		{
			await m_lock.WaitAsync();

			try
			{
				bool retry = true;
				while (retry)
				{
					retry = false;
					
					stepResult.AddLog("[Steam] Uploading to Steam...");
					Stopwatch stopwatch = Stopwatch.StartNew();
					string args = CreateUploadBuildSteamArguments(appFilePath, true);
					var result = await ProcessUtils.RunTask(stepResult, m_steamCMDPath, args, null, UserName);
					stopwatch.Stop();
					stepResult.AddLog($"[Steam] Steam upload took {stopwatch.ElapsedMilliseconds}ms");
					if (!result.IsSuccessful)
					{
						return false;
					}

					// Check the logs to see if it uploaded 
					var outputResults = LogOutSteamResult(result.Output, false, appFile.appid);
					if (!outputResults.successful)
					{
						stepResult.SetFailed("[Steam] " + outputResults.errorText);
						retry = outputResults.retry;
					}
					else
					{
						stepResult.AddLog("[Steam] Steam upload successful!");
					}
				}
			}
			catch (Exception e)
			{
				stepResult.AddException(e);
				stepResult.SetFailed("[Steam] Could not upload to app id: " + appFile.appid + "\n" + e.Message);
			}
			finally
			{
				m_lock.Release();
			}

			return stepResult.Successful;
		}

		private string CreateUploadBuildSteamArguments(string appFilePath, bool quitOnComplete)
		{
			string username = UserName;
			string uploadArg = $"+run_app_build \"{appFilePath}\"";
			
			string arguments = string.Format("+login \"{0}\" {1}", username, uploadArg);
			if (quitOnComplete)
			{
				arguments += " +quit";
			}
			
			return arguments;
		}
		
		private string CreateDRMWrapSteamArguments(bool quitOnComplete, string steamGuardCode, int appID, string sourcePath, string destinationPath, int flags)
		{
			string username = UserName;
			string guard = string.IsNullOrEmpty(steamGuardCode) ? "" : " " + steamGuardCode;
			
			string uploadArg = $" +drm_wrap {appID} \"{sourcePath}\" \"{destinationPath}\" drmtoolp {flags}";
			string arguments = $"+login \"{username}\" {guard} {uploadArg}";
			
			if (quitOnComplete)
			{
				arguments += " +quit";
			}
			return arguments;
		}

		private class OutputResultArgs
		{
			public bool successful;
			public bool retry = false;
			public string errorText;
		}
		
		private OutputResultArgs LogOutSteamResult(string text, bool drmWrapping, int appID)
		{
			OutputResultArgs result = new OutputResultArgs();

			if (text.Contains("Invalid Password"))
			{
				CachedStatus = AuthStatus.RequiresLogin;
				result.errorText = "Your computer is not authorized to upload to this Steam username.\nCheck the username is correct or Open the SteamCMD within preferences and login manually to authorize it and try again.";
				return result;
			}
			
			int errorTextStartIndex = text.IndexOf("Error", StringComparison.OrdinalIgnoreCase);
			if (errorTextStartIndex >= 0)
			{
				int errorStartOfLine = text.LastIndexOf('\n', errorTextStartIndex);
				if(errorStartOfLine < 0)
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

			if (text.Contains($"Successfully finished AppID {appID}"))
			{
				CachedStatus = AuthStatus.Authorized;
				result.successful = true;
				return result;
			}

			string[] lines = text.Split('\n');
			int index;
			
#if !UNITY_EDITOR_WIN
			if (!ContainsText(lines, "Waiting for confirmation...API", "OK", out index))
			{
				result.errorText = "Failed to authenticate API. (Couldn't access your account??)";
				return result;
			}
#else 
			if (!ContainsText(lines, "Loading Steam API", "OK", out index) &&
				!ContainsText(lines, "Waiting for confirmation...Loading Steam API", "OK", out index))
			{
				result.errorText = "Failed to load API.";
				return result;
			}
#endif

			if (!ContainsText(lines, "Logging in user", "OK", out index))
			{
				string context = "";
				if (index != -1)
				{
					context = lines[index + 1];
				}

				if (string.IsNullOrEmpty(context))
				{
					result.errorText = "Failed to log into User account: " + context;
				}
				else
				{
					result.errorText = "Failed to log into User account: ";
				}
 
				return result;
			}

			// Getting past the login check is the strongest evidence we have that the machine is authorized.
			CachedStatus = AuthStatus.Authorized;

			if (text.Contains("Rate Limit Exceeded"))
			{
				result.errorText = "You tried logging in too many times. Try again later.";
				return result;
			}

			if (drmWrapping)
			{
				if (!ContainsText(lines, "Module is already DRM protected", "", out index)) // TODO: Separate this into a separate verification check
				{
					if (!ContainsText(lines, "DRM wrap completed", "", out index))
					{
						result.errorText = "Failed to DRM wrap...";
						return result;
					}
				}
			}
			else
			{
				if (!ContainsText(lines, "Uploading content...", "", out index))
				{
					result.errorText = "Failed to scan content to upload...";
					return result;
				}
			}
			
			if (ContainsText(lines, "ERROR! Failed to commit build", "", out index))
			{
				result.errorText = "The build may have uploaded but the settings your provided were incorrect. Check Steamworks if the build is there.\n" +
								   "Possible reasons: Your branch does not exist on Steamworks.";
				return result;
			}
			
			if (text.Contains("Fail") || text.Contains("FAILED"))
			{
				result.errorText = "Failed to upload to steam. Check logs for info!";
				return result;
			}

			result.successful = true;
			return result;
		}

		private bool ContainsText(string[] lines, string startsWith, string endsWith, out int startsWithIndex)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i].Trim();
				if (line.StartsWith(startsWith, StringComparison.Ordinal))
				{
					bool success = string.IsNullOrEmpty(endsWith) || line.EndsWith(endsWith, StringComparison.Ordinal);
					startsWithIndex = i;
					return success;
				}
			}

			startsWithIndex = -1;
			return false;
		}

		public void Authenticate()
		{
			string[] arguments = new[] { "+login", UserName };
			ProcessUtils.ShowConsole(m_steamCMDPath, arguments);
		}

		public void ShowConsole()
		{
			ProcessUtils.ShowConsole(m_steamCMDPath, new string[0]);
		}
		
		/// <summary>
		/// The Steam DRM wrapper protects against extremely casual piracy (i.e. copying all game files to another computer) and has some obfuscation, but it is easily removed by a motivated attacker.
		/// https://partner.steamgames.com/doc/features/drm#other_drm
		///
		/// Works by uploading the .exe of your game and applies the DRM then writes it back to the output path.
		/// </summary>
		/// <returns></returns>
		public async Task<bool> DRMWrap(int appID, string sourceExe, string resultEXE, int flags, UploadTaskReport.StepResult stepResult)
		{
			// Get name of sourceExe
			string sourceExeName = Path.GetFileName(sourceExe);
			stepResult.AddLog($"[Steam] Attempting DRMWrap {sourceExeName}. If you're using Steam Guard or Two-factor Authenticator check your phone!\n\n");
			try
			{
				bool retry = true;
				string steamGuardCode = "";
				while (retry)
				{
					retry = false;
					
					stepResult.AddLog("[Steam] DRM'ing on Steam...");
					Stopwatch stopwatch = Stopwatch.StartNew();
					string args = CreateDRMWrapSteamArguments(true, steamGuardCode, appID, sourceExe, resultEXE, flags);
					var result = await ProcessUtils.RunTask(stepResult, m_steamCMDPath, args, null, UserName);
					stopwatch.Stop();
					stepResult.AddLog($"[Steam] Steam DRM took {stopwatch.ElapsedMilliseconds}ms");

					if (!result.IsSuccessful)
					{
						return false;
					}

					var outputResults = LogOutSteamResult(result.Output, true, appID);
					if (!outputResults.successful)
					{
						stepResult.AddError("[Steam] " + outputResults.errorText);
						retry = outputResults.retry;

						if (!retry)
						{
							stepResult.SetFailed(stepResult.Logs[stepResult.Logs.Count - 1].Message);
						}
					}
					else
					{
						stepResult.AddLog("[Steam] Steam DRMWrap successful!");
					}
				}
			}
			catch (Exception e)
			{
				stepResult.AddException(e);
				stepResult.SetFailed(e.Message);
			}

			return stepResult.Successful;
		}
		
		/// <summary>
		/// Tries loading the app file at appPath, copies it to a new unique name and modifies the "desc" field with the given description.
		/// </summary>
		/// <returns></returns>
		public static bool TryCopyAppFileAndModifyDescAtPath(string appPath, out string newAppPath, string description, UploadTaskReport.StepResult result)
		{
			newAppPath = "";
			
			// Copy the file at appPath to a unique name, modify the "desc" field with what we need
			if (!File.Exists(appPath))
			{
				result.SetFailed("App file does not exist: " + appPath);
				return false;
			}
			
			string allText = File.ReadAllText(appPath);
			int desc = allText.IndexOf("\"desc\"", StringComparison.OrdinalIgnoreCase);
			if (desc < 0)
			{
				result.SetFailed("App file does not contain a 'desc' field: " + appPath);
				return false;
			}
			
			try
			{
				int endOfLine = allText.IndexOf('\n', desc);
				if (endOfLine < 0)
				{
					endOfLine = allText.Length;
				}
				
				string after = allText.Substring(endOfLine);
				string newDesc = $" \"{description}\"";
				string newText = allText.Substring(0, desc + 6) + newDesc + after;

				string directoryName = Path.GetDirectoryName(appPath) ?? string.Empty;
				string newPath = Path.Combine(directoryName, Path.GetFileNameWithoutExtension(appPath) + "_modified" + ".vdf");
				File.WriteAllText(newPath, newText);
				result.AddLog("Copied and modified app file to: " + newPath);
				newAppPath = newPath;
			}
			catch (Exception e)
			{
				result.SetFailed("Failed to copy and modify app file: " + e.Message);
				return false;
			}

			return true;
		}
		
		/// <summary>
		/// Looks for Readme.txt in Steam SDK path and tries to find the version number.
		/// The version number is listed in the Readme like so:
		/// ----------------------------------------------------------------
		/// v1.59 9th February 2024
		/// ----------------------------------------------------------------
		/// </summary>
		/// <param name="version">Version we currently have otherwise an error message why we couldn't get it.</param>
		/// <returns>True if we retrievwed it</returns>
		public static bool TryCurrentVersion(out string version)
		{
			// Find Readme.txt in SteamSDKPath
			FileInfo[] files = new DirectoryInfo(SteamSDKPath).GetFiles("Readme.txt", SearchOption.AllDirectories);
			if (files.Length == 0)
			{
				version = $"Could not find Readme.txt in Steam SDK path. ({SteamSDKPath})";
				return false;
			}

			foreach (FileInfo file in files)
			{
				// Read and check for "Welcome to the Steamworks SDK" line
				string[] lines = File.ReadAllLines(file.FullName);
				foreach (string line in lines)
				{
					if (line.Contains("Welcome to the Steamworks SDK"))
					{
						for (var i = 0; i < lines.Length; i++)
						{
							var line2 = lines[i];
							if (line2.StartsWith("----------------------------------------------------------------"))
							{
								version = lines[i + 1].Split(' ')[0];
								return true ;
							}
						}

						version = "Found the right Readme.mexe but unable to find version in Readme.txt";
						return false;
					}
				}
			}
			
			version = "Could not find a Readme.txt that has \"Welcome to the Steamworks SDK\" in it.";
			return false;
		}
	}
}