# Tableflow Android app

Native Expo / React Native app for waiter orders, kitchen preparation, cashier billing, and restaurant administration. It uses the same .NET API and PostgreSQL database as the web app.

## Install and connect

1. Copy `deliverables/Tableflow-1.0.0.apk` to an Android phone running Android 7.0 or later.
2. Open the APK and allow installation from that file manager/browser when Android asks.
3. Open Tableflow and enter your restaurant's HTTPS server origin, such as `https://restaurant.example.com`. Do not append `/api`.
4. Tap **Connect & continue**, then sign in with an account created by the restaurant administrator.

If the restaurant server has not been deployed yet, the app stays on its setup screen. There are no default staff credentials or embedded database credentials. You can configure the server later without replacing the APK. See `deploy/oci/README.md` for the existing server deployment setup.

To change servers, sign out and choose **Change restaurant server**. Server-specific login sessions are stored in Android secure storage. Drafts, unconfirmed orders and cached state are scoped to the server and staff account.

The app saves order drafts locally, recovers unconfirmed submissions using the same request ID, and refreshes after reconnecting. Submitting orders and confirming payments require a reachable server. Food-ready alerts work while the app is active; background push delivery is not implemented.

## Build a standalone signed APK on Windows

Requirements: Node.js, JDK 17, Android SDK platform/build-tools 36, NDK 27.1.12297006, CMake 3.22.1. Install mobile dependencies with `npm ci` inside `mobile`. Set `JAVA_HOME` and `ANDROID_HOME`, or use this workspace's `.tools` installations.

From the project root:

```powershell
./scripts/build-android.ps1
# If Windows Java reports a Unix-domain socket temporary-path error:
./scripts/build-android.ps1 -TemporaryDirectory C:\tf-build-temp
# For long workspace paths, build a temporary copy under a short path:
./scripts/build-android.ps1 -TemporaryDirectory C:\tf-build-temp -StagingDirectory C:\tf-build-temp\app
```

The script checks TypeScript, generates Android native files with the Expo config plugin, builds a release APK with bundled JavaScript, verifies its signature, and copies the APK and SHA-256 checksum into `deliverables`. Expo Go and a development bundler are not required to run it. The default APK supports ARM64, ARMv7, and x86-64.

`mobile/signing` contains the private release key and its password properties, generated once by the build script. It is excluded from Git. Back up this folder privately: Android requires the same signing key to install future updates over this APK. Never share the signing folder with staff or include it in a public source archive.

Version information lives in `mobile/app.json`. Increment `android.versionCode` for updates and update the output filename in the build script when changing the displayed version.

## Checks

```powershell
npm --prefix mobile run typecheck
node --test tests/mobile-connection.test.mjs shared/network.test.mjs
```

No production server is configured by this build. Multi-device order and payment verification requires a deployed backend and test staff accounts.
