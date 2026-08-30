using System;
using System.Runtime.InteropServices;
using MaxwellBoost.Logging;

namespace MaxwellBoost.CoreAudio
{
    public class VolumeEnforcer
    {
        private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");
        private readonly DailyRotatingLogger _logger;

        public VolumeEnforcer(DailyRotatingLogger logger)
        {
            _logger = logger;
        }

        public (bool Success, float LevelScalar, bool WasMuted) EnforceVolume(IMMDevice device, float targetScalar = 1.0f)
        {
            try
            {
                var iid = IID_IAudioEndpointVolume;
                var hr = device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out var pUnk);
                if (hr != 0 || pUnk is not IAudioEndpointVolume endpointVolume)
                {
                    _logger.Warn($"Failed to activate IAudioEndpointVolume on device (HR: 0x{hr:X8})");
                    return (false, 0f, false);
                }

                endpointVolume.GetMute(out var isMuted);
                if (isMuted)
                {
                    var emptyGuid = Guid.Empty;
                    endpointVolume.SetMute(false, ref emptyGuid);
                    _logger.Info("Device was muted in Windows. Unmuted successfully.");
                }

                endpointVolume.GetMasterVolumeLevelScalar(out var currentScalar);
                if (Math.Abs(currentScalar - targetScalar) > 0.001f)
                {
                    var emptyGuid = Guid.Empty;
                    endpointVolume.SetMasterVolumeLevelScalar(targetScalar, ref emptyGuid);
                    _logger.Info($"Adjusted Windows endpoint volume from {currentScalar:P0} to {targetScalar:P0}.");
                    currentScalar = targetScalar;
                }

                return (true, currentScalar, isMuted);
            }
            catch (Exception ex)
            {
                _logger.Error("Error enforcing volume level", ex);
                return (false, 0f, false);
            }
        }
    }
}
