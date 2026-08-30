# Claude Code Instructions for MaxwellBoost

See [`AGENTS.md`](./AGENTS.md) for full architectural guidelines, Gotchas, and technical details.

## Quick Summary for Claude
- **Language/Framework**: C# / .NET 8.0 Windows (`net8.0-windows`) Windows Forms.
- **Root Directory**: `D:\code\maxwellboost`
- **Build Command**: `powershell -ExecutionPolicy Bypass -File scripts\build.ps1` or `dotnet build MaxwellBoost.sln -c Release`
- **Test Command**: `powershell -ExecutionPolicy Bypass -File scripts\test-cli.ps1`
- **Deploy/Run**: `powershell -ExecutionPolicy Bypass -File scripts\install-startup.ps1`
- **Target Logs**: `C:\logs\maxwell.log` (Strict 7-day rotation for `maxwell-*.log`, NEVER delete non-Maxwell files).

Refer to [`AGENTS.md`](./AGENTS.md) for complete details on COM interfaces, WinForms tray host lifecycle, hot-reloading, and Equalizer APO registry bindings.
