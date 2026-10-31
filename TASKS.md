# Tasks

## Storage settings folder picker
- [ ] Decide between Syncfusion `SfFileManager` and the in-app folder picker.
  - `SfFileManager` needs a server endpoint that implements Syncfusion's file-operations protocol. No published server package was found on NuGet, so this is a custom build.
  - The in-app picker already works, with a New Folder button (uncommitted, 7 tests passing).
- [ ] Report: Change on Storage settings did not move files. Need the category, old and new paths, and whether a success message appeared.
- [ ] Commit the New Folder picker change after review.
