using System;
using System.Text.RegularExpressions;

namespace MaxwellBoost.CoreAudio
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string DeviceDesc { get; set; } = string.Empty;
        public DeviceState State { get; set; }
        public EDataFlow Flow { get; set; }

        public string EndpointGuid
        {
            get
            {
                // Format is usually {0.0.1.00000000}.{59226142-4778-492f-aa01-6e477cd0bafd}
                var match = Regex.Match(Id, @"\{[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\}$");
                if (match.Success)
                {
                    return match.Value;
                }

                var anyGuid = Regex.Match(Id, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                if (anyGuid.Success)
                {
                    return "{" + anyGuid.Value + "}";
                }

                return string.Empty;
            }
        }

        public override string ToString()
        {
            return $"{FriendlyName} [{State}] (GUID: {EndpointGuid})";
        }
    }
}
