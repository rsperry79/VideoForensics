# VideoForensics iOS Sideload

This folder contains guidance for building and deploying VideoForensics directly to your own iPhone or iPad for personal testing. This is **not** a distributable release artifact — it is a local developer sideload only.

## What This Is (and Isn't)

- **Sideload**: building and running the app directly from your Mac to a physical iOS device connected via USB or wireless debugging, signed with your personal Apple ID.
- **Not TestFlight**: the free Apple Developer account does not support TestFlight (Apple's beta distribution platform).
- **Not App Store**: this is not a path to publish VideoForensics on the App Store.
- **Not ad-hoc distribution**: free accounts cannot create shareable `.ipa` files for distribution to other devices. To share with others, you either sideload to their device directly (requires their device to be present and connected to your Mac) or upgrade to a paid Apple Developer Program membership ($99/year).
- **Expires after 7 days**: free-account provisioning profiles are short-lived. The installed app stops launching after 7 days and must be rebuilt and redeployed from Xcode/your build machine to renew it.

## Prerequisites

### On Your Mac

- **Xcode 15+** (with iOS platform/SDK components installed)
  - Install via the App Store or download from [developer.apple.com](https://developer.apple.com/download)
  - Verify: `xcode-select --print-path` should return `/Applications/Xcode.app/Contents/Developer` (or your custom Xcode path)
- **.NET 10 SDK** (to build VideoForensics from the command line)
  - Install from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)
- **MAUI iOS workload** (bridges .NET build to Xcode/iOS SDK)
  - Install: `dotnet workload install maui-ios`
- **A free Apple ID** (or a paid Apple Developer Program account)
  - Sign in to Xcode: **Xcode** > **Settings** > **Accounts** > add your Apple ID
  - The first time you deploy to a physical device with a free account, Xcode automatically creates a personal team and registers your device — you do not need to manually create a team

### On Your Device

- **iPhone or iPad running iOS 15.0 or later**
- **Connected to your Mac** via:
  - USB cable (plug in to Mac), or
  - Wireless debugging over the local network (both Mac and device on the same Wi-Fi, pair via Xcode's **Devices and Simulators** window)
- **Trusted developer profile** (see [Trusting the Developer Profile](#trusting-the-developer-profile) below)

## Building and Deploying

### Option 1: Command Line

From the VideoForensics repository root:

```bash
dotnet build src/client/maui/VideoForensics.MauiApp/VideoForensics.MauiApp.csproj \
  -f net10.0-ios \
  -p:BuildIOS=true \
  -c Debug \
  -t:Run
```

This builds and immediately deploys to the connected device (or simulator, if a simulator is running). The build is unsigned but valid for personal device testing under Personal Team signing.

**Device targeting** (if multiple devices are connected):
- Xcode caches the last-deployed device, so normally the build deploys to that device
- To deploy to a specific device, you may need to open Xcode, select the device from the product scheme menu, and run once; subsequent `dotnet build` invocations will remember it

### Option 2: Xcode IDE (More Visual, Better Feedback)

1. Open the solution in Visual Studio for Mac or VS Code with the MAUI extension
2. Select the iOS target and a connected device from the target scheme dropdown
3. Press Play (Run) or Cmd+R

This offers a visual device picker and live build feedback in the IDE.

## Trusting the Developer Profile

The first time you install the app on a physical device, iOS shows a security warning about an untrusted developer profile. You must explicitly trust it for the app to launch.

1. On your device, go to **Settings** > **General** > **VPN & Device Management** (or **Device Management** on older iOS versions)
2. Tap the developer profile under "Enterprise App"
3. Tap **Trust**
4. Confirm by tapping **Trust** again in the dialog

After this one-time setup, the app will launch normally.

## The 7-Day Expiry

On a free Apple Developer account, the provisioning profile and code signing certificate issued by Personal Team are valid for only 7 days. After 7 days:

- The app stops launching and shows a "Untrusted Enterprise Developer" error
- The device must remain connected to your Mac for the app to run (it cannot be installed standalone for longer than 7 days)

To continue testing after 7 days:

1. Plug the device into your Mac (or connect via wireless debugging)
2. Rebuild and redeploy using the same `dotnet build` or Xcode command as before
3. The new provisioning profile resets the 7-day timer

This is a platform limitation imposed by Apple, not a bug in VideoForensics.

## Upgrading to a Paid Developer Account

If you want to distribute the app beyond your own device (e.g., share with colleagues for testing, or prepare for App Store release), a paid Apple Developer Program membership ($99/year, [developer.apple.com](https://developer.apple.com)) unlocks:

- **Ad-hoc distribution**: build and sign a shareable `.ipa` file once, install it on up to 100 registered devices, each without re-signing or rebuilding every 7 days
- **TestFlight beta distribution**: use Apple's TestFlight platform to invite testers, distribute prerelease builds, and collect feedback via App Store Connect
- **App Store release**: publish VideoForensics to the App Store for download by any iOS user

The existing `.github/workflows/build-ios.yml` workflow is currently a compile-only check (it validates the app builds for iOS but does not sign, package, or distribute it). Once VideoForensics adopts a paid developer account, that workflow can be extended to build a signed `.ipa` and automate ad-hoc or TestFlight releases as part of the CI/CD pipeline.

## Troubleshooting

**Build fails with "maui-ios workload not installed"**
- Install the workload: `dotnet workload install maui-ios`
- Verify: `dotnet workload list | grep maui`

**"Xcode not found" or "iOS SDK not found"**
- Verify Xcode is installed: `xcode-select --print-path` should show `/Applications/Xcode.app/Contents/Developer` (or your custom path)
- If needed, point `dotnet` to your Xcode installation:
  ```bash
  sudo xcode-select --switch /Applications/Xcode.app/Contents/Developer
  ```

**"No provisioning profile matches the bundle identifier"**
- Ensure your Apple ID is signed into Xcode: **Xcode** > **Settings** > **Accounts**
- Plug in your device or start a simulator
- In Xcode, select the device from the scheme dropdown and press Play once — this creates the provisioning profile
- Retry the `dotnet build` command

**Device not showing up in build output**
- Verify USB connection or wireless debugging is active
- Restart Xcode: `killall Xcode`
- Restart the device
- In Xcode (**Window** > **Devices and Simulators**), check that your device appears and its status is "Connected"

**"Untrusted Enterprise Developer" error on device**
- Trust the profile: on the device, go to **Settings** > **General** > **VPN & Device Management** (or **Device Management**), tap the profile, tap **Trust**, and confirm
- See [Trusting the Developer Profile](#trusting-the-developer-profile) above for detailed steps

**"The specified item could not be found"**
- This can occur if the device UDID or bundle identifier changes unexpectedly
- Delete the provisioning profile manually: in Xcode, **Settings** > **Accounts**, select your team, click **Manage Certificates**, then **Revoke** any suspicious profiles
- Retry the build — Xcode will recreate a fresh profile

**App expires after 7 days**
- This is expected on a free account (see [The 7-Day Expiry](#the-7-day-expiry))
- Redeploy: reconnect the device to your Mac and run `dotnet build -t:Run` again

## Additional Resources

- [Microsoft .NET MAUI iOS Deployment](https://learn.microsoft.com/dotnet/maui/deployment/ios)
- [Apple Developer Account Registration](https://developer.apple.com/account)
- [Xcode Release Notes](https://developer.apple.com/documentation/xcode-release-notes)
