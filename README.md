# 🎧 MaxwellBoost

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue.svg)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET-8.0--windows-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Latency](https://img.shields.io/badge/Latency-0%20ms%20(In--Engine)-brightgreen.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An intelligent, zero-latency background service and system tray monitor for the **Audeze Maxwell** wireless headset microphone on Windows.

MaxwellBoost automatically and persistently applies **+20 dB digital gain boost** to your microphone directly inside the Windows Audio Engine, completely eliminating volume drops across **system reboots**, **headset power cycles**, and **USB reconnects**.

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
3. **Synchronizes the preamp gain** (+20.0 dB) in the APO configuration.
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
|                                       - Preamp: +20 dB                   |                    |
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
|  | - Balloon Toast Notifications|                                                             |
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
- **🖥️ System Tray Monitor**:
  - 🟢 **Green Microphone**: Maxwell is Connected & Boosted (+20 dB active, 100% volume).
  - ⚪ **Grey Microphone**: Headset is Off / Standby Watcher active.
  - Right-click menu for instant boost reload, opening logs, editing settings, or toggling Windows startup.
  - Optional Windows balloon notifications when the headset connects/disconnects.
- **🛡️ Safe 7-Day Daily Rotating Logger**:
  - Automatically archives previous days to `maxwell-YYYY-MM-DD.log`.
  - Automatically prunes `maxwell-*.log` files older than 7 days.
  - **Strictly isolated**: Safely ignores all non-Maxwell files in `C:\logs`.
- **🎛️ Fully Configurable**: Adjust gain (e.g. `20.0` dB), volume scalar (`1.0`), log directory, or target device name in `appsettings.json`.

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
2. Open **PowerShell** as Administrator or Standard User in the project directory.
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

## ⚙️ Configuration (`appsettings.json`)

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
  "ShowNotifications": true,
  "PollingFallbackSeconds": 10,
  "EqualizerApoConfigPath": "C:\\Program Files\\EqualizerAPO\\config\\config.txt"
}
```

### Settings Reference
- **`DeviceNameFilter`** *(string)*: Substring used to identify your Audeze Maxwell microphone capture endpoint (default: `"Chat-Audeze Maxwell"`).
- **`GainDb`** *(double)*: Digital boost in decibels applied to the microphone (default: `20.0`).
- **`EnforceVolume`** *(bool)*: Automatically sets the Windows microphone recording volume to 100% (default: `true`).
- **`TargetVolumeScalar`** *(float)*: Target master volume scalar between `0.0` (0%) and `1.0` (100%) (default: `1.0`).
- **`LogDirectory`** *(string)*: Directory where log files are written (default: `"C:\\logs"`).
- **`LogRetentionDays`** *(int)*: Number of days to retain rotated daily log archives `maxwell-YYYY-MM-DD.log` (default: `7`).
- **`ShowNotifications`** *(bool)*: Shows Windows balloon toast notifications when the headset connects or disconnects (default: `true`).
- **`PollingFallbackSeconds`** *(int)*: Periodic fallback sync interval in seconds for sleep/hibernate resume (default: `10`).

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
