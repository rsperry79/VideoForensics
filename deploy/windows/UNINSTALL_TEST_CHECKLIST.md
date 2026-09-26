# Windows Sandbox Test Checklist — Installer / Uninstaller Data Safety

Run this entirely inside **Windows Sandbox** (Start menu → "Windows Sandbox"), never on the host
machine. The sandbox has its own disposable `%ProgramData%`, so nothing here can touch the host's
real `C:\ProgramData\VideoForensics` (currently 208.84 GB of recovered data — do not test against
that folder).

Prep (one-time, on the host, before opening the sandbox):
- [ ] Enable the feature if not already on: `Enable-WindowsOptionalFeature -Online -FeatureName "Containers-DisposableClientVM" -All` (elevated PowerShell, reboot if prompted)
- [ ] Have `deploy\windows\bin\VideoForensicsSetup.exe` ready to drag into the sandbox window (or share the folder via the sandbox's config)

## 1. Fresh install

- [ ] Run `VideoForensicsSetup.exe` in the sandbox
- [ ] Confirm both "VideoForensics Server (Windows Service)" and "Desktop Client" are **checked by default**
- [ ] Confirm the FFmpeg / shortcuts tasks toggle correctly with their parent component
- [ ] Confirm the Data Directory page shows resolved real paths (not raw `%ProgramData%` placeholders), one box per category
- [ ] Complete the install
- [ ] `Get-Service VideoForensics` → Running
- [ ] Browse to `http://localhost:5162` → redirects to `/setup`

## 2. First-run setup

- [ ] Create an admin account via `/setup`
- [ ] Sign in, confirm the dashboard loads
- [ ] Upload/create some dummy data (a fake device pairing, a note, whatever's easiest) so `%ProgramData%\VideoForensics` is non-empty — check with `Get-ChildItem $env:ProgramData\VideoForensics -Recurse`

## 3. Uninstall — "Keep" path

- [ ] Start uninstall (Programs & Features → VideoForensics → Uninstall)
- [ ] At the first prompt, choose **"Keep the data (recommended)"**
- [ ] Confirm uninstall completes, service is removed (`Get-Service VideoForensics` → not found)
- [ ] Confirm `%ProgramData%\VideoForensics` **still exists** with all prior content intact

## 4. Reinstall, then Uninstall — "Cancel" path

- [ ] Reinstall (repeat step 1, data dir already exists from step 3)
- [ ] Start uninstall, at the first prompt choose **"Cancel uninstall"**
- [ ] Confirm the uninstaller aborts — VideoForensics is still listed in Programs & Features, service still installed/running

## 5. Uninstall — "Delete → Back up first" path

- [ ] Start uninstall, choose **"Delete the data"** at the first prompt, then **"Back up, then delete"** at the second
- [ ] Confirm a `VideoForensics.backup-<timestamp>` folder appears next to the original in `%ProgramData%`, with the same content
- [ ] Confirm the original `%ProgramData%\VideoForensics` is gone after the backup completes
- [ ] Confirm uninstall completes cleanly

## 6. Reinstall, then Uninstall — "Delete → No backup" path

- [ ] Reinstall, recreate some dummy data
- [ ] Start uninstall, choose **"Delete the data"** then **"Delete immediately, no backup"**
- [ ] Confirm `%ProgramData%\VideoForensics` is gone immediately, no backup folder created
- [ ] Confirm uninstall completes cleanly

## 7. Upgrade-over-existing-install sanity check

- [ ] With VideoForensics installed and data present, run `VideoForensicsSetup.exe` again (same version or bumped `/DAppVersion`)
- [ ] Confirm the Data Directory page is **skipped** on this run (upgrade path, not fresh install)
- [ ] Confirm the service is stopped before file copy and restarted after, with no prompt about data (upgrade never touches `%ProgramData%`)

---

Report back anything that doesn't match — especially any path where uninstall could delete data
without both an explicit "Delete" choice **and** a second explicit backup/no-backup choice.
