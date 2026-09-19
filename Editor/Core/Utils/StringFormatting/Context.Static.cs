using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wireframe
{
    public partial class Context
    {
        public const string PRODUCT_NAME_KEY = "{productName}";
        public const string BUNDLE_VERSION_KEY = "{bundleVersion}";
        public const string COMPANY_NAME_KEY = "{companyName}";
        public const string BUILD_TARGET_KEY = "{buildTarget}";
        public const string BUILD_ARCHITECTURE_KEY = "{buildArchitecture}";
        public const string BUILD_TARGET_GROUP_KEY = "{buildTargetGroup}";
        public const string SCRIPTING_BACKEND_KEY = "{scriptingBackend}";
        public const string PROJECT_PATH_KEY = "{projectPath}";
        public const string ROOT_PATH_KEY = "{rootPath}";
        public const string PERSISTENT_DATA_PATH_KEY = "{persistentDataPath}";
        public const string CACHE_FOLDER_KEY = "{cacheFolderPath}";
        public const string UNITY_VERSION_KEY = "{unityVersion}";
        public const string DATE_KEY = "{date}";
        public const string TIME_KEY = "{time}";
        public const string DATE_TIME_KEY = "{dateTime}";
        public const string MACHINE_NAME_KEY = "{machineName}";
        public const string UPLOAD_NUMBER_KEY = "{uploadNumber}";
        
        // Task
        public const string TASK_PROFILE_NAME_KEY = "{taskProfileName}";
        public const string TASK_DESCRIPTION_KEY = "{taskDescription}";
        public const string TASK_FAILED_REASONS_KEY = "{taskFailedReasons}";
        public const string TASK_STATUS_KEY = "{taskStatus}";
        
        // Sources
        public const string BUILD_NAME_KEY = "{buildName}";
        public const string BUILD_NUMBER_KEY = "{buildNumber}";
        
        // Destinations
        public const string DESTINATION_LOCAL_PATH_KEY = "{destLocalPath}";
        public const string STEAM_APP_NAME_KEY = "{steamAppName}";
        public const string STEAM_BRANCH_NAME_KEY = "{steamBranchName}";
        public const string STEAM_DEPOT_NAME_KEY = "{steamDepotName}";
        public const string ITCHIO_USER_NAME_KEY = "{itchioUserName}";
        public const string ITCHIO_GAME_NAME_KEY = "{itchioGameName}";
        public const string ITCHIO_CHANNEL_NAME_KEY = "{itchioChannelName}";
        public const string EPIC_GAMES_ORGANIZATION_NAME_KEY = "{epicgamesOrganizationName}";
        public const string EPIC_GAMES_PRODUCT_NAME_KEY = "{epicgamesProductName}";
        public const string EPIC_GAMES_ARTIFACT_NAME_KEY = "{epicgamesArtifactName}";
        public const string APPLE_APP_NAME_KEY = "{appleAppName}";
        public const string APPLE_BUNDLE_ID_KEY = "{appleBundleId}";
        public const string APPLE_BUILD_ID_KEY = "{appleBuildId}";
        public const string APPLE_BUILD_VERSION_KEY = "{appleBuildVersion}";
        public const string APPLE_BUILD_NUMBER_KEY = "{appleBuildNumber}";
        public const string GOOGLE_DRIVE_APP_NAME_KEY = "{googleDriveAppName}";
        public const string GOOGLE_DRIVE_FOLDER_NAME_KEY = "{googleDriveFolderName}";
        public const string GOOGLE_DRIVE_FOLDER_FILE_ID_KEY = "{googleDriveFileID}";
        public const string GOOGLE_DRIVE_FOLDER_WEB_VIEW_URL_KEY = "{googleDriveWebViewURL}";
        public const string GOOGLE_CHAT_SPACE_NAME_KEY = "{googleChatSpaceName}";
        public const string GOOGLE_PLAY_PACKAGE_NAME_KEY = "{googlePlayPackageName}";
        public const string GOOGLE_PLAY_TRACK_KEY = "{googlePlayTrack}";
        public const string GOOGLE_PLAY_VERSION_CODE_KEY = "{googlePlayVersionCode}";
        public const string GOOGLE_PLAY_EDIT_ID_KEY = "{googlePlayEditId}";
        public const string DROPBOX_APP_NAME_KEY = "{dropboxAppName}";
        public const string DROPBOX_FOLDER_NAME_KEY = "{dropboxFolderName}";
        public const string DROPBOX_SHARE_LINK_KEY = "{dropboxShareLink}";

        // Git
        public const string GIT_BRANCH_KEY = "{gitBranch}";
        public const string GIT_COMMIT_KEY = "{gitCommit}";
        public const string GIT_COMMIT_SHORT_KEY = "{gitCommitShort}";
        public const string GIT_COMMIT_MESSAGE_KEY = "{gitCommitMessage}";
        public const string GIT_COMMIT_MESSAGE_SUBJECT_KEY = "{gitCommitMessageSubject}";
        public const string GIT_COMMIT_MESSAGE_BODY_KEY = "{gitCommitMessageBody}";
        public const string GIT_COMMIT_AUTHOR_KEY = "{gitCommitAuthor}";
        public const string GIT_COMMIT_AUTHOR_EMAIL_KEY = "{gitCommitAuthorEmail}";
        public const string GIT_COMMIT_DATE_KEY = "{gitCommitDate}";
        public const string GIT_TAG_KEY = "{gitTag}";

        // Version
        public const string VERSION_KEY = "{version}";
        public const string VERSION_MAJOR_KEY = "{versionMajor}";
        public const string VERSION_MINOR_KEY = "{versionMinor}";
        public const string VERSION_PATCH_KEY = "{versionPatch}";
        public const string VERSION_REVISION_KEY = "{versionRevision}";
        public const string VERSION_SEM_KEY = "{versionSem}";
        public const string VERSION_MAJOR_SEM_KEY = "{versionMajorSem}";
        public const string VERSION_MINOR_SEM_KEY = "{versionMinorSem}";
        public const string VERSION_PATCH_SEM_KEY = "{versionPatchSem}";
        public const string VERSION_REVISION_SEM_KEY = "{versionRevisionSem}";

        internal static Dictionary<string, Command> FormatToCommand;
        
        static Context()
        {
            FormatToCommand = new Dictionary<string, Command>(StringComparer.InvariantCultureIgnoreCase);
            AddS(PRODUCT_NAME_KEY, ()=> PlayerSettings.productName, "The name of your product as specified in Player Settings.");
            AddS(BUNDLE_VERSION_KEY, ()=> PlayerSettings.bundleVersion, "The version of your project for android/ios as specified in Player Settings.");
            AddS(COMPANY_NAME_KEY, ()=> PlayerSettings.companyName, "The name of your company as specified in Player Settings.");
            AddS(BUILD_TARGET_KEY, ()=> EditorUserBuildSettings.activeBuildTarget.ToString(), "Which platform targeting for the next build as defined in Build Settings.");
            AddS(BUILD_ARCHITECTURE_KEY, ()=> BuildUtils.CurrentTargetArchitecture().ToString(), "Which architecture the build will be (eg x64/x84)");
            AddS(BUILD_TARGET_GROUP_KEY, ()=> BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget).ToString(), "The target group of the upcoming build as defined in Player Settings.");
            AddS(SCRIPTING_BACKEND_KEY, ()=> BuildUtils.ScriptingBackendDisplayName(BuildUtils.CurrentScriptingBackend()), "The scripting backend for the next build as defined in Player Settings.");
            AddS(PROJECT_PATH_KEY, ()=> Path.GetDirectoryName(Application.dataPath), "The path of your Unity Project contains the Assets folder.");
            AddS(ROOT_PATH_KEY, RootPath, "The path every other path can start from as specified in Preferences. eg: C:/SomeFolder/Builds/{version}");
            AddS(PERSISTENT_DATA_PATH_KEY, ()=> Application.persistentDataPath, "The path of the Persistent Data folder");
            AddS(CACHE_FOLDER_KEY, ()=> Preferences.CacheFolderPath, "The path where all files and builds are stored when build uploader is working.");
            AddS(UNITY_VERSION_KEY, ()=> Application.unityVersion, "The version of Unity you are using.");
            AddS(DATE_KEY, ()=> DateTime.Now.ToString("yyyy-MM-dd"), "The current local date in the format YYYY-MM-DD.");
            AddS(TIME_KEY, ()=> DateTime.Now.ToString("HH-mm-ss"), "The current local time in the format HH-MM-SS.");
            AddS(DATE_TIME_KEY, ()=> DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"), "The current local date and time in the format YYYY-MM-DD HH-MM-SS.");
            AddS(MACHINE_NAME_KEY, ()=> Environment.MachineName, "The name of the machine running the build.");
            AddS(UPLOAD_NUMBER_KEY, ()=> (BuildUploaderProjectSettings.Instance.TotalUploadTasksStarted + 1).ToString(), "A unique number of the upload task that's getting sources and uploading them.", true);
            
            // Task
            AddS(TASK_PROFILE_NAME_KEY, null, "The name of the upload profile or task specified when creating the task.");
            AddS(TASK_DESCRIPTION_KEY, null, "The description of the current task being executed.");
            AddS(TASK_FAILED_REASONS_KEY, null, "Gets the reasons why the task failed.");
            AddS(TASK_STATUS_KEY, null, "Get a small message describing the status of the Upload Task.");

            // Sources
            AddS(BUILD_NAME_KEY, null, "The name of the build as specified in a build config.");
            AddS(BUILD_NUMBER_KEY, null, "A unique number of the build that is produced.", true);

            // Destinations
            AddS(DESTINATION_LOCAL_PATH_KEY, null, "The path which files will be copied to using the LocalPath destination.");
            AddS(STEAM_APP_NAME_KEY, null, "The name of the app that is being uploaded to on Steamworks.");
            AddS(STEAM_BRANCH_NAME_KEY, null, "The name of the branch that we are uploading to on Steamworks.");
            AddS(STEAM_DEPOT_NAME_KEY, null, "The name of the depots that are being uploaded to on Steamworks.");
            AddS(ITCHIO_USER_NAME_KEY, null, "The name of the user that owns the game that we want to upload the files to on Itchio.");
            AddS(ITCHIO_GAME_NAME_KEY, null, "The name of the game that is being uploaded to on Itchio.");
            AddS(ITCHIO_CHANNEL_NAME_KEY, null, "The channels/platforms that we are uploading to on Itchio.");
            AddS(EPIC_GAMES_ORGANIZATION_NAME_KEY, null, "The name of the organization that owns the product on Epic Games that will receive the files.");
            AddS(EPIC_GAMES_PRODUCT_NAME_KEY, null, "The name of the product that is being uploaded to on Epic Games.");
            AddS(EPIC_GAMES_ARTIFACT_NAME_KEY, null, "The name of the artifact that is being uploaded to on Epic Games.");
            AddS(APPLE_APP_NAME_KEY, null, "The name of the App Store Connect app receiving the upload.");
            AddS(APPLE_BUNDLE_ID_KEY, null, "The Bundle ID of the App Store Connect app receiving the upload.");
            AddS(APPLE_BUILD_ID_KEY, null, "The App Store Connect Build resource ID returned after a TestFlight upload completes.");
            AddS(APPLE_BUILD_VERSION_KEY, null, "The CFBundleShortVersionString (marketing version) of the most recent Apple upload.");
            AddS(APPLE_BUILD_NUMBER_KEY, null, "The CFBundleVersion (build number) of the most recent Apple upload.");
            AddS(GOOGLE_DRIVE_APP_NAME_KEY, null, "The name of the Google App used to authenticate with Google Drive.");
            AddS(GOOGLE_DRIVE_FOLDER_NAME_KEY, null, "The name of the Google Drive folder receiving the upload.");
            AddS(GOOGLE_DRIVE_FOLDER_FILE_ID_KEY, null, "ID of the last file that was uploaded.");
            AddS(GOOGLE_DRIVE_FOLDER_WEB_VIEW_URL_KEY, null, "A URL to the last file that was uploaded.");
            AddS(GOOGLE_CHAT_SPACE_NAME_KEY, null, "The name of the Google Chat space receiving the message.");
            AddS(GOOGLE_PLAY_PACKAGE_NAME_KEY, null, "The application package name (e.g. com.example.MyGame) of the most recent Google Play upload.");
            AddS(GOOGLE_PLAY_TRACK_KEY, null, "The release track (internal/alpha/beta/production) of the most recent Google Play upload.");
            AddS(GOOGLE_PLAY_VERSION_CODE_KEY, null, "The versionCode returned by Google Play after a successful binary upload.");
            AddS(GOOGLE_PLAY_EDIT_ID_KEY, null, "The Play Console edit id used by the most recent Google Play upload (already committed).");
            AddS(DROPBOX_APP_NAME_KEY, null, "The name of the Dropbox App used to authenticate the upload.");
            AddS(DROPBOX_FOLDER_NAME_KEY, null, "The name of the Dropbox folder receiving the upload.");
            AddS(DROPBOX_SHARE_LINK_KEY, null, "The public shared link created for the most recent Dropbox upload (if enabled).");

            // Git
            AddS(GIT_BRANCH_KEY, ()=> GitValue(a => a.Branch), "The git branch the project is currently on. eg: main. Reads HEAD when the repository is in a detached head state.", true);
            AddS(GIT_COMMIT_KEY, ()=> GitValue(a => a.Commit), "The full hash of the commit the project is currently on.", true);
            AddS(GIT_COMMIT_SHORT_KEY, ()=> GitValue(a => a.CommitShort), "The shortened hash of the commit the project is currently on. eg: 3f2a91c", true);
            AddS(GIT_COMMIT_MESSAGE_KEY, ()=> GitValue(a => a.CommitMessage), "The whole message of the commit the project is currently on - both the subject and the body.", true);
            AddS(GIT_COMMIT_MESSAGE_SUBJECT_KEY, ()=> GitValue(a => a.CommitMessageSubject), "The first line of the message of the commit the project is currently on. Always a single line.", true);
            AddS(GIT_COMMIT_MESSAGE_BODY_KEY, ()=> GitValue(a => a.CommitMessageBody), "Everything after the first line of the message of the commit the project is currently on. Empty for a single line commit message, and can span multiple lines otherwise.", true);
            AddS(GIT_COMMIT_AUTHOR_KEY, ()=> GitValue(a => a.CommitAuthor), "The name of the author of the commit the project is currently on.", true);
            AddS(GIT_COMMIT_AUTHOR_EMAIL_KEY, ()=> GitValue(a => a.CommitAuthorEmail), "The email address of the author of the commit the project is currently on.", true);
            AddS(GIT_COMMIT_DATE_KEY, ()=> GitValue(a => a.CommitDate), "The date the commit the project is currently on was authored, in the format YYYY-MM-DD.", true);
            AddS(GIT_TAG_KEY, ()=> GitValue(a => a.Tag), "The most recent git tag reachable from the current commit. eg: v1.4.2. Empty when the repository has no tags.", true);

            // Versions
            AddS(VERSION_KEY, ()=> Application.version, "The version of your project as specified in Player Settings.");
            AddS(VERSION_MAJOR_KEY, ()=> Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Major), "The version of your project as specified in Player Settings but only the major segment. eg: a1 from a1.2.3-beta1");
            AddS(VERSION_MINOR_KEY, ()=> Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Minor), "The version of your project as specified in Player Settings but only the minor segment. eg: 2 from a1.2.3-beta1");
            AddS(VERSION_PATCH_KEY, ()=> Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Patch), "The version of your project as specified in Player Settings but only the patch segment. eg: 3 from a1.2.3-beta1");
            AddS(VERSION_REVISION_KEY, ()=> Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Revision), "The version of your project as specified in Player Settings but only the revision segment. eg: beta1 from a1.2.3-beta1");

            AddS(VERSION_SEM_KEY, ()=> Utils.ToSemantic(Application.version), "The version of your project as specified in Player Settings but with only numbers. eg: 1.2.3 from a1.2.3-beta1");
            AddS(VERSION_MAJOR_SEM_KEY, ()=> Utils.ToSemantic(Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Major)), "The major segment of the version of your project as specified in Player Settings but with only numbers. eg: 1 from a1.2.3-beta1");
            AddS(VERSION_MINOR_SEM_KEY, ()=> Utils.ToSemantic(Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Minor)), "The minor segment of the version of your project as specified in Player Settings but with only numbers. eg: 2 from a1.2.3-beta1");
            AddS(VERSION_PATCH_SEM_KEY, ()=> Utils.ToSemantic(Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Patch)), "The patch segment of the version of your project as specified in Player Settings but with only numbers. eg: 3 from a1.2.3-beta1");
            AddS(VERSION_REVISION_SEM_KEY, ()=> Utils.ToSemantic(Utils.VersionSegmentToString(Application.version, Utils.VersionSegment.Revision)), "The revision segment of the version of your project as specified in Player Settings but with only numbers. eg: 1 from a1.2.3-beta1");
        }

        internal static string RootPath()
        {
            // Strip out the key itself so a self referencing root path can not loop forever
            return Utils.Replace(Preferences.RootPath, ROOT_PATH_KEY, "", StringComparison.OrdinalIgnoreCase);
        }

        private static string GitValue(Func<Git.GitSnapshot, string> selector)
        {
            return Git.Enabled ? selector(Git.GetSnapshot()) : "<GitDisabled>";
        }

        private static void AddS(string key, Func<string> formatter, string tooltip, bool canBeCached = false)
        {
            Command command = new Command(key, formatter, tooltip, canBeCached);
            FormatToCommand[key] = command;
        }
    }
}