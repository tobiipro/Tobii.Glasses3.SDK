using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace G3SDK
{
    public class SystemObj : DynamicChildNode, ISystem
    {
        private readonly ROProperty _headUnitSerial;
        private readonly ROProperty _recordingUnitSerial;
        private readonly ROProperty<string> _version;
        private readonly ROProperty _timezone;
        private readonly ROProperty<DateTime> _time;
        private readonly ROProperty<bool> _ntpIsEnabled;
        private readonly ROProperty<bool> _ntpIsSynchronized;
        private readonly Storage _storage;
        private readonly Battery _battery;
        public IBattery Battery => _battery;
        public IStorage Storage => _storage;

        public SystemObj(G3Api g3Api) : base(g3Api, "system")
        {
            _version = AddROProperty("version", s =>
           {
                // firmware 0.7.1 has a trailing "\n" in the firmware value
                if (s.EndsWith("\\n"))
                   return s.Substring(0, s.Length - 2);
               return s;
           });
            _recordingUnitSerial = AddROProperty("recording-unit-serial");
            _timezone = AddROProperty("timezone");
            _time = AddROProperty<DateTime>("time", ParserHelpers.ParseDate);
            _headUnitSerial = AddROProperty("head-unit-serial");

            _ntpIsEnabled = AddROProperty<bool>("ntp-is-enabled", bool.Parse);
            _ntpIsSynchronized = AddROProperty<bool>("ntp-is-synchronized", bool.Parse);

            _battery = new Battery(g3Api, Path);
            _storage = new Storage(g3Api, Path);
        }

        #region Properties
        public Task<string> Version => _version.Value();
        public async Task<G3Version> G3Version()
        {
            return new G3Version(await Version);
        }

        public Task<string> RecordingUnitSerial => _recordingUnitSerial.GetString();
        public Task<string> HeadUnitSerial => _headUnitSerial.GetString();
        public Task<string> TimeZone => _timezone.GetString();
        public Task<bool> NtpIsEnabled => _ntpIsEnabled.Value();
        public Task<bool> NtpIsSynchronized => _ntpIsSynchronized.Value();
        public Task<DateTime> Time => _time.Value();
        #endregion

        public Task<bool> SetTime(DateTime value)
        {
            return G3Api.ExecuteCommandBool(Path, "set-time", LogLevel.info, value.ToString("O"));
        }

        public override async Task<IEnumerable<G3Object>> GetSDKChildren()
        {
            return await Task.FromResult(new G3Object[] { _battery, _storage });
        }

        public Task<bool> UseNtp(bool value)
        {
            return G3Api.ExecuteCommandBool(Path, "use-ntp", LogLevel.info, value.ToString().ToLower());
        }

        public Task<bool> SetTimezone(string tz)
        {
            return G3Api.ExecuteCommandBool(Path, "set-timezone", LogLevel.info, tz);
        }

        public Task<int[]> AvailableGazeFrequencies()
        {
            return G3Api.ExecuteCommand<int[]>(Path, "available-gaze-frequencies", LogLevel.info);
        }
    }

    public interface ISystem
    {
        IBattery Battery { get; }
        IStorage Storage { get; }
        Task<string> Version { get; }
        Task<string> RecordingUnitSerial { get; }
        Task<string> HeadUnitSerial { get; }
        Task<string> TimeZone { get; }
        Task<bool> NtpIsEnabled { get; }
        Task<bool> NtpIsSynchronized { get; }
        Task<DateTime> Time { get; }
        Task<bool> SetTime(DateTime value);
        Task<bool> UseNtp(bool value);
        Task<bool> SetTimezone(string tz);
        Task<int[]> AvailableGazeFrequencies();
    }
}