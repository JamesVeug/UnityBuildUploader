<p align="center"><img src="https://github.com/JamesVeug/UnityBuildUploader/blob/main/Assets/LargeIcon.png?raw=true" alt="MAS Logo"></p>

<h1 align="center">Build Uploader</h1>

<p align="center">Unity Editor Tool For Making Builds, Uploading Them To Online Services And Notifying Chat Apps</p>

<p align="center">Steamworks | Itch.io | Epic Games | Google | Apple | Consoles | Dropbox | Github</p>

<p align="center">Discord | Slack | Google Chat</p>

<p align="center">Open Source | For Commercial Use</p>

<hr>

[![LICENSE](https://img.shields.io/github/license/JamesVeug/UnityBuildUploader)](LICENSE)
[![GitHub Issues Open](https://img.shields.io/github/issues/JamesVeug/UnityBuildUploader.svg?maxAge=2592000)]()
[![GitHub Issues Closed](https://img.shields.io/github/issues-closed-raw/JamesVeug/UnityBuildUploader.svg?maxAge=2592000)]()
[![Github Release Downloads](https://img.shields.io/github/downloads/JamesVeug/UnityBuildUploader/total.svg)]()
[![STARS](https://img.shields.io/github/stars/JamesVeug/UnityBuildUploader)](https://github.com/JamesVeug/UnityBuildUploader)

## Key Points 💡
- Windows, Mac, Linux support
- Unity Supported (Light and Dark)
  - Unity 6000.1-7
  - Unity 202x
  - Unity 2020 and 2019
    - Requires package [com.unity.sharp-zip-lib](https://docs.unity3d.com/Packages/com.unity.sharp-zip-lib@1.3/manual/Installation.html)
- Create multiple builds using Build Configs or Unity's Build Profiles.
  - Windows, Mac, Linux, Webgl (Others loosely supported via Unity's Build Profiles)
- Tiny build size impact
- For personal and commercial use
- Open Source
- Safely modify builds before uploading
  - Remove files/folders
  - Compress/Decompress files
  - DRM wrap (anti-piracy)

### Interface
#### Simple and Advanced GUI support
![alt text](https://i.imgur.com/X94RXLW.png "Build Uploader Window")

#### Non-UI support
```csharp
[MenuItem("Test/Create new Task and Upload to Steam")]
public static async Task StartTask()
{
    UploadConfig config = new UploadConfig();
    
    // Create a build using a custom Build Profile
    config.AddSource(new BuildProfileSource("Windows Development"));
    
    // Build to this directory. {buildNumber} will be replaced with a locally stored incremental number
    config.AddDestination(new LocalPathDestination("C:/MyBuilds/{buildNumber}"));
    
    // Upload to Steam
    config.AddDestination(new SteamUploadDestination(1141030, "internal", 1141031));
    
    // Create task and execute to begin the upload
    UploadTask task = new UploadTask("Upload to Steam", config);
    task.SetBuildDescription("My first build!");
    
    await task.StartAsync();
    Debug.Log("Upload complete. Success: " + task.Report.Successful);
}
```

```csharp
[MenuItem("Test/Upload Existing Profile")]
public static async Task StartTask2()
{
    // Load existing Upload Profile that gets a file and uploads it
    UploadProfile profile = UploadProfile.FromProfileName("Save Locally");
    
    // Create task and execute to begin the upload
    UploadTask task = new UploadTask(profile);
    
    await task.StartAsync();
    Debug.Log("Upload complete. Success: " + task.Report.Successful);
}
```

#### Batch mode

`"C:\Program Files\Unity\Hub\Editor\<Unity Version>\Editor\Unity.exe" -quit -nographics -batchmode -logFile logs.log -projectPath  "<Path To Your Unity Project>" -executeMethod Wireframe.BatchModeUtil.Execute 41cf5e`

Replace `41cf5e` with the `GUID or `Name` of your Upload Profile

#### **Unity's command CLI**

`unity command build_uploader --start_tasks "41cf5e"`

Replace `41cf5e` with the `GUID or `Name` of your Upload Profile

### Project state and version control

Shared Build Uploader configuration is stored in `BuildUploader/ProjectSettings.json`. The frequently changing build and upload counters are stored separately in `BuildUploader/ProjectState.json`.

To keep counters local to each workspace, ignore only the state file:

```gitignore
/BuildUploader/ProjectState.json
```

Tracking `ProjectState.json` instead retains shared counter state for the team, but allocated build numbers and started upload tasks will continue to produce intentional changes to that file.

### Services
  - Steamworks
    - Uploading a build to any branch or depots
    - DRM wrap (anti-piracy)
  - Itch.io
    - Upload a build
  - Epic Games
    - Upload builds to specific artifacts
  - Google (Experimental)
    - Upload builds to Google Drive
    - Upload builds to Google Play
    - Send message to Google Chat
  - Apple (Unity 2022 and up) (Experimental)
    - Upload builds to Apple
    - Add build to Test Flight
  - Dropbox (Experimental)
    - Upload a build
  - Playstation (Experimental)
    - Upload a build
  - Xbox (Experimental)
    - Upload a build
  - Nintendo (Experimental)
    - Upload a build
  - Git
    - Format text according to current git state 
    - Create and push Tags
  - Github
    - Upload a new Release
  - Discord
    - Send message to a channel
  - Slack
    - Send message to a channel
  - Email
    - Send customized emails
  - Unity Cloud Build
    - View, download and start builds


Prefer a fully headless run with no Editor open? 
- See [Starting a BuildTask without UI](https://github.com/JamesVeug/UnityBuildUploader/wiki/Starting-a-BuildTask-without-UI).
- Or Full argument reference: [CLI](https://github.com/JamesVeug/UnityBuildUploader/wiki/CLI). 


## Wiki
- Home: https://github.com/JamesVeug/UnityBuildUploader/wiki
- How to Install: https://github.com/JamesVeug/UnityBuildUploader/wiki/How-to-Install
- How to Setup: https://github.com/JamesVeug/UnityBuildUploader/wiki/How-to-Setup
- How does it Work: https://github.com/JamesVeug/UnityBuildUploader/wiki#how-does-it-work
- CLI (`unity command`): https://github.com/JamesVeug/UnityBuildUploader/wiki/CLI


## Links 
- Support Me: https://buymeacoffee.com/jamesgamesnz
- Discord: https://discord.gg/R2UjXB6pQ8
- Github: https://github.com/JamesVeug/UnityBuildUploader
- Asset Store: https://assetstore.unity.com/packages/tools/utilities/build-uploader-306907



## Security 🔒
This package does NOT distribute any personal information. Any information entered is encrypted locally on your computer.

Keeping your credentials safe is important to me!

If you discover any security related issues, please email me, message on discord or create an issue on [github.](https://github.com/JamesVeug/UnityBuildUploader)


## Reporting bugs / suggesting changes ❓

If you find a bug or want to suggest a change, [create an issue on github](https://github.com/JamesVeug/UnityBuildUploader/issues)

Include:
- What the problem is
- What you expected to happen
- What version of Build Uploader you are using
- What version of Unity you are using
- Are you using Windows/Linux/Mac


## How to Contribute 🔨
- Fork the [repository](https://github.com/JamesVeug/UnityBuildUploader)
- Make your changes
- Create a pull request to the `develop` branch
  - Include detailed description of the changes you made and why
  - Include what versions of Unity you tested it on
  - Include any concerns with the changes you made (So i'm aware of them too)


## How to Support 🙏

A lot of effort has been put into this package for others to freely use. Any kind of support is greatly appreciated and encourages further work!

- ✍️ Review on the [Asset Store](https://assetstore.unity.com/packages/tools/utilities/build-uploader-306907)
- ⭐ Star the [Github repository](https://github.com/JamesVeug/UnityBuildUploader)
- ☕ Buy me a coffee: [buymeacoffee](https://buymeacoffee.com/jamesgamesnz)
- 💬 Report bug reports or suggestions improvements: [Github Issues](https://github.com/JamesVeug/UnityBuildUploader/issues)
- 🔗 Share the package with your friends and colleagues

### Supporters

- [Nementic Games](https://store.steampowered.com/developer/nementic/)
- [Classy Games](https://store.steampowered.com/developer/classygames)

## License 🪪
Creative Commons - CC0 1.0 Universal

Use this package however you want - commercially or non-commercially.


## Created with AI
Claude AI has assisted by providing suggestions and creating services that would otherwise not be possible by myself.

All suggestions have been reviewed and rewritten by a professional Senior Unity Developer to fit the package's requirements.
