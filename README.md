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
- Left-click the tray icon to show only the nested launcher tree.
- Right-click the tray icon for launcher management commands.
- Close or minimize the window to keep it running in the tray.
- Create virtual folders for a nested app hierarchy.
- Select a folder, then drag and drop files or folders to add launcher entries there.
- Add items with the `Add` button or tray menu.
- Search by name or path.
- Run items with the `Run` button, double-click, Enter, or the nested tray menu.
- Remove the selected item with Delete or the `X` button.
- Persists entries to `%APPDATA%\KevLauncher\launcher-items.json`.
