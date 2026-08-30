# AGENTS.md — Agent & Developer Guide for MaxwellBoost

This document serves as the primary orientation and architectural reference for AI coding assistants (Antigravity, Claude Code, Cursor, Copilot) and human developers working on **MaxwellBoost**.

---

## 🧭 Project Overview

**MaxwellBoost** is a lightweight Windows background service and system tray monitor for the **Audeze Maxwell** wireless headset. It automatically guarantees a **+20 dB digital gain boost** (configurable) to the headset microphone directly inside the Windows Audio Engine (`audiosrv`) with **0 ms added latency**.

### Why this project exists
- **0 dB Hardware Limit**: The standard Windows USB audio driver limits the Maxwell microphone volume slider to 100% (0 dB digital gain), making the microphone quiet in Discord, Teams, games, and OBS.
- **Dynamic Endpoint Dropout**: Equalizer APO provides software preamp gain, but Windows dynamically creates or re-registers capture endpoints when wireless headsets turn off/on. Equalizer APO loses its hooks on disconnects and reboots.
- **Solution**: MaxwellBoost listens for real-time CoreAudio COM hardware events (`IMMNotificationClient`), dynamically binds APO registry hooks (`FxProperties`), manages [`config.txt`](file:///C:/Program%20Files/EqualizerAPO/config/config.txt), enforces 100% volume level, and safely rotates logs daily at `C:\logs\maxwell.log`.

---

## 🛠️ Tech Stack & Runtime Environment

- **Target Framework**: .NET 8.0 Windows (`net8.0-windows`)
- **UI Framework**: Windows Forms (`<UseWindowsForms>true</UseWindowsForms>`) with high-DPI custom system tray components
- **Language**: C# 12
- **Audio APIs**: Windows CoreAudio COM interop (`IMMDeviceEnumerator`, `IMMNotificationClient`, `IAudioEndpointVolume`, `IAudioClient`, `IPropertyStore`)
- **DSP Engine**: Equalizer APO (`EqualizerAPO.dll` Audio Processing Object)
- **OS Target**: Windows 10 / Windows 11 (x64)

---

## 📁 Repository Structure

```
D:\code\maxwellboost/
├── AGENTS.md                     # This file (AI agent & architecture guide)
├── CLAUDE.md                     # Claude-specific entrypoint referencing AGENTS.md
├── README.md                     # User-facing documentation & quick start
├── LICENSE                       # MIT License
├── .gitignore                    # Standard .NET gitignore
├── MaxwellBoost.sln              # Visual Studio / .NET Solution
├── src/                          # C# Source code
│   ├── MaxwellBoost.csproj       # Project file (net8.0-windows, WinForms, Single-File)
│   ├── Program.cs                # Entry point (CLI argument dispatcher, single instance mutex, tray host)
│   ├── Config/
│   │   ├── AppSettings.cs        # Strongly-typed configuration model with hot-reloading & JSON serialization
│   │   └── appsettings.json      # Default settings template
│   ├── CoreAudio/
│   │   ├── ComInterfaces.cs      # CoreAudio COM interface definitions (IMMDevice, IAudioEndpointVolume, etc.)
│   │   ├── AudioDeviceInfo.cs    # Model representing capture device name, state, flow, and GUID
│   │   ├── AudioDeviceWatcher.cs # Event-driven watcher implementing IMMNotificationClient + fallback polling
│   │   └── VolumeEnforcer.cs     # Enforces 100% Windows endpoint volume via IAudioEndpointVolume
│   ├── Apo/
│   │   └── ApoManager.cs         # Manages config.txt, FxProperties registry hooks, and WASAPI stream warmup
│   ├── Logging/
│   │   └── DailyRotatingLogger.cs# 7-day safe daily rotating logger for C:\logs\maxwell.log
│   └── UI/
│       ├── Icons.cs              # Managed PNG-in-ICO dynamic high-DPI microphone icon generator
│       ├── GainSliderControl.cs  # Embedded ToolStrip TrackBar slider (0–40 dB)
│       ├── CustomGainDialog.cs   # Custom decimal gain input dialog form
│       └── TrayMainForm.cs       # Invisible Form message pump, NotifyIcon, context menu, FileSystemWatcher
└── scripts/                      # PowerShell deployment & automation scripts
    ├── build.ps1                 # Builds and publishes release binary to publish/
    ├── install-startup.ps1       # Publishes, registers Windows Run startup key, and launches detached process
    ├── test-cli.ps1              # Runs one-shot diagnosis (--test) and displays live status & logs
    └── uninstall.ps1             # Removes startup registry key and stops running instances
```

---

## ⚡ Common Commands & Workflows

### 1. Building the Solution
```powershell
# Quick build
dotnet build D:\code\maxwellboost\MaxwellBoost.sln -c Release

# Publish release distribution to publish/
powershell -ExecutionPolicy Bypass -File D:\code\maxwellboost\scripts\build.ps1
```

### 2. Running Diagnostic Tests
```powershell
# Run self-diagnosis and check current device/APO status
powershell -ExecutionPolicy Bypass -File D:\code\maxwellboost\scripts\test-cli.ps1

# Or run CLI directly
D:\code\maxwellboost\publish\MaxwellBoost.exe --test
```

### 3. CLI Modes
- `MaxwellBoost.exe` : Runs GUI System Tray application (default).
- `MaxwellBoost.exe --test` / `-t` : One-shot diagnostic scan and log output.
- `MaxwellBoost.exe --status` / `-s` : Quick device connection and volume status.
- `MaxwellBoost.exe --console` / `-c` : Runs continuous watcher with console output.

### 4. Deploying & Starting
```powershell
# Install to Windows Startup and launch detached
powershell -ExecutionPolicy Bypass -File D:\code\maxwellboost\scripts\install-startup.ps1

# Stop and uninstall
powershell -ExecutionPolicy Bypass -File D:\code\maxwellboost\scripts\uninstall.ps1
```

---

## 🧠 Architectural Guidelines & Critical Gotchas

### 1. In-Engine APO Hooks (`ApoManager.cs`)
- **Equalizer APO CLSID**: `{EACD2258-FCAC-4FF4-B36D-419E924A6D79}`.
- **Registry Key**: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\{ENDPOINT_GUID}\FxProperties`.
- **Property Key**: `{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},5` (PKEY_FX_StreamEffectClsid).
- **Child APO Key**: `HKLM\SOFTWARE\EqualizerAPO\Child APOs\{ENDPOINT_GUID}`.
- **Preamp Injection**: In `config.txt`, device-specific rules must be formatted with invariant culture (`CultureInfo.InvariantCulture`):
  ```
  Device: "Chat-Audeze Maxwell" capture
  Preamp: 20.0 dB
  ```

### 2. Log Isolation Guarantee (`DailyRotatingLogger.cs`)
- `C:\logs` contains legacy and system log files.
- The rotation cleanup **MUST STRICTLY** filter by regex `^maxwell-(\d{4}-\d{2}-\d{2})` or file pattern `maxwell-*.log`.
- **NEVER delete or modify any non-Maxwell files in `C:\logs`**.

### 3. WinForms Message Pump & Hidden Tray Window (`TrayMainForm.cs`)
- In .NET 8 Windows Forms, calling `Application.Run(context)` without an active `Form` handle or calling `Visible = false` in `OnLoad` can cause `Application.Run` to terminate immediately when `OpenForms.Count == 0`.
- `TrayMainForm` forces handle creation via `_ = this.Handle;`, sets `Opacity = 0`, `WindowState = FormWindowState.Minimized`, `ShowInTaskbar = false`, `Location = Point(-3000, -3000)`.
- Thread-safe UI updates must be dispatched via `SafeInvoke()` (`BeginInvoke`).

### 4. Hot-Reload (`FileSystemWatcher`)
- `TrayMainForm` initializes a `FileSystemWatcher` on the directory of `appsettings.json`.
- Edits to `appsettings.json` (such as changing `GainDb` or `ShowNotifications`) trigger a 300ms debounced `_settings.Reload()` and `_watcher.SyncCurrentState()`.

### 5. File Locks During Publish (`build.ps1`)
- When updating the build, `MaxwellBoost.exe` in `publish\` might be locked by the active background instance.
- `build.ps1` checks for running `MaxwellBoost` processes and stops them before `dotnet publish` executes.

### 6. Process Detachment in Scripts (`install-startup.ps1`)
- Standard `Start-Process` inside an ephemeral PowerShell session / agent subshell can terminate child processes upon runner exit.
- `install-startup.ps1` uses `Invoke-CimMethod -ClassName Win32_Process -MethodName Create` to spawn a detached background process owned by the user session.

---

## 📝 Configuration Reference (`appsettings.json`)

| Setting | Type | Default | Purpose |
|---|---|---|---|
| `DeviceNameFilter` | string | `"Chat-Audeze Maxwell"` | Substring filter for matching the capture endpoint |
| `GainDb` | double | `20.0` | Digital boost in decibels |
| `EnforceVolume` | bool | `true` | Enforces Windows recording slider to 100% |
| `TargetVolumeScalar` | float | `1.0` | Target volume level (1.0 = 100%) |
| `LogDirectory` | string | `"C:\\logs"` | Directory for log output |
| `LogFileName` | string | `"maxwell.log"` | Active log filename |
| `LogRetentionDays` | int | `7` | Daily rotation retention cutoff in days |
| `ShowNotifications` | bool | `false` | Balloon toast notifications on connect/disconnect (off by default) |
| `PollingFallbackSeconds` | int | `10` | Safety net polling interval |
| `EqualizerApoConfigPath` | string | `@"C:\Program Files\EqualizerAPO\config\config.txt"` | Target config path |
