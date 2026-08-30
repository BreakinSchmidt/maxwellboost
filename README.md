# 🎧 MaxwellBoost

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue.svg)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-8.0--windows-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Latency](https://img.shields.io/badge/Latency-0%20ms%20(In--Engine)-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An intelligent, zero-latency background service and system tray monitor for the **Audeze Maxwell** wireless headset microphone on Windows.

MaxwellBoost automatically and persistently applies **+20 dB digital gain boost** (customizable on-the-fly via a tray slider or settings) to your microphone directly inside the Windows Audio Engine, completely eliminating volume drops across **system reboots**, **headset power cycles**, and **USB reconnects**.

---

## 📌 The Problem

The **Audeze Maxwell** is an industry-leading wireless planar magnetic headset, but on Windows, its boom microphone output is noticeably quiet in Discord, Microsoft Teams, OBS, Zoom, and games. 

- **Hardware Gain Ceiling**: The standard Windows USB audio driver limits the microphone volume slider to 100% (0 dB digital gain), offering no hardware microphone boost slider (+10dB/+20dB).
- **The Equalizer APO Quirk**: While [Equalizer APO](https://sourceforge.net/projects/equalizerapo/) can apply a digital preamp boost, Windows dynamically recreates or reinitializes audio capture endpoints when wireless headsets turn off/on. Equalizer APO fails to re-attach its hooks, causing the gain boost to reset or fail every time:
  1. You turn your headphones off and back on.
  2. Windows boots up with the headphones powered off.
  3. Windows or communication apps pull down the microphone recording slider.

---

## 💡 How MaxwellBoost Solves It

**MaxwellBoost** runs quietly in your Windows system tray, listening for low-level Windows CoreAudio COM hardware events (`IMMNotificationClient`) in real time. 

The millisecond your Audeze Maxwell powers on or reconnects, MaxwellBoost:
1. **Identifies the active capture endpoint GUID**.
2. **Automatically hooks the Audio Processing Object (APO)** in the Windows Registry (`FxProperties`).
3. **Synchronizes the preamp gain** (+20.0 dB by default) in the APO configuration.
4. **Warms up the audio stream** to force the Windows Audio Service (`audiosrv`) to immediately bind the APO pipeline.
5. **Locks the Windows recording volume** to 100% (scalar 1.0) and unmutes the device if muted.
6. **Logs the operation** with microsecond timestamps to `C:\logs\maxwell.log` with safe 7-day daily rotation.

```
+-----------------------------------------------------------------------------------------------+
|                                    WINDOWS AUDIO ENGINE                                       |
|                                                                                               |
|  [ Audeze Maxwell Headset ] (Powered On / Connected)                                         |
|               |                                                                               |
|               v                                                                               |
|  [ USB / Wireless Dongle Driver ] ---> [ MMDevice Endpoint: Capture GUID ]                    |
|                                                     |                                         |
|                                                     v                                         |
|                                    [ Audio Processing Object (APO) ] <---+                    |
|                                       - EqualizerAPO.dll                 | (Auto-Hooked)      |
|                                       - Preamp: +20 dB (Configurable)    |                    |
|                                                     |                    |                    |
|                                                     v                    |                    |
|                                       [ Windows Audio Service ]          |                    |
|                                        (audiosrv / WASAPI)               |                    |
|                                                     |                    |                    |
|       +---------------------------------------------+                    |                    |
|       |                     |                       |                    |                    |
|       v                     v                       v                    |                    |
|   [ Discord ]           [ Games ]               [ OBS/Teams ]            |                    |
|                                                                          |                    |
+--------------------------------------------------------------------------|--------------------+
                                                                           |
+--------------------------------------------------------------------------|--------------------+
|                                    MAXWELLBOOST APPLICATION              |                    |
|                                                                          |                    |
|  +------------------------------+        +-------------------------------+                    |
|  | CoreAudio Event Watcher      | -----> | APO & Endpoint Volume Manager |                    |
|  | (IMMNotificationClient COM)  |        | - Injects FxProperties & Child|                    |
|  | - Instant connect/disconnect |        | - Syncs config.txt (+20 dB)   |                    |
|  +------------------------------+        | - Enforces 100% Mic Volume    |                    |
|               |                          | - Re-initializes Audio Stream |                    |
|               v                          +-------------------------------+                    |
|  +------------------------------+                        |                                    |
|  | Windows System Tray Monitor  | <----------------------+                                    |
|  | - 🟢 Green / ⚪ Grey status  |                                                             |
|  | - 🎚️ Interactive Gain Slider |                                                             |
|  | - 🔔 Notification Toggle     |                                                             |
|  | - Quick Control Menu         |                                                             |
|  +------------------------------+                                                             |
|               |                                                                               |
|               v                                                                               |
|  +------------------------------+                                                             |
|  | Daily Rotating Logger        | -----> Writes to C:\logs\maxwell.log                        |
|  | (Safe 7-day retention)       |        (Isolated: strictly protects other files in C:\logs) |
|  +------------------------------+                                                             |
+-----------------------------------------------------------------------------------------------+
```

---

## ✨ Features

- **⚡ 0 ms Added Latency**: Processes audio natively in the Windows Audio Engine pipeline without virtual audio cables (e.g. VB-Cable) or user-space buffering delays.
- **🎯 Zero App Reconfiguration**: All applications (Discord, Zoom, Teams, OBS, Games) capture directly from `Microphone (Chat-Audeze Maxwell)`.
- **🔄 Auto-Reconnect & Reboot Persistence**: Automatically detects headset power-on and wake events in milliseconds.
- **🎚️ Interactive Quick Menu Gain Slider**:
  - Drag the built-in slider (0 to 40 dB) right inside the tray context menu to adjust your gain instantly.
  - Or click **Set Custom Gain (dB)...** to type any precise decimal value (e.g. `18.5` dB).
  - **Zero Popups on Adjustment**: Adjusting the gain slider or boost level updates silently without toast notifications.
- **🔔 Optional Notifications (Off by Default)**: Enable or disable Windows toast notifications directly from the tray right-click menu (`Show Toast Notifications [✓]`).
- **🖥️ System Tray Monitor**:
  - 🟢 **Green Microphone**: Maxwell is Connected & Boosted (+20 dB active, 100% volume).
  - ⚪ **Grey Microphone**: Headset is Off / Standby Watcher active.
  - Right-click menu for instant boost reload, opening logs, editing settings, or toggling Windows startup.
- **🔥 Live Settings Hot-Reload**: Automatically detects any manual edits to `appsettings.json` and updates the audio gain immediately without restarting the app.
- **🛡️ Safe 7-Day Daily Rotating Logger**:
  - Automatically archives previous days to `maxwell-YYYY-MM-DD.log`.
  - Automatically prunes `maxwell-*.log` files older than 7 days.
  - **Strictly isolated**: Safely ignores all non-Maxwell files in `C:\logs`.

---

## 📦 Prerequisites

### 1. Equalizer APO (Core DSP Engine)
- **Why is it needed?** Windows does not natively provide software gain above 0 dB for USB audio devices. Equalizer APO provides the registered Audio Processing Object DLL (`EqualizerAPO.dll`) that performs real-time float PCM amplification inside the Windows Audio Engine (`audiosrv`).
- **What to install**: Download and install [Equalizer APO (v1.3+ or latest)](https://sourceforge.net/projects/equalizerapo/).
- **Note**: You only need Equalizer APO installed on your machine. You **never need to open or use Equalizer APO's GUI, Editor, or Configurator** — MaxwellBoost automates 100% of the registry hooks and configurations for you.

### 2. .NET 8.0 Runtime (Windows Desktop)
- **Why is it needed?** MaxwellBoost is built on .NET 8 (`net8.0-windows`).
- Download the [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) if not already installed.

---

## 🚀 Installation & Quick Start

### Option A: Automatic Installation (Recommended)
1. Clone or download this repository to your machine (e.g. `D:\code\maxwellboost`).
2. Open **PowerShell** in the project directory.
3. Run the startup installer:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\install-startup.ps1
   ```
   *This automatically builds the release binary, registers MaxwellBoost in your Windows Startup registry (`HKCU\...\Run`), and launches the System Tray monitor.*

---

### Option B: Manual Build & Run
1. Open PowerShell in the project directory:
   ```powershell
   # 1. Build and publish
   powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1

   # 2. Run the application
   .\publish\MaxwellBoost.exe
   ```

---

## 🎛️ Tray Quick Menu

Right-click the microphone icon in your Windows notification area (near the clock) to access:

| Menu Item | Action |
|---|---|
| **Status / Volume** | Live connection state (e.g. `Connected (+20 dB)`) and Windows endpoint volume level |
| **Gain Slider (0–40 dB)** | Drag to increase or decrease mic volume gain silently in real time |
| **✏️ Set Custom Gain...** | Opens an input dialog to specify any exact gain value (e.g. `18.5` dB) |
| **⚡ Re-apply Boost Now** | Forces an immediate scan, settings reload, and APO stream re-bind |
| **📄 Open Log File** | Opens `C:\logs\maxwell.log` in your default text editor |
| **⚙️ Open Settings** | Opens `appsettings.json` for manual configuration |
| **Show Toast Notifications [✓]** | Toggles Windows connect/disconnect balloon toast popups on or off (disabled by default) |
| **Run on Windows Startup [✓]** | Toggles automatic launch on Windows login |
| **❌ Exit MaxwellBoost** | Disables tray icon and cleanly exits |

---

## ⚙️ Configuration Reference (`appsettings.json`)

Configuration is stored in `publish\Config\appsettings.json` (or `src\Config\appsettings.json`):

```json
{
  "DeviceNameFilter": "Chat-Audeze Maxwell",
  "GainDb": 20.0,
  "EnforceVolume": true,
  "TargetVolumeScalar": 1.0,
  "LogDirectory": "C:\\logs",
  "LogFileName": "maxwell.log",
  "LogRetentionDays": 7,
  "ShowNotifications": false,
  "PollingFallbackSeconds": 10,
  "EqualizerApoConfigPath": "C:\\Program Files\\EqualizerAPO\\config\\config.txt"
}
```

### Complete Setting Descriptions

| Setting | Type | Default | Description |
|---|---|---|---|
| **`DeviceNameFilter`** | `string` | `"Chat-Audeze Maxwell"` | Case-insensitive substring filter used to locate the Audeze Maxwell capture endpoint. If your device appears under a slightly different name in Windows Sound Settings, adjust this filter accordingly. |
| **`GainDb`** | `double` | `20.0` | Digital gain boost in decibels applied to your microphone in Equalizer APO. `20.0` dB represents a $10.0\times$ linear amplitude boost. Adjust between `0.0` and `40.0` dB based on your preference. |
| **`EnforceVolume`** | `bool` | `true` | When `true`, automatically locks the Windows Recording Volume slider to `TargetVolumeScalar` whenever the headset is detected or power-cycled, preventing third-party apps (e.g. Discord auto-gain) from pulling your volume down. |
| **`TargetVolumeScalar`** | `float` | `1.0` | The target master volume level in Windows CoreAudio between `0.0` (0% / muted) and `1.0` (100% / 0 dB attenuation). Recommended to leave at `1.0`. |
| **`LogDirectory`** | `string` | `"C:\\logs"` | Directory where operational logs are written. If the directory does not exist, MaxwellBoost will create it automatically. |
| **`LogFileName`** | `string` | `"maxwell.log"` | The active log file name. All connection events, gain updates, and errors are recorded here. |
| **`LogRetentionDays`** | `int` | `7` | Number of days to retain rotated daily log archives (`maxwell-YYYY-MM-DD.log`). Files matching this pattern older than `LogRetentionDays` are automatically cleaned up at midnight. |
| **`ShowNotifications`** | `bool` | `false` | When `true`, displays Windows balloon toast notifications when the Audeze Maxwell headset connects or disconnects. Notifications are never displayed on volume or gain changes. Defaults to `false`. |
| **`PollingFallbackSeconds`** | `int` | `10` | Frequency in seconds for secondary background state verification. Ensures device reconnection is caught even if Windows COM event notifications are dropped during OS sleep or hibernate resume. |
| **`EqualizerApoConfigPath`** | `string` | `@"C:\Program Files\EqualizerAPO\config\config.txt"` | Absolute file path to the Equalizer APO `config.txt` file where MaxwellBoost injects the device preamp directive. |

---

## 🧪 Diagnostic Utility & CLI Commands

MaxwellBoost includes built-in diagnostic and testing tools:

### Run Self-Diagnostic Test
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-cli.ps1
```
or directly:
```powershell
.\publish\MaxwellBoost.exe --test
```
*Scans all audio endpoints, verifies registry hooks, checks Equalizer APO configuration, ensures 100% volume level, and writes a test event to `C:\logs\maxwell.log`.*

### Command-Line Arguments
| Argument | Description |
|---|---|
| *(no args)* | Runs as the background System Tray application |
| `--test`, `-t` | Runs one-shot diagnosis, applies boost, and prints output to console |
| `--status`, `-s` | Checks current Maxwell connection state and volume level |
| `--console`, `-c` | Runs interactive continuous watcher with live console logging |
| `--help`, `-h` | Displays CLI help |

---

## 🗑️ Uninstallation

To remove MaxwellBoost from Windows Startup and stop the running application:
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

---

## ❓ Frequently Asked Questions (FAQ)

#### Q: Do I need to keep Equalizer APO installed?
**A:** Yes, keep Equalizer APO installed on your drive because Windows Audio Service loads its core processing DLL (`EqualizerAPO.dll`) to perform the DSP boost in the audio engine. However, you never need to open Equalizer APO's Editor or Configurator applications.

#### Q: Does MaxwellBoost add any audio latency?
**A:** **0 ms**. MaxwellBoost configures the native in-engine Audio Processing Object (APO). Audio buffers flow directly from your USB driver into the Windows audio pipeline without passing through user-mode buffers or virtual cables.

#### Q: Will this interfere with my other log files in `C:\logs`?
**A:** **No**. MaxwellBoost's daily rotation and cleanup logic strictly targets files matching the regex `maxwell-*.log` and will never alter or delete any other files in `C:\logs`.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
