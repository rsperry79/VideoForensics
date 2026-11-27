# Plan: multi-camera primary layout, video menu, and share links

Status: approved in chat (share plan: "yes"). Multi-camera plan: approved in chat earlier. Not started.

## Part 1: Multi-camera view and video menu

### Layout (LiveView / CameraGrid)
- The primary camera fills the top 50% of the window height, full width.
- The other cameras sit in one row below it, equal width.
- Heights come from `--vf-vh` (set by `viewport-fit.ts`), so nothing scrolls.
- With fewer than four cameras, the primary keeps 50% and the others share the row.

### Primary camera
- Stored per browser in the existing layout prefs (`vfLayoutPrefs`), so it survives a reload.
- Default primary: the first camera.

### Right-click menu on video (replaces the browser menu)
- Make primary: shown only on non-primary cameras.
- Grab still: pauses playback first, then asks where the still goes: download, save as evidence, or both.
- Open single view: same as a tile click today.
- Full screen.
- Copy still link: enabled only after a still has been grabbed for that camera.

### Build order
1. Context menu component; suppress the browser menu on video.
2. Make primary, with the stored choice.
3. Grab still: pause, ask, then download and/or evidence save.
4. Layout: 50% primary above a row of the others.
5. Tests for each step; lite gate; commit.

## Part 2: Share links

### Wizard (Share button on the media and review pages)
1. Who can see it?
   - Authorized users: the link opens the media or review page the item came from. A signed-in user who already has access to the item sees it. Anyone else sees "auth denied" in place of the media. The wizard does not pick users.
   - Anyone with the link: no login. The link shows only the media, with download or view-only, chosen in the wizard.
2. Expiry: a default list (for example 1, 7, 30 days) or a custom date and time.

### Build order
1. Share link data model: target item, mode (authorized or link), download allowed, expiry, revoked flag.
2. Server check on every link request: expiry, revoked flag, and for authorized links the signed-in user's access.
3. Public link page: media only, download or view-only as set. No navigation to other pages.
4. Wizard UI on the media and review pages.
5. Tests for each access case: signed in with access, signed in without, signed out, expired, revoked.

### Risk
A no-login link exposes the media to anyone who gets the URL until it expires or is revoked.

## Notes
- Plan location: `.claude/plans/` in the repo (not `docs/plans`, because `docs` is a submodule with local changes).
- Each part is built on its own branch from `wip`, then merged into `wip`.
