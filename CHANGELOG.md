# 3.4.0

## Highlights
- Added Android support
- Build Uploader Window has a new simplified view when the window is very thin
- Added Dry Run UI to test making builds without actually uploading or executing actions
- Can now build addressables
- Can now send messages to discord using webhooks
- Moved Build Uploader menu items to `Tools/Build Uploader`
- Fixed Upload Tasks stopping if code compiles during a build
- Fixed projects without `com.unity.pipeline` package throwing exceptions
- Fixed compile errors on early Unity 6000.0 versions

## What's new
- Added Dry-Run support to the UI and BatchMode (Better handling with services too)
- Added `Build Addressables` toggle to all Build Sources
- Added `Build Addressables` Action
- Added Discord Web Hook Action
- Steamworks Auth button to check if you're verified and asks you to login without needing the console
- Added Update button to the Build Uploader window (Disableable in preferences)
- Added `Skip Number Updates` project setting to stop builds and uploads rewriting ProjectSettings.json
- Added `Invoke Static Method` Action to run a project's own static method
- Added live upload task reports that are written when a task starts and updated as it runs, so an interrupted task still leaves a report showing how far it got
- Added Menu items to indivisual source/modifier/destination/actions to move them up/down... etc
- Added right-click context menu option to Local Destinations path button to "Open in Explorer"
- Added preference toggle to open explorer when builds complete successfully (on by default)
- Added Android App Bundle to Build Settings
- Added Android Target Architecture to Build settings
- Added Android keystore support
- BatchUtils: Added `-dryRun`, `-uploadDescription` and `-uploadReportFile` to batch mode, and exits when a profile is missing or an upload fails
- Basic AI agent documentation

## What's Changed
- Moved Build Uploader menu items to `Tools/Build Uploader`
- Added Thin view of the Build Uploader Window that is visible when the width is low
- `Upload All` button text changed to `Run All` to be more expressive of what it does
- Steam Depot dropdown shows ids for when depots have the same id
- Added a new depot will set the new id to +1 the previous depot or app id
- Removed confirmation window when duplicate Upload Configs
- Welcome Window wall of text trimmed
- Embeds for Discord,Slack,Emails now have a formatable field
- Small UI QOL fixes

## What's Fixed
- Fixed Upload Tasks stopping if code compiles during a build
- Fixed projects without `com.unity.pipeline` package throwing exceptions
- Fixed compile errors on early Unity 6000.0 versions
- Fixed failing pre-build actions not stopping the upload task
- Fixed builds being deleted when the first Local Destination is disabled
- Fixes for all platforms regarding showing the console when pressing the CMD button
- Fixed pressing enter to pick a format string suggestion from losing focus on Unity 6
- Fixed paths being cleared when cancelling out of choose folder dialogue
- Fixed CLIs dry run from executing actions
- Fixed making builds using Build Profiles not using specific settings
- Fixed actions, sources, modifiers and destinations outside the core assembly not reloading
- Fixed upload tasks reporting success when a step failed without a reason but report says it failed
- Fixed Steam Depot ID field being impossible to clear and depots without an ID
- Fixed Steam depots merging and Depot IDs that could not be cleared or were never checked
- Fixed Slack attachment text not formatted
- Fixed choosing 'Choose Source' or 'Choose Destination' in an expanded Upload Config throwing a NullReferenceException
- Fixed Welcome Window opening when running tests or batch mode
- Fixed package minimum Unity version requirements
- Fixed a Unity 7000 error
- Fixed builds made using batch mode showing as 0ms
- Obfuscated more info that could appear in the task reports
- Windows: Fixed paths not saving to JSON correctly
- Linux & Mac: Command line fixes to work more natively
- Linux: Fixed needing to log into Steam every upload
- Android: Fixed builds failing even though the build succeeded
- Android: Fixed app bundle builds showing up as .apk


# 3.3.0

## Highlights
- 8 new experimental Services - Mobile, Console, Git, Email, Dropbox
- All reorderable lists have tooltips and placeholder exmaples text
- Added auto-complete dropdown for string formats when typing in text fields
- Git integration
- Unity CLI support
- Fixed non-english users unable to save data
- Fixed itch.io uploading not working on Linux

## What's new
- Added new Experimental services
  - Xbox
  - Playstation
  - Nintendo
  - Git
    - String formatting options
    - Create and push Tag
  - Apple (Unity 2022 and up)
      - New Apple Destination
      - New Testflight action
  - Google
      - New Google Drive destination
      - New Google Play destination
      - New Google Chat Action
  - Dropbox
  - Email
- Added main menu options to quick upload (Unity 6000.3 and up - Right click next to play button)
- Added Unity CLI support
- Added auto-complete dropdown for string formats when typing in text fields
- Added optional API Key option when uploading to itch.io
- Added 12 new string formats
  - {rootPath} The path every other path can start from as specified in Preferences. eg: C:/SomeFolder/Builds/{version}
  - {gitBranch} The git branch the project is currently on. eg: main. Reads HEAD when the repository is in a detached head state.
  - {gitCommit} The full hash of the commit the project is currently on.
  - {gitCommitAuthor} The name of the author of the commit the project is currently on.
  - {gitCommitAuthorEmail} The email address of the author of the commit the project is currently on.
  - {gitCommitDate} The date the commit the project is currently on was authored, in the format YYYY-MM-DD.
  - {gitCommitMessage} The whole message of the commit the project is currently on - both the subject and the body.
  - {gitCommitMessageBody} Everything after the first line of the message of the commit the project is currently on. Empty for a single line commit message, and can span multiple lines otherwise.
  - {gitCommitMessageSubject} The first line of the message of the commit the project is currently on. Always a single line.
  - {gitCommitShort} The shortened hash of the commit the project is currently on. eg: 3f2a91c
  - {gitTag} The most recent git tag reachable from the current commit. eg: v1.4.2. Empty when the repository has no tags.

## What's Changed
- Some warnings/errors have buttons taking you to preferences/settings to resolve them
- Added missing set description setter for Itch.io destination
- Upload tasks can now be cancelled
- Added context menu to Upload Task window
- Added preference toggle to show/hide blank string formatting tooltips (Off by default)
- Added supporters to Readme
- Improved welcome window UX 
- Git repository changed from `UnitySteamBuildUploader` to `UnityBuildUploader`
- `Used with AI` declaration updated
- Uploading via BatchMode now supports using the name of the Upload Profile
- Can now create a BuildProfileSource using the name of a Build Profile

## What's Fixed
- Fixed itch.io uploading not working on Linux
- Fixed config actions not executing
- Fixed config actions not showing errors or warnings in the UI
- Fixed post upload actions not showing formatted strings in UI
- Fixed lots of JSON errors (empty lists, non-english numbers and more)
- Fixed some steam logs showing username
- Fixed some warnings showing twice
- Fixed batch scripts not producing task reports
- Fixed string format tooltip double ups
- Fixed Menu Items showing top of the list of Window list
- Potential fix for some builds failing during modifying files due to other processes. 


# 3.2.2

## What's Fixed
- Fixed validation failing showing as successful
- Fixed new modifiers starting disabled when using default constructor
- Fixed new build configs having null values
- Fixed syntax errors for Unity 2019 and 6000.5
- Fixed unable to get build profiles for Unity 6000.5
- First pass fix for Linux unable to upload to steam
- Fixed Build Profiles always choosing first in the list on start and when uploading

## What's Changed
- All requests will log to their urls and response codes to task reports to help understand what the tool is doing


# 3.2.1

## What's new
- Added 'Show Upload Task Report Folder' context menu option to open the folder where the upload reports are saved

## What's Fixed
- Fixed some source edge cases resulting in unexpected empty folders
- Fixed Exclude Folder&File modifiers from not working when set to DeleteFromCache
- Fixed some text fields looking like a button and not an input text field
- Fixed some errors/warnings showing on Upload Configs that are disabled 

## What's Changed
- Moved Add Default Modifiers menu context option to Upload Config's menu context. 
- Can now specify WhenToExclude in constructors for ExcludeFilesModifier and ExcludeFoldersModifier 


# 3.2.0

## Highlights
- Greatly reduced time to build and copy to a local destination
- Better Batch script support - https://github.com/JamesVeug/UnityBuildUploader/wiki/Starting-a-BuildTask-without-UI
- Can now make builds using Unity's internal Build Profile system

## What's new
- Added support to make builds using Unity's build in Build Profiles (Unity 6.0 and up)
- Added DoNotCache to all sources to speed up getting sources (on by default)
- Added 'Save contents to LocalDestination' to preferences to save directly to a LocalDestination if available to speed up tasks. (On by default)
- Actions can now specify at what point during the upload they trigger. 
- New string formats
  - {taskStatus} - Get a small message describing the status of the Upload Task

## What's changed
- Auto-Generate Menu Items is now set to false by default
- Auto-Generate Menu Items now save to the `Assets/ThirdParty/BuildUploader` instead of the Packages folder (Changable in ProjectSettings)
- Builds are no longer cached before being copied to the destination
- Improved support for triggering uploads without Unity open

## What's fixed
- Fixed an edgecase where string formatting is case sensitive
- Fixed some edgecases where errors happen and silently kill upload tasks
- Fixed some Slack text formatting issues
- Fixed some JSON errors when strings contain double quotes
- Potentially fixed error when starting uploading using UI.
- Cleaned up some warnings that appear when compiling code
- Fixed some MenuItems showing until Tools instead of Window->BuildUploader
- Fixed readme icon


# 3.1.1

## What's new
- Added checklist to welcome window for basic setup steps
- Improved UX of the welcome window

## What's changed
- Steam username in preferences is now red when empty 

## What's fixed
- Fixed error showing when compiling code when no upload profiles have been made
- Fixed changelog not showing in welcome window when the package isn't in the package folder 


# 3.1.0

## Highlights
- Improved visual support for Unity Light Mode
- Can now upload to Epic Games Store
- Can now send messages to Slack via API
- Can now perform actions per Upload Config
- Steam Destination supports uploading to multiple depots.
- Lots more string formatting options

## What's new
- Added Epic Games Store destination
- Added Slack service to send messages via API
- Added Post Upload Config actions
- Added Steam Destination support for uploading to multiple depots.
- Added Compression option to BuildConfigs
- Added context option to duplicate Upload Profiles
- Added description format field to steam,itchio,github destinations to override description.
- Added preference option to make all text field format toffles start on
- Added preference option to toggle deleting of vdf files when uploading to Steamworks
- Added more versioning string formatting. Major, Minor, Patch, Revision and Semantic.
- Added `{uploadNumber}` string format to represent the unique number of the upload task.
- Added string formatting for steam/itchio/epic/local path to get certain info.
- Added auto-generated Menu Items to quickly upload from `Window->BuildUploader->Quick Upload` 
- Added context options that add extra new source/destination via upload config settings.
- Added context option top build config scenes to remove invalid scenes
- Added missing helper setter methods to Discord message action

## What's changed
- Steam Destination selecting an app file been revamped to be more user friendly.
- Improved Upload Task UX around auto-following logs and not being able to expand steps.
- ProjectSettings/Services has a tab per service now
- Description Edit button has been moved to the left and changed to a settings icon
- Changed Description "Reset" context option to show its reset text
- Fixed string format tooltips show ??? instead of the key to reduce clutter
- Changed discord server ID to a long so it can actually be used

## What's fixed
- Fixed unable to load UploadProfiles due to unhandled JSON 
- Fixed only 1 Upload Profile showing in dropdown when multiple with the same name exist.
- Fixed various edge case where duplicate scenes is possible in Build Configs.
- Fixed Discord message clearing text after a new line
- Fixed ProjectSettings/Preferences not properly filtering with expected keywords.
- Fixed some string formatting not working in discord messages. eg: {buildNumber}
- Fixed double quote characters breaking JSON deserialization in some edge cases.
- Fixed new lines not serializing properly in some text fields.

# 3.0.2

## What's new
- Added Steam desination toggle to overwrite the description when an using existing app file.
- Added Steam desination app/depot contextual options when in advanced mode
- Added Steam desination tooltips

## What's fixed
- Fixed unable to upload builds to 'none' branch on Steamworks
- Fixed issue where 'NullReferenceException' would show when opening the build uploader window with unspecified sources or destinations.
- Exposed SteamSDK for use outside the package

# 3.0.1

## What's new
- Added preferences toggle to auto show Upload Task Window when starting an upload
- Added `Remove all Build Configs` to Build Configs settings context menu
- Added {buildArchitecture} string format
- Added error messages if selected platforms aren't installed or supported

## What's fixed
- Fixed exception spam when viewing build configs in project settings
- Fixed unable to make builds in some Unity versions
- Fixed able to start an upload using platforms that aren't installed or supported
- Fixed obsolete platforms showing in dropdowns
- Fixed sometimes platforms defaulting to windows server - not windows player
- Fixed upload validation failing due to starting an upload that contains build configs
- Fixed upload task continuing to make multiple builds of previous have failed. (early out)
- Fixed unable to make WebGL builds if Unity isn't already set to WebGL
- Fixed WebGL builds not including build meta data
- Fixed default build configs not having any scenes in them (now adds current open scene)
- Fixed Task window not expanding to show all possible text for a step (still cuts off if over 32k character)
- Fixed builds failing if deep profile is enable but development build is disabled


# 3.0.0

## Highlights
  - Create multiple builds and upload them with 1 button
  - Upload builds to itch.io
  - Post a message to Discord using an App (Bot)
  - New Upload Task window to view old and in-progress tasks and their logs
  - Lots of Steamworks destination fixes (No more 2 factor per upload!)
  - Lots of UI and UX improvements

## What's new
  - Added BuildConfigs (Define all Editor and Player settings that you want in a build)
  - Added Itch.io upload destination
  - Added Post upload actions
  - Added Discord app messaging action (Can send messages to discord on successful upload)
  - Added Upload Profiles (Can name multiple sets of uploads and change between them)
  - Replaced Report window with new Task window to view in progress tasks and their logs `Window/Build Uploader/Open Upload Tasks Window`
  - Added BuildConfig source (Start a new build using the config when uploading)
  - Added BuildData to every build's StreamingAssets to see its build number (can be opted out in ProjectSettings)
  - Build Uploader now makes a ProjectID.txt to differentiate between different projects when using EditorPrefs
  - New string formats
    - {buildName} - The name of the build as specified in a build config.
    - {buildNumber} - The unique number of the build that is produced if enabled in Project Settings.
    - {buildTarget} - The target group of the upcoming build as defined in Player Settings.
    - {buildTargetGroup} - The target group of the upcoming build as defined in Player Settings.
    - {cacheFolderPath} - The path where all files and builds are stored when build uploader is working.
    - {machineName} - The name of the machine running the build.
    - {persistentDataPath} - The version of your project as specified in Player Settings.
    - {projectPath} - The version of your project as specified in Player Settings.
    - {productName} - The name of your product as specified in Player Settings. (Previously projectName)
    - {scriptingBackend} - The scripting backend for the next build as defined in Player Settings.
    - {taskFailedReasons} - Gets the reasons why the task failed to upload all destinations.
    - {taskProfileName} - The name of the upload profile or task specified when creating the task.

## What's changed
  - General
    - Removed Build button in the upload window in favor of using build configs.
    - Upload profiles / upload configs are saved per project and can sync with source control
    - Upload tab no longer shows percentage of upload
    - Added settings context options to reorder upload configs
    - Added foldout and right-click context options to all reorderable lists
    - Can now remove Steamworks Configs
    - Added `.*ButDontShipItWithYourGame` when creating a new folder regex modifier for IL2CPP builds
    - UI changes to Project settings and Preferences
  - Upload Tasks
    - API changed to allow for more customisability
    - Added Validation step to prevent starting an upload via CI if there are errors
    - Added PrepareSource step for caching before starting to make builds and modify editor
    - More logs added to assist debugging
    - Cleanup step is now async
    - Cleanup will now happen even if Preferences is set to not clear cache after upload
  - Unity Cloud
    - Tab moved to its own window `Window/Build Uploader/Open Unity Cloud Window`
    - CloudBuildManifest no longer makes a new UnityCloudBuildManifest.json in builds if it doesn't exist
  - Welcome Window
    - Changelog shows dropdowns per versions instead of everything all at once

## What's fixed
  - String formats
    - Fixed unable to use taskDescription and other strings in destinations
    - Fixed dropdowns not formatting (eg: Source type, steam depot...)
  - LocalPathDestination
    - Fixed show button not going to the path if its formatted
    - Fixed Duplicate File Handling not save/loading
    - Added error check for invalid characters in path 
  - Steamworks destination
    - Fixed uploading multiple builds to multiple depots on steam resulting in the same build being uploaded
    - Fixed first time uploading to steamworks without a branch throwing an error
    - Fixed changing Steam DRM toggle and flags from not auto-saving
    - Possible fix for Steam DRM wrapping the wrong .exe depending on Product name
  - Github
    - Fixed draft and prerelease always true when uploading
  - Other
    - Fixed some JSON deserialization edge cases
    - Fixed window icons not showing when the package is added to the Assets folder.


# 2.4.1
- Upload tab
  - Added pre-upload error reporting when Exclude files/folders has bad regex
  - Added warning when cache file path exceeds OS limit (Windows 260, Mac 255, Linux 4096 characters)
  - Added string format examples  to "Show as Formatted text" toggle
  - Fixed new modifiers starting with bad regex 
  - Fixed string formatting not working in most sources, modifiers, destinations
  - Fixed file/folder source text field not filling the entire container
- Preferences
  - Fixed cache build folder size not updating when changing the path


# 2.4.0
- General
  - Added menu item to open the build task report window `Window/Build Uploader/Build Task Report`
  - Welcome Window changelog text is now showing formatted text
- Upload Tab
  - Added `Build and Upload All` button
  - Added `LastBuildDirectory` source that chooses the directory of the last build made using the Build Uploader
  - Added button to start a new unity build and upload if successful
  - Fixed warning and error message UI double ups
  - Fixed errors and warnings showing when source/modifier/destination is disabled
- Exclude FIle/Folder Modifiers
  - Added `When To Exclude` dropdown to choose when to exclude files/folders. 
    - Set to `DoNotCopyFromSource` by default.
    - Existing modifiers will migrate to `DeleteFromCache`
- Steam Destination
  - Fixed uploading to multiple branches on Steam only appearing on 1
- LocalPathDestination
  - Can now use string formatting to change the file name and path
    - See [docs](https://github.com/JamesVeug/UnityBuildUploader/wiki/StringFormatter) for all formats you can use
  - Fixed non-existent path showing as an error and now shows as a yellow warning
- Preferences
  - Added toggle to toggle auto saving of build config changes
  - Added dropdown in preferences to change when confirmation popups show
  - Added dropdown in preferences to change when build task report shows
  - Added field in preferences to change what the default description is in the upload tab
    - See [docs](https://github.com/JamesVeug/UnityBuildUploader/wiki/StringFormatter) for all formats you can use
  - Now shows size of the cached builds folder

# 2.3.0
- General
  - Sources/Destinations/Modifiers show errors/warnings where on the build config instead of just on the button   
- Steam Destination 
  - Uploading multiple builds at once now happens sequentially now due to SteamSDK limitations
  - Can no longer upload to `default` branch due to SteamSDK limitations
  - Added more log handling to show errors from uploading
- Build Report
  - Shows display name of source type when showing files
- Fixes
  - Fixed Upload process not completing when the task completes.
  - Fixed Uploading to Steam with 2 factor saying it failed but actually succeeded.
  - Fixed Build Report not logging to console if the build task fails.
  - Fixed SteamGuard/Two Factor Authentication confirmation popup now showing when uploading multiple builds


# 2.2.0
- General
  - Added **Welcome Window** for more information `Window/Build Uploader/Welcome`
  - Added **Build Report Window** which shows after every build from the UI
  - Exposed a lot of code, so anyone can create and trigger a BuildTask without UI
  - BuildTasks create a BuildTaskReport to easily view logs
  - Made the Description text area bigger and added more options to copy/paste
  - All Sources/Modifiers/Destinations have a `?` to link to their documentation
  - Fixed new lines and tabs not serializing correctly
  - Fixed zipping a folder and saving to the same folder causing IOException
  - Fixed unzipping a .zip file halting editor 
  - Fixed many edge cases causing the BuildTask to run indefinitely and not reporting exceptions
  - Fixed Custom Sources/Modifiers/Destinations made outside the Build Uploader package not showing in the dropdowns
  - Fixed LocalPathDestination requiring a file name when not compressing to a .zip
  - Minor UI improvements
  - Minor Logging improvements
  - Basic [Wiki](https://github.com/JamesVeug/UnityBuildUploader/wiki) support
- Sources
  - Each config can now have multiple Sources
  - Added `URLSource` to download files from online
  - Added `FolderSource`
  - Added PathType to File and Folder Source to select files within the project
  - Changed File Source to only handle files
- Modifiers
  - Can now add/remove any Modifier same as Sources/Destinations
  - Can now be enabled/disabled
  - Added `CompressModifier` to turn a selection of files to a .zip file
  - Added `DecompressModifier` to unzip .zip files
- Destinations
  - Each config can now have multiple Destinations
  - Added `NewGithubRelease` Destination
  - Added Duplicate File dropdown to LocalPathDestination. Set to Overwrite by default
  - Changed SteamworksDestination to create files in the Prepare step to fail before uploading starts
  - Fixed SteamworksDestination not showing Steam Guard popup on first run
- Preferences
  - Added new checkbox to enable/disable auto decompressing .zip files from sources (On by default)
  - Added new checkbox to enable/disable auto saving of build tasks (Off by default)
  - Added new text field to change directory of cached builds.
  - Added tooltips to all preferences
  - Fixed Build Uploader not filtering by certain keywords

# 2.1.1
- General
  - First pass Mac/linux support
  - Fixed DRM Wrapping not working for games/companies with spaces in the name
  - Fixed DRM Wrapping not working with SteamGuard/Two Factor support properly
  - Safety guards to add modifiers back in case they disappear


# 2.1.0
## Upload Tab
- Added `Folder` Source so its now possible ot upload a folder for Steam DLC.
- Added `LocalPath` Destination to copy a modified build to somewhere on your computer.
- Changed `File` Source
  - Only uploads a selected file EXCEPT .exe which will upload the whole folder
  - Removed file extension restriction
- Added build modifiers to change a Build before uploading
  - Remove files meeting regex
    - By default, all build configs remove `*_DoNotShip` files.
  - Steam DRM (Anti-piracy)
    - Upload .exe to Steam so the game has to be run from Steam.
- Description no longer adds `Windows` to what gets uploaded to Steam.
  - Description is exactly what is written in the text box.
- Lots of fixes and improvements to the upload process

## Preferences
- Added Button to go open Cache folder
- Added Toggle to enable/disable removing of cached files after uploading
  - Great for checking what you're uploading before it uploads!
- Fixed trying to enter Steam credentials causing username/password to swap around making it impossible to enter

##  General
- Fixed a bunch of warnings that appear when recompiling the project

## Readme
- Added instructions of how the upload process works
- Lots more information

# 2.0.1
- Fixed issues where the package would not compile in Unity 2020 and 2019

# 2.0.0
- General
  - Package renamed to `Build Uploader`
  - Refactored Steamworks and UnityCloud code be in a services folder
  - Added support me, discord, github links to readme
  - All dropdowns describe what they need instead of something generic
- Fixes
  - Moved all saved directories to a `Build Uploader` folder within `Application.persistentDataPath`
  - Minor UI adjustments to the Upload tab
  - Fixed warnings that appear when recompiling the project
