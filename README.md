# KevLauncher

KevLauncher is a WPF system tray launcher for Windows. Drop apps, shortcuts,
scripts, folders, or documents into the launcher window, then run them from the
window or directly from the tray menu.

## Run

```powershell
dotnet run
```

## Current Features

- Runs as a system tray app.
- Double-click the tray icon to show the launcher.
- Close or minimize the window to keep it running in the tray.
- Drag and drop files or folders to add launcher entries.
- Add items with the `Add` button or tray menu.
- Search by name or path.
- Run items with the `Run` button, double-click, Enter, or tray menu.
- Remove the selected item with Delete or the `X` button.
- Persists entries to `%APPDATA%\KevLauncher\launcher-items.json`.
