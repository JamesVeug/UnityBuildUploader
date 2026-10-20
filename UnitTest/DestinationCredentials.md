# Destination test credentials

How to get the credentials for the destination tests in `UnitTest/Destinations` and `UnitTest/Apple`. Every
test uploads one small dummy file and cleans up after itself (see the table in [`README.md`](README.md)).
A test without its variables is ignored, so set up only the services you use.

Use test accounts, repositories and apps where you can. These are real credentials: never commit them, and
revoke them when you stop testing.

## Setting the variables (Windows)

1. Press **Win+R**, run `rundll32 sysdm.cpl,EditEnvironmentVariables`.
2. Under **User variables**, click **New...** and add each variable named in the sections below.
   (This keeps secrets out of your PowerShell history.)
3. Quit Unity and Unity Hub completely, including the Hub tray icon, then reopen them. Programs only see
   variables that existed when they started.

## Running the tests

1. Open the project in Unity and choose **Window > General > Test Runner > EditMode**.
2. Expand **BuildUploader.UnitTest.Destinations** (and **BuildUploader.UnitTest.Apple** on Unity 2022.3+).
3. Select the tests and click **Run Selected**. An ignored test names the first variable it is missing.
4. Every upload task increments the counters in `BuildUploader/ProjectSettings.json`. Revert that file
   afterwards.

## GitHub (draft release)

1. Create a test repository on GitHub (private is fine). Tick **Add a README** so it has a branch.
2. Open **Settings > Developer settings > Personal access tokens > Fine-grained tokens > Generate new token**.
3. Under **Repository access**, choose **Only select repositories** and pick the test repository.
4. Under **Permissions > Repository permissions**, set **Contents** to **Read and write**.
5. Click **Generate token** and copy it.
6. Set:
   - `BUILDUPLOADER_TEST_GITHUB_TOKEN`: the token
   - `BUILDUPLOADER_TEST_GITHUB_REPOSITORY`: `owner/name`, e.g. `your-name/BuildUploaderTests`

The test makes a draft prerelease with the dummy file attached, then deletes it.

## Google Drive

1. Go to https://console.cloud.google.com and create a project, e.g. "Build Uploader Tests".
2. Open **APIs & Services > Library**, search for **Google Drive API** and click **Enable**.
3. Open **Google Auth Platform** (the OAuth consent screen) and click **Get started**. Enter an app name and
   your email, choose **External** and finish.
4. On **Audience**, click **Publish app** so the status is **In production**. In "Testing", refresh tokens
   stop working after 7 days.
5. On **Clients**, click **Create client**:
   - Application type: **Web application**
   - Authorized redirect URIs: `https://developers.google.com/oauthplayground`

   Click **Create** and copy the **Client ID** and **Client secret**.
6. Open https://developers.google.com/oauthplayground and click the gear icon (top right). Tick
   **Use your own OAuth credentials**, then paste the Client ID and Client secret.
7. In **Step 1**, type this scope into the box under the list and click **Authorize APIs**:
   `https://www.googleapis.com/auth/drive.file`
   (to test Google Play too, add a space and `https://www.googleapis.com/auth/androidpublisher`).
8. Sign in and allow access. If Google warns that the app is unverified, click **Advanced** and continue;
   it is your own app.
9. In **Step 2**, click **Exchange authorization code for tokens** and copy the **Refresh token**.
10. Set:
    - `BUILDUPLOADER_TEST_GOOGLE_CLIENT_ID`
    - `BUILDUPLOADER_TEST_GOOGLE_CLIENT_SECRET`
    - `BUILDUPLOADER_TEST_GOOGLE_REFRESH_TOKEN`

The `drive.file` scope only lets the test see files it created. It uploads to the root of My Drive, reads the
file back and deletes it.

## Google Play

You need a Play Console account with an app that already has a build. The API cannot create apps, and the
first build has to be uploaded in Play Console.

1. Follow the Google Drive steps, including the `androidpublisher` scope in step 7. The Google account you
   sign in with must have access to the app in Play Console.
2. In the same Cloud project, enable **Google Play Android Developer API** (APIs & Services > Library).
3. In Play Console, open **Setup > API access**. If it asks you to link a Google Cloud project, link the one
   from step 1.
4. Set `BUILDUPLOADER_TEST_GOOGLE_PLAY_PACKAGE` to the app's package name, e.g. `com.company.game`.

Dry run: the test opens an edit (this checks the account and package) and deletes it. Nothing is uploaded or
committed.

## Dropbox

1. Go to https://www.dropbox.com/developers/apps and click **Create app**.
2. Choose **Scoped access** and **App folder**, and give it a unique name.
3. On the **Permissions** tab, tick `files.content.write` and `files.content.read`, then click **Submit**.
4. On the **Settings** tab, copy the **App key** and **App secret**.
5. In a browser, open this URL with your app key in place of `APP_KEY`:
   `https://www.dropbox.com/oauth2/authorize?client_id=APP_KEY&response_type=code&token_access_type=offline`

   Click **Allow** and copy the code. It works once and expires after a few minutes.
6. In PowerShell, run the following and paste the values when asked. It prints the refresh token.

   ```powershell
   $body = @{ grant_type = 'authorization_code'; code = (Read-Host 'Code'); client_id = (Read-Host 'App key'); client_secret = (Read-Host 'App secret') }
   (Invoke-RestMethod -Method Post -Uri 'https://api.dropboxapi.com/oauth2/token' -Body $body).refresh_token
   ```

7. Set:
   - `BUILDUPLOADER_TEST_DROPBOX_APP_KEY`
   - `BUILDUPLOADER_TEST_DROPBOX_APP_SECRET`
   - `BUILDUPLOADER_TEST_DROPBOX_REFRESH_TOKEN`

The test uploads to `Apps/<your app>/BuildUploaderTests/`, reads the file back and deletes it. The empty
folder stays.

## Epic Games

You need an Epic Developer Portal organization with a product.

1. Download **BuildPatchTool** from the Developer Portal and unzip it. Set `BUILDUPLOADER_TEST_EPIC_SDK` to
   the folder that contains `Engine\Binaries\Win64\BuildPatchTool.exe`.
2. In the Developer Portal, open your product's **Product Settings**. Copy the **Organization ID** and
   **Product ID**, and under **BuildPatchTool Credentials** create or copy a **Client ID** and **Client secret**.
3. Pick the artifact to upload to (a test artifact if you have one) and copy its **Artifact ID**.
4. Set:
   - `BUILDUPLOADER_TEST_EPIC_ORGANIZATION_ID`
   - `BUILDUPLOADER_TEST_EPIC_PRODUCT_ID`
   - `BUILDUPLOADER_TEST_EPIC_ARTIFACT_ID`
   - `BUILDUPLOADER_TEST_EPIC_CLIENT_ID`
   - `BUILDUPLOADER_TEST_EPIC_CLIENT_SECRET`

BuildPatchTool has no dry run. The binary is never labeled, so players never see it. Each run leaves one
unlabeled binary named `build-uploader-test-<date>-<id>` in the artifact.

## Xbox (Partner Center)

You need a Partner Center account with an app whose first submission was published in Partner Center. The API
creates new submissions by copying the last published one.

1. In the Azure portal (https://portal.azure.com), open **Microsoft Entra ID > App registrations > New
   registration**. Name it, e.g. "Build Uploader tests", and click **Register**.
2. Copy the **Application (client) ID** and **Directory (tenant) ID** from the overview page.
3. Open **Certificates & secrets > New client secret**, add it and copy the **Value** (it is shown once).
4. In Partner Center, open **Account settings** (gear icon) **> User management > Microsoft Entra
   applications > Add Microsoft Entra application**. Pick the app and give it the **Manager** role.
5. Copy the app's **Store ID** from **Product management > Product identity**.
6. Set:
   - `BUILDUPLOADER_TEST_XBOX_TENANT_ID`
   - `BUILDUPLOADER_TEST_XBOX_CLIENT_ID`
   - `BUILDUPLOADER_TEST_XBOX_CLIENT_SECRET`
   - `BUILDUPLOADER_TEST_XBOX_PRODUCT_ID`: the Store ID

Dry run: the test creates a submission, uploads the dummy file as its package, and deletes the submission.
It fails if a submission is already in progress for the app, so finish or delete that one first.

## Apple (App Store Connect)

You need an Apple Developer Program membership and an app record in App Store Connect. Unity 2022.3 or newer.

1. In App Store Connect, open **Users and Access > Integrations > App Store Connect API > Team Keys**.
2. Click **+**, name the key, give it the **App Manager** role and click **Generate**.
3. Download the `AuthKey_<KEY ID>.p8` file (it can only be downloaded once) and keep it outside the project.
   Copy the **Key ID**, and the **Issuer ID** shown above the list of keys.
4. Open **Apps >** your app **> App Information** and copy the **Apple ID** (a number).
5. Set:
   - `BUILDUPLOADER_TEST_APPLE_ISSUER_ID`
   - `BUILDUPLOADER_TEST_APPLE_KEY_ID`
   - `BUILDUPLOADER_TEST_APPLE_PRIVATE_KEY_PATH`: the full path to the `.p8` file
   - `BUILDUPLOADER_TEST_APPLE_APP_ID`: the Apple ID

Dry run: the test signs a token with the key and reads the app. It does not run altool, so it works on Windows.

## Already covered elsewhere

- **itch.io**: `ItchioWebGLTests` only needs Butler (`BUILDUPLOADER_TEST_BUTLER`), because Butler's dry run
  does not log in.
- **Steam**: see the host project's `.ci/README.md` (Steam credentials).
- **Local Path, Nowhere**: Core tests that need no credentials.
- **Nintendo, PlayStation**: Core tests with a fake upload tool (the SDKs are under NDA). They run on macOS and
  Linux only, because a script cannot stand in for a Windows `.exe`.
