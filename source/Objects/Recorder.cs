using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Recorder: G3Object, IMetaDataCapable, IRecorder
    {
        private readonly RWProperty<string> _folder;
        private readonly RWProperty<string> _visibleName;
        private readonly ROProperty<Guid> _uuid;
        private readonly ROProperty<TimeSpan?> _duration;
        private readonly ROProperty<TimeSpan> _remainingTime;
        private readonly ROProperty<DateTime?> _created;
        private readonly ROProperty<int> _gazeSamples;
        private readonly ROProperty<int> _validGazeSamples;
        private readonly ROProperty _timezone;
        private readonly ROProperty<int> _currentGazeFrequency;
        private readonly ROProperty<bool> _gazeOverlay;


        public Recorder(G3Api g3Api): base(g3Api, "recorder")
        {
            _folder = AddRWProperty("folder");
            _visibleName = AddRWProperty("visible-name");

            _uuid = AddROProperty("uuid", ParserHelpers.ParseGuid);
            _timezone = AddROProperty("timezone");
            _duration = AddROProperty("duration", ParserHelpers.ParseDurationToTimespan);
            _gazeSamples = AddROProperty("gaze-samples", int.Parse);
            _validGazeSamples = AddROProperty("valid-gaze-samples", int.Parse);
            _created = AddROProperty("created", ParserHelpers.ParseDateOptional);
            _remainingTime = AddROProperty("remaining-time", ParserHelpers.ParseTimeSpan);
            _currentGazeFrequency = AddROProperty("current-gaze-frequency", int.Parse);
            _gazeOverlay = AddROProperty("gaze-overlay", bool.Parse);


            Started = AddSignal("started", ConvertGuid);
            Stopped = AddSignal("stopped", ConvertString);
        }

        private string ConvertString(List<JToken> arg)
        {
            return arg[0].Value<string>();
        }

        public IG3Observable<string> Stopped { get; }


        private Guid ConvertGuid(List<JToken> list)
        {
            var guidStr = list[0].Value<string>();
            return Guid.Parse(guidStr);
        }

        public IG3Observable<Guid> Started { get; }

        #region Actions
        public async Task<bool> Start()
        {
            return await G3Api.ExecuteCommandBool(Path, "start", LogLevel.info);
        }

        public async Task<bool> Snapshot()
        {
            return await G3Api.ExecuteCommandBool(Path, "snapshot", LogLevel.info);
        }
        
        public async Task<bool> Stop()
        {
            return await G3Api.ExecuteCommandBool(Path, "stop", LogLevel.info);
        }
        
        public async Task Cancel()
        {
            await G3Api.ExecuteCommand(Path, "cancel", LogLevel.info);
        }
        #endregion

        #region Properties
        public Task<string> Folder => _folder.Value();

        public Task<bool> GazeOverlay => _gazeOverlay.Value();

        public Task<string> TimeZone => _timezone.GetString();
        
        public Task<bool> SetFolder(string value)
        {
            return _folder.Set(value);
        }

        public Task<string> VisibleName => _visibleName.Value();

        public Task<bool> SetVisibleName(string value)
        {
            return _visibleName.Set(value);
        }


        public async Task<bool> SendEvent(string tag, object obj)
        {
            return await G3Api.ExecuteCommandBool(Path, "send-event", LogLevel.info, tag, obj);
        }

        public Task<int> GazeSamples => _gazeSamples.Value();
        public Task<int> ValidGazeSamples => _validGazeSamples.Value();
        public Task<Guid> UUID => _uuid.Value();
        public Task<TimeSpan?> Duration => _duration.Value();
        public Task<TimeSpan> RemainingTime => _remainingTime.Value();
        public Task<DateTime?> Created => _created.Value();

        public Task<int> CurrentGazeFrequency => _currentGazeFrequency.Value();

        #endregion

        public async Task<bool> MetaInsert(string key, string value)
        {
            return await MetaDataCapableHelpers.MetaInsert(G3Api, Path, key, value);
        }
        public async Task<bool> MetaInsert(string key, byte[] data)
        {
            return await MetaDataCapableHelpers.MetaInsert(G3Api, Path, key, data);
        }

        public async Task<string[]> MetaKeys()
        {
            return await MetaDataCapableHelpers.MetaKeys(G3Api, Path);
        }

        public async Task<string> MetaLookupString(string key)
        {
            return await MetaDataCapableHelpers.MetaLookupString(G3Api, Path, key);
        }
        public async Task<byte[]> MetaLookup(string key)
        {
            return await MetaDataCapableHelpers.MetaLookup(G3Api, Path, key);
        }
        public async Task<bool> RecordingInProgress()
        {
            var uuid = await UUID;
            return uuid != Guid.Empty;
        }
    }

}