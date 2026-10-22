using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Wireframe
{
    public static class Preferences
    {
        internal static readonly string DefaultCacheFolder = Path.Combine(Application.persistentDataPath, "BuildUploader", "CachedBuilds");

        public enum ShowIf
        {
            Always,
            Never,
            Successful,
            Failed,
        }

        public enum WelcomeWindowPopup
        {
            OnStartup,
            WhenUpdated,
            Never,
        }

        internal static readonly string[] WelcomeWindowPopupOptions = { "On startup", "When updated", "Never" };

        internal const string WelcomeWindowPopupTooltip = "When the Welcome window should open on its own.\n\n" +
                                                          "On startup: Every time the Unity Editor opens this project.\n" +
                                                          "When updated: Only after the Build Uploader has been updated to a new version.\n" +
                                                          "Never: Only when opened from Window->Build Uploader->Welcome.";

        public static WelcomeWindowPopup ShowWelcomeWindow
        {
            get => (WelcomeWindowPopup)ProjectEditorPrefs.GetInt("BuildUploader_ShowWelcomeWindow", (int)WelcomeWindowPopup.WhenUpdated);
            set => ProjectEditorPrefs.SetInt("BuildUploader_ShowWelcomeWindow", (int)value);
        }

        public static string LastSeenWelcomeVersion
        {
            get => ProjectEditorPrefs.GetString("BuildUploader_LastSeenWelcomeVersion", "");
            set => ProjectEditorPrefs.SetString("BuildUploader_LastSeenWelcomeVersion", value);
        }

        /// <summary>
        /// EditorPrefs stores the number, so new options go at the end.
        /// </summary>
        public enum UpdateCheck
        {
            Weekly = 0,
            Never = 1,
            Monthly = 2,
        }

        /// <summary>
        /// In the order the popup lists them, with their values in <see cref="UpdateCheckOptionValues"/>.
        /// </summary>
        internal static readonly string[] UpdateCheckOptions = { "Weekly", "Monthly", "Never" };
        internal static readonly int[] UpdateCheckOptionValues = { (int)UpdateCheck.Weekly, (int)UpdateCheck.Monthly, (int)UpdateCheck.Never };

        internal const string UpdateCheckTooltip = "How often to ask GitHub for new versions of the Build Uploader. " +
                                                   "Only the list of releases (or commits, for a git submodule or clone) is downloaded; nothing about your project is sent.\n\n" +
                                                   "Weekly: When Unity opens, check if it's been a week, and show a button in the Build Uploader window when a new version is out.\n" +
                                                   "Monthly: The same, but only if it's been a month.\n" +
                                                   "Never: Only check from Window->Build Uploader->Check for Updates.";

        public static UpdateCheck CheckForUpdates
        {
            get => (UpdateCheck)EditorPrefs.GetInt("BuildUploader_CheckForUpdates", (int)UpdateCheck.Weekly);
            set => EditorPrefs.SetInt("BuildUploader_CheckForUpdates", (int)value);
        }

        /// <summary>
        /// For git checkouts: the newest commit the user chose to skip. Only newer commits show the update button.
        /// </summary>
        public static string SkippedUpdateCommit
        {
            get => EditorPrefs.GetString("BuildUploader_SkippedUpdateCommit", "");
            set => EditorPrefs.SetString("BuildUploader_SkippedUpdateCommit", value);
        }

        /// <summary>
        /// The GitHub branch the cached commits came from ("" for the default branch).
        /// </summary>
        public static string UpdateCommitsBranch
        {
            get => ProjectEditorPrefs.GetString("BuildUploader_UpdateCommitsBranch", "");
            set => ProjectEditorPrefs.SetString("BuildUploader_UpdateCommitsBranch", value);
        }

        /// <summary>
        /// The version the user chose to skip. Only newer versions show the update button.
        /// </summary>
        public static string SkippedUpdateVersion
        {
            get => EditorPrefs.GetString("BuildUploader_SkippedUpdateVersion", "");
            set => EditorPrefs.SetString("BuildUploader_SkippedUpdateVersion", value);
        }

        /// <summary>
        /// Remind Me Later hides the update button until this time.
        /// </summary>
        public static DateTime UpdateRemindAfterUtc
        {
            get => ReadUtcTicks(EditorPrefs.GetString("BuildUploader_UpdateRemindAfter", ""));
            set => EditorPrefs.SetString("BuildUploader_UpdateRemindAfter", value.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The last time this project downloaded the list of releases or commits.
        /// </summary>
        public static DateTime LastUpdateCheckUtc
        {
            get => ReadUtcTicks(ProjectEditorPrefs.GetString("BuildUploader_LastUpdateCheck", ""));
            set => ProjectEditorPrefs.SetString("BuildUploader_LastUpdateCheck", value.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The last time downloading the list of releases or commits failed, so being offline doesn't retry on every script reload.
        /// </summary>
        public static DateTime LastFailedUpdateCheckUtc
        {
            get => ReadUtcTicks(ProjectEditorPrefs.GetString("BuildUploader_LastFailedUpdateCheck", ""));
            set => ProjectEditorPrefs.SetString("BuildUploader_LastFailedUpdateCheck", value.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        private static DateTime ReadUtcTicks(string ticks)
        {
            return long.TryParse(ticks, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) &&
                   value >= DateTime.MinValue.Ticks && value <= DateTime.MaxValue.Ticks
                ? new DateTime(value, DateTimeKind.Utc)
                : DateTime.MinValue;
        }

        public static bool DeleteCacheAfterUpload
        {
            get => EditorPrefs.GetBool("BuildUploader_DeleteCacheAfterBuild", true);
            set => EditorPrefs.SetBool("BuildUploader_DeleteCacheAfterBuild", value);
        }
        
        public static ShowIf ShowConfirmationWindowAfterUpload
        {
            get => (ShowIf)EditorPrefs.GetInt("BuildUploader_ShowConfirmationWindowAfterUpload", (int)ShowIf.Always);
            set => EditorPrefs.SetInt("BuildUploader_ShowConfirmationWindowAfterUpload", (int)value);
        }
        
        public static bool AutoFocusNewUploadTask
        {
            get => EditorPrefs.GetBool("BuildUploader_AutoFocusNewUploadTask", true);
            set => EditorPrefs.SetBool("BuildUploader_AutoFocusNewUploadTask", value);
        }
        
        public static bool DefaultShowFormattedTextToggle
        {
            get => EditorPrefs.GetBool("BuildUploader_DefaultShowFormattedTextToggle", true);
            set => EditorPrefs.SetBool("BuildUploader_DefaultShowFormattedTextToggle", value);
        }
        
        public static bool ToolTipsHideBlackValuesToggle
        {
            get => EditorPrefs.GetBool("BuildUploader_ToolTipsHideBlackValuesToggle", false);
            set => EditorPrefs.SetBool("BuildUploader_ToolTipsHideBlackValuesToggle", value);
        }
        
        public static bool AutoSaveReportToCacheFolder
        {
            get => EditorPrefs.GetBool("BuildUploader_AutoSaveReportToCacheFolder", false);
            set => EditorPrefs.SetBool("BuildUploader_AutoSaveReportToCacheFolder", value);
        }
        
        public static bool AutoSaveUploadConfigsAfterChanges
        {
            get => EditorPrefs.GetBool("BuildUploader_AutoSaveBuildConfigsAfterChanges", true);
            set => EditorPrefs.SetBool("BuildUploader_AutoSaveBuildConfigsAfterChanges", value);
        }
        
        public static bool AutoGenerateMenuItems
        {
            get => EditorPrefs.GetBool("BuildUploader_AutoGenerateMenuItems", false);
            set => EditorPrefs.SetBool("BuildUploader_AutoGenerateMenuItems", value);
        }
        
        public static ShowIf ShowReportAfterUpload
        {
            get => (ShowIf)EditorPrefs.GetInt("BuildUploader_ShowReportAfterUpload", (int)ShowIf.Always);
            set => EditorPrefs.SetInt("BuildUploader_ShowReportAfterUpload", (int)value);
        }
        
        public static bool AutoDecompressZippedSourceFiles
        {
            get => EditorPrefs.GetBool("BuildUploader_AutoDecompressZippedSourceFiles", true);
            set => EditorPrefs.SetBool("BuildUploader_AutoDecompressZippedSourceFiles", value);
        }
        
        public static string DefaultDescriptionFormat
        {
            get => EditorPrefs.GetString("BuildUploader_DefaultDescriptionFormat", "v{version} - ");
            set => EditorPrefs.SetString("BuildUploader_DefaultDescriptionFormat", value);
        }
        
        public static string CacheFolderPath
        {
            get => ProjectEditorPrefs.GetString("BuildUploader_CacheFolderPath", DefaultCacheFolder);
            set => ProjectEditorPrefs.SetString("BuildUploader_CacheFolderPath", value);
        }
        
        public static string RootPath
        {
            get => ProjectEditorPrefs.GetString("BuildUploader_RootPath", "");
            set => ProjectEditorPrefs.SetString("BuildUploader_RootPath", value);
        }

        public static bool UseLocalDestinationIfAvailable
        {
            get => ProjectEditorPrefs.GetBool("BuildUploader_UseLocalDestinationIfAvailable", true);
            set => ProjectEditorPrefs.SetBool("BuildUploader_UseLocalDestinationIfAvailable", value);
        }
    }
}