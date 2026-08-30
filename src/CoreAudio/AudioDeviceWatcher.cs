using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using MaxwellBoost.Apo;
using MaxwellBoost.Config;
using MaxwellBoost.Logging;

namespace MaxwellBoost.CoreAudio
{
    public class AudioDeviceWatcher : IMMNotificationClient, IDisposable
    {
        private readonly AppSettings _settings;
        private readonly DailyRotatingLogger _logger;
        private readonly ApoManager _apoManager;
        private readonly VolumeEnforcer _volumeEnforcer;

        private IMMDeviceEnumerator? _enumerator;
        private System.Threading.Timer? _debounceTimer;
        private System.Threading.Timer? _pollingTimer;
        private bool _isDisposed;
        private readonly object _stateLock = new();

        public bool IsMaxwellConnected { get; private set; }
        public AudioDeviceInfo? CurrentMaxwellDevice { get; private set; }
        public float CurrentVolumeLevel { get; private set; }

        public event Action<AudioDeviceInfo, float, bool>? OnDeviceBoosted;
        public event Action<string>? OnDeviceDisconnected;
        public event Action<string>? OnStatusChanged;

        public AudioDeviceWatcher(
            AppSettings settings,
            DailyRotatingLogger logger,
            ApoManager apoManager,
            VolumeEnforcer volumeEnforcer)
        {
            _settings = settings;
            _logger = logger;
            _apoManager = apoManager;
            _volumeEnforcer = volumeEnforcer;

            EnsureEnumerator();
        }

        private IMMDeviceEnumerator? EnsureEnumerator()
        {
            if (_enumerator == null)
            {
                try
                {
                    var comObj = new MMDeviceEnumeratorComObject();
                    _enumerator = (IMMDeviceEnumerator)comObj;
                }
                catch (Exception ex)
                {
                    _logger.Error("Failed to create MMDeviceEnumerator COM object", ex);
                }
            }
            return _enumerator;
        }

        public void Start()
        {
            try
            {
                var enumerator = EnsureEnumerator();
                if (enumerator != null)
                {
                    enumerator.RegisterEndpointNotificationCallback(this);
                    _logger.Info("Registered CoreAudio IMMNotificationClient callback successfully.");
                }

                // Immediate initial synchronization
                TriggerSync(delayMs: 100);

                // Polling fallback timer in case of system sleep/hibernate resume
                var fallbackInterval = Math.Max(5, _settings.PollingFallbackSeconds) * 1000;
                _pollingTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        SyncCurrentState(logStateChanges: false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Debug($"Polling fallback error: {ex.Message}");
                    }
                }, null, fallbackInterval, fallbackInterval);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to start AudioDeviceWatcher", ex);
            }
        }

        public void TriggerSync(int delayMs = 500)
        {
            lock (_stateLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer(_ =>
                {
                    try
                    {
                        SyncCurrentState(logStateChanges: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Error during audio state sync", ex);
                    }
                }, null, delayMs, Timeout.Infinite);
            }
        }

        public void SyncCurrentState(bool logStateChanges = true)
        {
            var enumerator = EnsureEnumerator();
            if (enumerator == null) return;

            lock (_stateLock)
            {
                try
                {
                    var foundDevice = FindMaxwellCaptureDevice();

                    if (foundDevice != null && foundDevice.State == DeviceState.Active)
                    {
                        var wasDisconnected = !IsMaxwellConnected;
                        IsMaxwellConnected = true;
                        CurrentMaxwellDevice = foundDevice;

                        // Apply Boost and Enforce Volume
                        var boostOk = _apoManager.EnsureApoConfig(_settings.DeviceNameFilter, _settings.GainDb);
                        _apoManager.EnsureRegistryHooks(foundDevice);

                        // Warm up endpoint and get COM device instance
                        var hr = enumerator.GetDevice(foundDevice.Id, out var immDevice);
                        float volLevel = 1.0f;
                        if (hr == 0 && immDevice != null)
                        {
                            _apoManager.WarmupEndpointStream(immDevice);

                            if (_settings.EnforceVolume)
                            {
                                var volResult = _volumeEnforcer.EnforceVolume(immDevice, _settings.TargetVolumeScalar);
                                volLevel = volResult.LevelScalar;
                            }
                        }

                        CurrentVolumeLevel = volLevel;

                        if (wasDisconnected || logStateChanges)
                        {
                            _logger.Info($"Maxwell Connected & Boosted! Target: [{foundDevice.FriendlyName}], Gain: +{_settings.GainDb} dB, Volume: {volLevel:P0}");
                            OnDeviceBoosted?.Invoke(foundDevice, volLevel, wasDisconnected);
                            OnStatusChanged?.Invoke($"Connected (+{_settings.GainDb} dB)");
                        }
                    }
                    else
                    {
                        if (IsMaxwellConnected)
                        {
                            IsMaxwellConnected = false;
                            var prevName = CurrentMaxwellDevice?.FriendlyName ?? "Chat-Audeze Maxwell";
                            CurrentMaxwellDevice = null;

                            if (logStateChanges)
                            {
                                _logger.Warn($"Maxwell Disconnected / Inactive: [{prevName}]");
                                OnDeviceDisconnected?.Invoke(prevName);
                                OnStatusChanged?.Invoke("Disconnected (Standby)");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error in SyncCurrentState", ex);
                }
            }
        }

        public List<AudioDeviceInfo> GetAllCaptureDevices()
        {
            var result = new List<AudioDeviceInfo>();
            var enumerator = EnsureEnumerator();
            if (enumerator == null) return result;

            var hr = enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DeviceState.All, out var collection);
            if (hr != 0 || collection == null) return result;

            collection.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) == 0 && device != null)
                {
                    var info = GetDeviceInfo(device, EDataFlow.eCapture);
                    if (info != null)
                    {
                        result.Add(info);
                    }
                }
            }

            return result;
        }

        private AudioDeviceInfo? FindMaxwellCaptureDevice()
        {
            var enumerator = EnsureEnumerator();
            if (enumerator == null) return null;

            var hr = enumerator.EnumAudioEndpoints(EDataFlow.eCapture, DeviceState.Active, out var collection);
            if (hr != 0 || collection == null) return null;

            collection.GetCount(out var count);
            for (uint i = 0; i < count; i++)
            {
                if (collection.Item(i, out var device) == 0 && device != null)
                {
                    var info = GetDeviceInfo(device, EDataFlow.eCapture);
                    if (info != null && MatchesMaxwell(info))
                    {
                        return info;
                    }
                }
            }

            return null;
        }

        private bool MatchesMaxwell(AudioDeviceInfo info)
        {
            var filter = _settings.DeviceNameFilter;
            return info.FriendlyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                   info.DeviceDesc.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                   info.Id.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        private AudioDeviceInfo? GetDeviceInfo(IMMDevice device, EDataFlow flow)
        {
            try
            {
                device.GetId(out var id);
                device.GetState(out var state);

                var info = new AudioDeviceInfo
                {
                    Id = id,
                    State = state,
                    Flow = flow
                };

                if (device.OpenPropertyStore(StorageAccessMode.Read, out var propStore) == 0 && propStore != null)
                {
                    var keyFriendly = PROPERTYKEY.PKEY_Device_FriendlyName;
                    if (propStore.GetValue(ref keyFriendly, out var pvFriendly) == 0)
                    {
                        info.FriendlyName = pvFriendly.GetString() ?? string.Empty;
                    }

                    var keyDesc = PROPERTYKEY.PKEY_Device_DeviceDesc;
                    if (propStore.GetValue(ref keyDesc, out var pvDesc) == 0)
                    {
                        info.DeviceDesc = pvDesc.GetString() ?? string.Empty;
                    }
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        #region IMMNotificationClient Callbacks

        public void OnDeviceStateChanged(string pwstrDeviceId, DeviceState dwNewState)
        {
            _logger.Debug($"CoreAudio Event: OnDeviceStateChanged -> ID: {pwstrDeviceId}, State: {dwNewState}");
            TriggerSync(delayMs: 300);
        }

        public void OnDeviceAdded(string pwstrDeviceId)
        {
            _logger.Debug($"CoreAudio Event: OnDeviceAdded -> ID: {pwstrDeviceId}");
            TriggerSync(delayMs: 500);
        }

        public void OnDeviceRemoved(string pwstrDeviceId)
        {
            _logger.Debug($"CoreAudio Event: OnDeviceRemoved -> ID: {pwstrDeviceId}");
            TriggerSync(delayMs: 300);
        }

        public void OnDefaultDeviceChanged(EDataFlow flow, ERole role, string pwstrDefaultDeviceId)
        {
            if (flow == EDataFlow.eCapture)
            {
                _logger.Debug($"CoreAudio Event: OnDefaultDeviceChanged -> Capture Default changed to {pwstrDefaultDeviceId}");
                TriggerSync(delayMs: 200);
            }
        }

        public void OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key)
        {
            // Ignore minor property updates unless relevant
        }

        #endregion

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _debounceTimer?.Dispose();
            _pollingTimer?.Dispose();

            try
            {
                _enumerator?.UnregisterEndpointNotificationCallback(this);
            }
            catch
            {
                // Ignored during cleanup
            }
        }
    }
}
