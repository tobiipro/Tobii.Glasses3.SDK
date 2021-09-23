using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using G3SDK;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3Simulator
{
    public class G3Simulator : IG3Api
    {
        private readonly DateTime _simStarted;

        public G3Simulator()
        {
            Calibrate = new CalibrateSimulator(this);
            Settings = new SettingsSimulator(this);
            Recorder = new RecorderSimulator(this);
            InternalRecordings = new RecordingsSimulator();
            System = new SystemSimulator(this);
            Rudimentary = new RudimentarySimulator(this);
            _simStarted = DateTime.Now;
            IpAddress = "127.0.0.1";
            IpAddress = "192.168.1.228";
            WebRTC = new WebRTCSimulator(this);
            Upgrade = new UpgradeSimulator(this);

        }

        public TimeSpan GetTimestamp()
        {
            return DateTime.Now - _simStarted;
        }

        public ICalibrate Calibrate { get; }
        public IRecorder Recorder { get; }
        public ISettings Settings { get; }
        public ISystem System { get; }
        public string IpAddress { get; }
        public string LiveRtspUrl(bool gazeOverlay = false)
        {
            return null;
        }

        public Uri LiveRtspUri(bool gazeOverlay = false)
        {
            var url = LiveRtspUrl(gazeOverlay);
            if (url == null)
                return null;
            return new Uri(url);
        }

        public IRudimentary Rudimentary { get; }
        public IRecordings Recordings => InternalRecordings;
        public IUpgrade Upgrade { get; }
        public LogLevel LogLevel { get; set; }
        public INetwork Network { get; }
        public IWebRTC WebRTC { get; }
        public RecordingsSimulator InternalRecordings { get; }
    }

    public class UpgradeSimulator : IUpgrade
    {
        private readonly G3Simulator _g3Simulator;

        public UpgradeSimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
            Completed = new SignalSimulator<bool>();
            Progress = new SignalSimulator<UpgradeState>();
        }

        public IG3Observable<bool> Completed { get; }
        public IG3Observable<UpgradeState> Progress { get; }
        public Task<bool> InProgress => Task.FromResult(false);
    }

    public class WebRTCSimulator : IWebRTC
    {
        private readonly G3Simulator _g3Simulator;
        private List<WebRTCSessionSim> _sessions = new List<WebRTCSessionSim>();

        public WebRTCSimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
        }

        public Task<IWebRTCSession> Create()
        {
            var session = new WebRTCSessionSim(_g3Simulator);
            _sessions.Add(session);
            return Task.FromResult<IWebRTCSession>(session);
        }

        public Task<bool> Delete(IWebRTCSession session)
        {
            foreach (var s in _sessions.ToArray())
                if (session.Guid == s.Guid)
                    _sessions.Remove(s);
            return Task.FromResult(true);
        }

        public Task<List<IWebRTCSession>> Children()
        {
            return Task.FromResult(new List<IWebRTCSession>(_sessions));
        }
    }

    public class WebRTCSessionSim : IWebRTCSession
    {
        private readonly G3Simulator _g3Simulator;
        private bool _iFrameStream;
        private string _stunServer;
        private string _turnServer;
        private readonly Guid _guid = Guid.NewGuid();
        private readonly SignalSimulator<Notification> _timedOut = new SignalSimulator<Notification>();
        private static readonly SignalSimulator<IceCandidate> _newIceCandidates = new SignalSimulator<IceCandidate>();
        private readonly Timer _sessionTimer;
        private readonly List<IceCandidate> _candidates = new List<IceCandidate>();

        public WebRTCSessionSim(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
            _sessionTimer = new Timer(5000);
            _sessionTimer.Elapsed += (sender, args) =>
            {
                _timedOut.Emit(new Notification());
                _g3Simulator.WebRTC.Delete(this);
            };
        }

        public Task<bool> IFrameStream => Task.FromResult(_iFrameStream);
        public Task<string> StunServer => Task.FromResult(_stunServer);
        public Task<string> TurnServer => Task.FromResult(_turnServer);
        public Task<int> CurrentGazeFrequency => _g3Simulator.Settings.GazeFrequency;
        public Guid Guid => _guid;
        public IG3Observable<G3SyncPortData> SyncPort => _g3Simulator.Rudimentary.SyncPort;
        public IObservable<G3GazeData> Gaze => _g3Simulator.Rudimentary.Gaze;
        public IObservable<G3Event> Event => _g3Simulator.Rudimentary.Event;
        public IObservable<G3ImuData> Imu => _g3Simulator.Rudimentary.Imu;
        public IObservable<Notification> TimedOut => _timedOut;
        public IObservable<IceCandidate> NewIceCandidate => _newIceCandidates;

        public Task<bool> SetIframeStream(bool value)
        {
            _iFrameStream = value;
            return Task.FromResult(true);
        }

        public Task<bool> SetStunServer(string value)
        {
            _stunServer = value;
            return Task.FromResult(true);
        }

        public Task<bool> SetTurnServer(string value)
        {
            _turnServer = value;
            return Task.FromResult(true);
        }

        public Task Keepalive()
        {
            _sessionTimer.Stop();
            _sessionTimer.Start();
            return Task.CompletedTask;
        }

        public Task<bool> Start(string offer)
        {
            return Task.FromResult(true);
        }

        public Task<string> Setup()
        {
            _newIceCandidates.Emit(new IceCandidate(0, "WebRTC sim candidate"));
            return Task.FromResult("WebRTC simulator answer");
        }

        public Task<string[]> GetIceCandidates()
        {
            return Task.FromResult(new string[0]);
        }

        public Task AddIceCandidate(IceCandidate candidate)
        {
            _candidates.Add(candidate);
            return Task.CompletedTask;
        }

        public Task<bool> SendEvent(string tag, object obj)
        {
            _g3Simulator.Rudimentary.SendEvent(tag, obj);
            return Task.FromResult(true);
        }
    }

    public class RudimentarySimulator : IRudimentary
    {
        private readonly G3Simulator _g3Simulator;
        private int _sceneScale = 1;
        private G3GazeData _lastGaze;
        private G3ImuData _lastImu;
        private G3Event _lastEvent;
        private G3SyncPortData _lastSyncPort;
        private int _sceneQuality = 25;
        private readonly SignalSimulator<byte[]> _sceneSig = new SignalSimulator<byte[]>();
        private readonly SignalSimulator<G3SyncPortData> _syncPortSig = new SignalSimulator<G3SyncPortData>();
        private readonly SignalSimulator<G3ImuData> _imuSig = new SignalSimulator<G3ImuData>();
        private readonly SignalSimulator<G3Event> _eventSig = new SignalSimulator<G3Event>();
        private readonly SignalSimulator<G3GazeData> _gazeSig = new SignalSimulator<G3GazeData>();
        private Timer _dataTimer = new Timer();
        private Stopwatch _dataTimeout = new Stopwatch();

        public RudimentarySimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
            _dataTimer.Interval = 10;
            _dataTimer.Elapsed += (sender, args) => SendData();
        }

        private void SendData()
        {
            if (_dataTimeout.ElapsedMilliseconds > 6000)
            {
                _dataTimer.Enabled = false;
                return;
            }

            var ts = _g3Simulator.GetTimestamp();
            _lastGaze = new G3GazeData(
                _g3Simulator.GetTimestamp(),
                new Vector2(0.5f, 0.4f),
                new Vector3(0.5f, 0.4f, 0.3f),
                new G3GazeData.EyeData(new Vector3(1, 2, 3),
                    new Vector3(3, 4, 5),
                    4),
                new G3GazeData.EyeData(new Vector3(1, 2, 3),
                    new Vector3(3, 4, 5),
                    4));


            _gazeSig.Emit(_lastGaze);
            _lastImu = new G3ImuData(_g3Simulator.GetTimestamp(),
                new Vector3(0, -10, 0),
                new Vector3(0, 0, 0),
                new Vector3((float) Math.Sin(ts.TotalSeconds) * 100 + 300,
                    (float) Math.Sin(ts.TotalSeconds + 1) * 150 - 200,
                    (float) Math.Sin(ts.TotalSeconds + 2) * 50 - 40));
            _imuSig.Emit(_lastImu);
        }

        public Task<G3GazeData> GazeSample => Task.FromResult(_lastGaze);
        public Task<G3ImuData> ImuSample => Task.FromResult(_lastImu);
        public Task<G3Event> EventSample => Task.FromResult(_lastEvent);
        public Task<G3SyncPortData> SyncPortSample => Task.FromResult(_lastSyncPort);
        public Task<int> SceneScale => Task.FromResult(_sceneScale);
        public Task<int> SceneQuality => Task.FromResult(_sceneQuality);
        public IG3Observable<byte[]> Scene => _sceneSig;
        public IG3Observable<G3SyncPortData> SyncPort => _syncPortSig;
        public IG3Observable<G3GazeData> Gaze => _gazeSig;
        public IG3Observable<G3Event> Event => _eventSig;
        public IG3Observable<G3ImuData> Imu => _imuSig;
        public Task<bool> SetSceneQuality(int quality)
        {
            _sceneQuality = quality;
            return Task.FromResult(true);
        }

        public Task<bool> SetSceneScale(int scale)
        {
            _sceneScale = scale;
            return Task.FromResult(true);
        }

        public Task<bool> Keepalive()
        {
            _dataTimer.Enabled = true;
            _dataTimeout.Restart();
            return Task.FromResult(true);
        }

        public Task<bool> Calibrate()
        {
            return _g3Simulator.Calibrate.Run();
        }

        public Task<bool> SendEvent(string tag, object obj)
        {
            _lastEvent = new G3Event(
                _g3Simulator.GetTimestamp(),
                tag,
                JsonConvert.SerializeObject(obj));
            _eventSig.Emit(_lastEvent);
            return Task.FromResult(true);
        }
    }

    public class SystemSimulator : ISystem
    {
        private readonly G3Simulator _g3Simulator;
        private TimeSpan _simTimeOffset;
        private bool _ntpIsEnabled;
        private string _timeZone;
        private readonly G3Version _version = new G3Version(G3Version.Latest + "+Sim");
        private readonly string _ruSerial = "TG03B-12342353125";
        private readonly string _huSerial = "TG03H-123123123123";
        private bool _ntpIsSynchronized = true;

        public SystemSimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
            Battery = new BatterySimulator(g3Simulator);
            Storage = new StorageSimulator(g3Simulator);
        }

        public IBattery Battery { get; }
        public IStorage Storage { get; }
        public Task<string> Version => Task.FromResult(_version.ToString());
        public Task<string> RecordingUnitSerial => Task.FromResult(_ruSerial);
        public Task<string> HeadUnitSerial => Task.FromResult(_huSerial);
        public Task<string> TimeZone => Task.FromResult(_timeZone);
        public Task<bool> NtpIsEnabled => Task.FromResult(_ntpIsEnabled && _ntpIsEnabled);
        public Task<bool> NtpIsSynchronized => Task.FromResult(_ntpIsSynchronized);
        public Task<DateTime> Time => Task.FromResult(DateTime.UtcNow + _simTimeOffset);
        public Task<bool> SetTime(DateTime value)
        {
            if (_ntpIsEnabled)
                return Task.FromResult(false);
            _simTimeOffset = DateTime.UtcNow - value;
            return Task.FromResult(true);
        }

        public Task<bool> UseNtp(bool value)
        {
            _ntpIsEnabled = value;
            return Task.FromResult(true);
        }

        public Task<bool> SetTimezone(string tz)
        {
            _timeZone = tz;
            return Task.FromResult(true);
        }

        public Task<int[]> AvailableGazeFrequencies()
        {
            return Task.FromResult(new[] { 50, 100 });
        }
    }

    public class BatterySimulator : IBattery
    {
        private readonly G3Simulator _g3Simulator;

        private readonly SignalSimulator<(BatteryState State, bool Charging)> _stateChanged = new SignalSimulator<(BatteryState State, bool Charging)>();

        public BatterySimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
        }

        public IG3Api G3Api => _g3Simulator;
        public Task<float> Level => Task.FromResult(1f);
        public Task<TimeSpan> RemainingTime => Task.FromResult(TimeSpan.FromHours(1.75));
        public Task<bool> Charging => Task.FromResult(true);
        public Task<BatteryState> State => Task.FromResult(BatteryState.full);
        public IG3Observable<(BatteryState State, bool Charging)> StateChanged => _stateChanged;
    }

    public class StorageSimulator : IStorage
    {
        private readonly G3Simulator _g3Simulator;
        private long _free = 512 * 1024 * 1024; // 512MB
        private long _size = 1024 * 1024 * 1024; // 1GB
        private readonly SignalSimulator<bool> _busyChanged = new SignalSimulator<bool>();

        private SignalSimulator<(SpaceState spaceState, CardState cardState)> _stateChanged = new SignalSimulator<(SpaceState spaceState, CardState cardState)>();

        public StorageSimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
        }

        public IG3Api G3Api => _g3Simulator;
        public Task<long> Free => Task.FromResult(_free);
        public Task<long> Size => Task.FromResult(_size);
        public Task<bool> Busy => Task.FromResult(false);
        public Task<TimeSpan> RemainingTime => Task.FromResult(TimeSpan.FromHours(2));
        public Task<SpaceState> SpaceState => Task.FromResult(G3SDK.SpaceState.Good);
        public Task<CardState> CardState => Task.FromResult(G3SDK.CardState.Available);
        public IG3Observable<(SpaceState spaceState, CardState cardState)> StateChanged => _stateChanged;
        public IG3Observable<bool> BusyChanged => _busyChanged;
    }

    public class DynamicChildNode : IDynamicChildNode
    {
        protected readonly SignalSimulator<string> _childRemoved = new SignalSimulator<string>();
        protected readonly SignalSimulator<string> _childAdded = new SignalSimulator<string>();
        public IG3Observable<string> ChildRemoved => _childRemoved;
        public IG3Observable<string> ChildAdded => _childAdded;

    }
    public class RecordingsSimulator : DynamicChildNode, IRecordings
    {
        private readonly List<RecordingSimulator> _recordings = new List<RecordingSimulator>();
        private readonly SignalSimulator<Guid> _deleted = new SignalSimulator<Guid>();
        private readonly SignalSimulator<Notification> _scanDone = new SignalSimulator<Notification>();
        private SignalSimulator<Notification> _scanStart = new SignalSimulator<Notification>();

        public Task<bool> Delete(Guid uuid)
        {
            var rec = _recordings.FirstOrDefault(r => r.UUID == uuid);
            if (rec == null)
                return Task.FromResult(false);
            _deleted.Emit(uuid);
            _childRemoved.Emit(uuid.ToString());
            _recordings.Remove(rec);
            return Task.FromResult(true);
        }

        public IG3Observable<Guid> Deleted => _deleted;
        public IG3Observable<Notification> ScanStart => _scanStart;
        public IG3Observable<Notification> ScanDone => _scanDone;
        public Task<List<IRecording>> Children()
        {
            return Task.FromResult(new List<IRecording>(_recordings));
        }

        internal void AddRecording(RecordingSimulator rec)
        {
            _recordings.Add(rec);
            _childAdded.Emit(rec.UUID.ToString());
        }
    }

    internal class RecordingSimulator : IRecording
    {
        private readonly Dictionary<string, string> _meta;
        private readonly G3Simulator _sim;
        private string _folder;
        private string _visibleName;
        private Guid _uuid;
        private string _rtspPath;
        private string _httpPath;
        private readonly int _validGazeSamples;
        private readonly int _gazeSamples;
        private readonly TimeSpan _duration;
        private readonly DateTime _created;
        private readonly bool _gazeOverlay;
        private readonly string _timeZone;


        public RecordingSimulator(G3Simulator sim, Guid uuid, string folder,
            string visibleName, bool gazeOverlay, int gazeSamples, int validGazeSamples,
            DateTime? recordingStart, string timeZone, TimeSpan duration, Dictionary<string, string> meta)
        {
            _sim = sim;
            _uuid = uuid;
            _folder = folder;
            _visibleName = visibleName;
            _gazeOverlay = gazeOverlay;
            _gazeSamples = gazeSamples;
            _validGazeSamples = validGazeSamples;
            _timeZone = timeZone;
            _duration = duration;
            _created = recordingStart.Value;
            _meta = meta;

        }

        public IG3Api G3Api => _sim;

        public Task<bool> MetaInsert(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                _meta.Remove(key);
            else
                _meta[key] = value;
            return Task.FromResult(true);
        }

        public Task<bool> MetaInsert(string key, byte[] data)
        {
            return MetaInsert(key, Convert.ToBase64String(data));
        }

        public Task<string> MetaLookupString(string key)
        {
            return Task.FromResult(_meta[key]);
        }

        public Task<byte[]> MetaLookup(string key)
        {
            var s = _meta[key];
            var bytes = Convert.FromBase64String(s);
            return Task.FromResult(bytes);

        }

        public Task<string[]> MetaKeys()
        {
            return Task.FromResult(_meta.Keys.ToArray());
        }

        public Task<string> Folder => Task.FromResult(_folder);
        public Task<string> VisibleName => Task.FromResult(_visibleName);
        public Task<string> TimeZone => Task.FromResult(_timeZone);
        public Task<bool> GazeOverlay => Task.FromResult(_gazeOverlay);
        public Task<DateTime> Created => Task.FromResult(_created);
        public Task<TimeSpan> Duration => Task.FromResult(_duration);
        public Task<int> GazeSamples => Task.FromResult(_gazeSamples);
        public Task<int> ValidGazeSamples => Task.FromResult(_validGazeSamples);
        public Task<string> HttpPath => Task.FromResult(_httpPath);
        public Task<string> RtspPath => Task.FromResult(_rtspPath);
        public Guid UUID => _uuid;
        public Task<bool> SetVisibleName(string value)
        {
            _visibleName = value;
            return Task.FromResult(true);
        }

        public Task<bool> Move(string folderName)
        {
            throw new NotImplementedException();
        }

        public Task<List<G3GazeData>> GazeData()
        {
            throw new NotImplementedException();
        }

        public Task<List<G3Event>> Events()
        {
            throw new NotImplementedException();
        }

        public Task<(ConcurrentQueue<G3GazeData>, Task)> GazeDataAsync()
        {
            return Task.FromResult((new ConcurrentQueue<G3GazeData>(), Task.CompletedTask));
        }

        public Task<(JObject json, string httpPath)> GetRecordingJson()
        {
            var jObject = new JObject();
            var sceneCam = new JObject();
            sceneCam["snapshots"] = new JArray();
            jObject["scenecamera"] = sceneCam;
            //TODO: fill json with real values
            return Task.FromResult((jObject, _httpPath));
        }

        public Task<Uri> GetUri(string fileName)
        {
            return Task.FromResult(new Uri(Path.Combine("c:\\temp", fileName)));
        }
    }

    public class RecorderSimulator : IRecorder
    {
        private readonly G3Simulator _sim;
        private bool _isRecording;
        private readonly SignalSimulator<Guid> _started;
        private readonly SignalSimulator<string> _stopped;
        private string _folder;
        private bool _gazeOverlay;
        private string _timeZone;
        private string _visibleName;
        private int _gazeSamples;
        private int _validGazeSamples;
        private Guid _uuid;
        private DateTime? _recordingStart;
        private readonly List<TimeSpan> _snapshots = new List<TimeSpan>();
        private Dictionary<string, string> _meta = new Dictionary<string, string>();

        public RecorderSimulator(G3Simulator sim)
        {
            _sim = sim;
            _started = new SignalSimulator<Guid>();
            _stopped = new SignalSimulator<string>();
        }

        public Task<bool> SendEvent(string tag, object o)
        {
            if (_isRecording)
            {
                _sim.Rudimentary.SendEvent(tag, o);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);

        }

        public IG3Observable<string> Stopped => _stopped;
        public IG3Observable<Guid> Started => _started;
        public Task<string> Folder => Task.FromResult(_folder);
        public Task<bool> GazeOverlay => Task.FromResult(_gazeOverlay);
        public Task<string> TimeZone => Task.FromResult(_timeZone);
        public Task<string> VisibleName => Task.FromResult(_visibleName);
        public Task<int> GazeSamples => Task.FromResult(_gazeSamples);
        public Task<int> ValidGazeSamples => Task.FromResult(_validGazeSamples);
        public Task<Guid> UUID => Task.FromResult(_uuid);
        public Task<TimeSpan?> Duration => Task.FromResult(CalculateDuration());

        private TimeSpan? CalculateDuration()
        {
            return DateTime.UtcNow - _recordingStart;
        }

        public Task<TimeSpan> RemainingTime => Task.FromResult(TimeSpan.FromHours(1.45));
        public Task<DateTime?> Created => Task.FromResult(_recordingStart);
        public Task<int> CurrentGazeFrequency => _sim.Settings.GazeFrequency;
        public async Task<bool> Start()
        {
            if (_isRecording || await _sim.System.Storage.CardState != CardState.Available)
                return false;

            _recordingStart = DateTime.UtcNow;
            _gazeSamples = 0;
            _validGazeSamples = 0;
            _timeZone = await _sim.System.TimeZone;
            _isRecording = true;
            _folder = _recordingStart?.ToString("yyyyMMdd_HHmmss");
            _visibleName = null;
            _uuid = Guid.NewGuid();
            _meta.Clear();
            _meta["RuVersion"] = await _sim.System.Version;
            _meta["RuSerial"] = await _sim.System.RecordingUnitSerial;
            _meta["HuSerial"] = await _sim.System.HeadUnitSerial;
            _gazeOverlay = await _sim.Settings.GazeOverlay;
            _started.Emit(_uuid);
            return true;
        }

        public async Task<bool> Snapshot()
        {
            if (!_isRecording)
                return false;
            _snapshots.Add((await Duration).Value);
            return false;
        }

        public Task<bool> Stop()
        {
            if (!_isRecording)
            {
                return Task.FromResult(false);
            }
            var rec = new RecordingSimulator(_sim, _uuid, _folder, _visibleName,
                _gazeOverlay, _gazeSamples, _validGazeSamples,
                _recordingStart, _timeZone, CalculateDuration().Value, _meta);
            _sim.InternalRecordings.AddRecording(rec);
            ResetFields();
            _stopped.Emit(rec.UUID.ToString());
            return Task.FromResult(true);
        }

        private void ResetFields()
        {
            _isRecording = false;
            _uuid = Guid.Empty;
            _folder = null;
            _visibleName = null;
            _recordingStart = null;
            _gazeSamples = 0;
            _validGazeSamples = 0;
        }

        public Task Cancel()
        {
            if (!_isRecording)
            {
                return Task.FromResult(false);
            }

            var uuid = _uuid;
            ResetFields();

            _stopped.Emit(uuid.ToString());
            return Task.FromResult(true);
        }

        public Task<bool> SetFolder(string value)
        {
            _folder = value;
            return Task.FromResult(true);
        }

        public Task<bool> SetVisibleName(string value)
        {
            _visibleName = value;
            return Task.FromResult(true);
        }

        public Task<bool> MetaInsert(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                _meta.Remove(key);
            else
                _meta[key] = value;
            return Task.FromResult(true);
        }

        public Task<bool> MetaInsert(string key, byte[] data)
        {
            return MetaInsert(key, Convert.ToBase64String(data));
        }

        public Task<string[]> MetaKeys()
        {
            return Task.FromResult(_meta.Keys.ToArray());
        }

        public Task<string> MetaLookupString(string key)
        {
            return Task.FromResult(_meta[key]);
        }

        public Task<byte[]> MetaLookup(string key)
        {
            var s = _meta[key];
            var bytes = Convert.FromBase64String(s);
            return Task.FromResult(bytes);
        }

        public Task<bool> RecordingInProgress()
        {
            return Task.FromResult(_isRecording);
        }
    }

    public class SettingsSimulator : ISettings
    {
        private bool _gazeOverlay;
        private readonly SignalSimulator<string> _changed;
        private int _gazeFrequency;

        public SettingsSimulator(G3Simulator g3Simulator)
        {
            _changed = new SignalSimulator<string>();
            _gazeFrequency = 50;
        }

        public IG3Observable<string> Changed => _changed;
        public Task<bool> GazeOverlay => Task.FromResult(_gazeOverlay);
        public Task<int> GazeFrequency => Task.FromResult(_gazeFrequency);
        public Task<bool> SetGazeOverlay(bool value)
        {
            _gazeOverlay = value;
            _changed.Emit("gaze-overlay");
            return Task.FromResult(true);
        }

        public Task<bool> SetGazeFrequency(int value)
        {
            _gazeFrequency = value;
            _changed.Emit("gaze-frequency");
            return Task.FromResult(true);
        }
    }

    public class CalibrateSimulator : ICalibrate
    {
        private readonly G3Simulator _g3Simulator;
        private readonly SignalSimulator<G3MarkerData> _marker;
        private readonly Stopwatch _markerTimeout = new Stopwatch();
        private readonly Timer _markerTimer = new Timer();

        public CalibrateSimulator(G3Simulator g3Simulator)
        {
            _g3Simulator = g3Simulator;
            _marker = new SignalSimulator<G3MarkerData>();
            _markerTimer.Interval = 1000d / 25;
            _markerTimer.Elapsed += _markerTimer_Elapsed;
        }

        private void _markerTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_markerTimeout.ElapsedMilliseconds > 3000)
            {
                _markerTimer.Enabled = false;
            }
            else
            {
                _marker.Emit(new G3MarkerData(_g3Simulator.GetTimestamp(), Vector2.Zero, Vector3.Zero));
            }
        }

        public Task<bool> EmitMarkers()
        {
            _markerTimeout.Restart();
            _markerTimer.Enabled = true;
            return Task.FromResult(true);
        }

        public IG3Observable<G3MarkerData> Marker => _marker;
        public Task<bool> Run()
        {
            return Task.FromResult(true);
        }
    }

    public class SignalSimulator<T> : IG3Observable<T>
    {
        private HashSet<IObserver<T>> _observers = new HashSet<IObserver<T>>();

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new Unsubscriber(this, observer);
        }
        public Task<IDisposable> SubscribeAsync(IObserver<T> observer)
        {
            return Task.FromResult(Subscribe(observer));
        }

        public void Emit(T item)
        {
            foreach (var o in _observers)
                o.OnNext(item);
        }

        private class Unsubscriber : IDisposable
        {
            private readonly SignalSimulator<T> _sim;
            private readonly IObserver<T> _observer;

            public Unsubscriber(SignalSimulator<T> sim, IObserver<T> observer)
            {
                _sim = sim;
                _observer = observer;
            }

            public void Dispose()
            {
                _sim._observers.Remove(_observer);
            }
        }

        public bool IsSubscribed => _observers.Any();
    }
}