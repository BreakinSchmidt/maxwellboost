using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;
using MaxwellBoost.Apo;
using MaxwellBoost.Config;
using MaxwellBoost.CoreAudio;
using MaxwellBoost.Logging;
using MaxwellBoost.UI;

namespace MaxwellBoost
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private const int ATTACH_PARENT_PROCESS = -1;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_WRITE = 0x2;
        private const uint OPEN_EXISTING = 0x3;

        private const string MutexName = @"Local\MaxwellBoost_App_SingleInstance";

        [STAThread]
        private static void Main(string[] args)
        {
            var isCli = args.Length > 0;

            if (isCli)
            {
                RedirectConsoleOutput();
            }

            var settings = AppSettings.Load();
            var logger = new DailyRotatingLogger(settings);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                logger.Error("Unhandled AppDomain Exception", e.ExceptionObject as Exception);
            };

            Application.ThreadException += (s, e) =>
            {
                logger.Error("Unhandled Application Thread Exception", e.Exception);
            };

            Application.ApplicationExit += (s, e) =>
            {
                logger.Info("Application.ApplicationExit triggered.");
            };

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                logger.Info("AppDomain ProcessExit triggered.");
            };

            var apoManager = new ApoManager(settings, logger);
            var volumeEnforcer = new VolumeEnforcer(logger);
            var watcher = new AudioDeviceWatcher(settings, logger, apoManager, volumeEnforcer);

            if (args.Length > 0)
            {
                var cmd = args[0].ToLowerInvariant();
                switch (cmd)
                {
                    case "--test":
                    case "-t":
                        RunTest(settings, logger, apoManager, volumeEnforcer, watcher);
                        return;

                    case "--status":
                    case "-s":
                        RunStatus(settings, logger, watcher);
                        return;

                    case "--console":
                    case "-c":
                        RunConsoleMode(settings, logger, watcher);
                        return;

                    default:
                        Console.WriteLine("\nMaxwellBoost CLI Usage:");
                        Console.WriteLine("  (no args)   : Run as Windows System Tray background application");
                        Console.WriteLine("  --test, -t  : Run diagnosis, verify Equalizer APO & volume, output results");
                        Console.WriteLine("  --status, -s: Check current device connection status");
                        Console.WriteLine("  --console,-c: Run interactive continuous watcher in console");
                        return;
                }
            }

            // GUI / System Tray Mode
            using var mutex = new Mutex(true, MutexName, out var isNewInstance);
            if (!isNewInstance)
            {
                logger.Warn("Another instance of MaxwellBoost is already running.");
                MessageBox.Show("MaxwellBoost is already running in the system tray.", "MaxwellBoost", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            logger.Info("Starting MaxwellBoost System Tray Service...");
            ApplicationConfiguration.Initialize();
            watcher.Start();

            var form = new TrayMainForm(settings, logger, watcher);
            Application.Run(form);
        }

        private static void RedirectConsoleOutput()
        {
            try
            {
                if (AttachConsole(ATTACH_PARENT_PROCESS) || AllocConsole())
                {
                    var hOut = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                    if (hOut != IntPtr.Zero && hOut != (IntPtr)(-1))
                    {
                        var safeHandleOut = new SafeFileHandle(hOut, true);
                        var fsOut = new FileStream(safeHandleOut, FileAccess.Write);
                        var writerOut = new StreamWriter(fsOut, System.Text.Encoding.Default) { AutoFlush = true };
                        Console.SetOut(writerOut);
                        Console.SetError(writerOut);
                    }
                }
            }
            catch
            {
                // Ignore console attachment errors
            }
        }

        private static void RunTest(
            AppSettings settings,
            DailyRotatingLogger logger,
            ApoManager apoManager,
            VolumeEnforcer volumeEnforcer,
            AudioDeviceWatcher watcher)
        {
            Console.WriteLine();
            Console.WriteLine("==================================================");
            Console.WriteLine("          MAXWELLBOOST DIAGNOSTIC TEST            ");
            Console.WriteLine("==================================================");
            Console.WriteLine($"Target Device Filter : {settings.DeviceNameFilter}");
            Console.WriteLine($"Target Gain Boost    : +{settings.GainDb} dB");
            Console.WriteLine($"Target Volume Scalar : {settings.TargetVolumeScalar:P0}");
            Console.WriteLine($"Log File Path        : {logger.LogFilePath}");
            Console.WriteLine($"Log Retention        : {settings.LogRetentionDays} days");
            Console.WriteLine($"APO Config Path      : {settings.EqualizerApoConfigPath}");
            Console.WriteLine("--------------------------------------------------");

            Console.WriteLine("[1/4] Checking Equalizer APO configuration file...");
            var configOk = apoManager.EnsureApoConfig(settings.DeviceNameFilter, settings.GainDb);
            Console.WriteLine($"      APO Config Status: {(configOk ? "SUCCESS (Updated/Verified)" : "FAILED")}");

            Console.WriteLine("\n[2/4] Scanning Windows CoreAudio Capture Endpoints...");
            watcher.SyncCurrentState(logStateChanges: true);

            if (watcher.IsMaxwellConnected && watcher.CurrentMaxwellDevice != null)
            {
                var dev = watcher.CurrentMaxwellDevice;
                Console.WriteLine($"      Device Found     : {dev.FriendlyName}");
                Console.WriteLine($"      Device Desc      : {dev.DeviceDesc}");
                Console.WriteLine($"      State            : {dev.State}");
                Console.WriteLine($"      Endpoint GUID    : {dev.EndpointGuid}");
                Console.WriteLine($"      Current Volume   : {watcher.CurrentVolumeLevel:P0}");

                Console.WriteLine("\n[3/4] Checking Registry Hooks for Endpoint GUID...");
                var regOk = apoManager.EnsureRegistryHooks(dev);
                Console.WriteLine($"      Registry Status  : {(regOk ? "SUCCESS / VERIFIED" : "WARNING (Check Permissions)")}");
            }
            else
            {
                Console.WriteLine("      Device Status    : NOT CONNECTED / OFF (Standby)");
                Console.WriteLine("      Note: MaxwellBoost will continuously monitor and apply boost as soon as the headset is powered on.");
            }

            Console.WriteLine("\n[4/4] Writing test event to log file...");
            logger.Info("Diagnostic test executed successfully.");
            Console.WriteLine($"      Log entry written to {logger.LogFilePath}");

            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("DIAGNOSTIC TEST COMPLETE");
            Console.WriteLine("==================================================");
            Console.WriteLine();
        }

        private static void RunStatus(AppSettings settings, DailyRotatingLogger logger, AudioDeviceWatcher watcher)
        {
            Console.WriteLine();
            watcher.SyncCurrentState(logStateChanges: false);
            if (watcher.IsMaxwellConnected && watcher.CurrentMaxwellDevice != null)
            {
                Console.WriteLine($"STATUS: CONNECTED (+{settings.GainDb} dB | Volume: {watcher.CurrentVolumeLevel:P0})");
                Console.WriteLine($"Device: {watcher.CurrentMaxwellDevice.FriendlyName}");
                Console.WriteLine($"Endpoint GUID: {watcher.CurrentMaxwellDevice.EndpointGuid}");
            }
            else
            {
                Console.WriteLine("STATUS: DISCONNECTED (Headset is off or dongle unplugged)");
            }
            Console.WriteLine();
        }

        private static void RunConsoleMode(AppSettings settings, DailyRotatingLogger logger, AudioDeviceWatcher watcher)
        {
            Console.WriteLine("\nStarting MaxwellBoost Console Monitor. Press Ctrl+C to stop.\n");
            logger.OnLogMessage += (level, msg) => Console.WriteLine(msg);

            watcher.Start();

            var quitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                quitEvent.Set();
            };

            quitEvent.WaitOne();
            Console.WriteLine("\nStopping watcher...");
            watcher.Dispose();
        }
    }
}
