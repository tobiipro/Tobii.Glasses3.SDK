using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Threading.Tasks;
using System.Timers;
using G3SDK;
using LSL;
using Newtonsoft.Json.Linq;

namespace G3LSLConnector
{
    public class G3LSL
    {
        public static readonly string Company = "Tobii AB";
        public static readonly string GazeStreamName = "Tobii Pro Glasses 3";
        public static readonly string GazeStreamType = "Wearable eye tracker";
        public static readonly string GazeChannelType = "Eye tracking";
        public static readonly string ImuChannelType = "Imu data";
        public static readonly string SyncEventChannelType = "Sync events";

        private readonly G3Api _api;
        private readonly List<FieldInfo<G3GazeData>> _gazeFields;
        private readonly List<FieldInfo<G3ImuData>> _magFields;
        private readonly List<FieldInfo<G3ImuData>> _accFields;
        private readonly List<FieldInfo<G3ImuData>> _gyrFields;

        private StreamInfo _info;
        private StreamOutlet _outlet;
        private IList<IDisposable> _tokens = new List<IDisposable>();
        private Timer _timer;
        private RudimentaryTimeSync _timeSync;
        private readonly List<FieldInfo<G3SyncPortData>> _syncEventsFields;

        public G3LSL(G3Api api)
        {
            _api = api;
            _gazeFields = new List<FieldInfo<G3GazeData>> {
                new("gaze2d.x", GazeChannelType,"normalized", data => data.Gaze2D.X),
                new("gaze2d.y", GazeChannelType,"normalized", data => data.Gaze2D.Y),
                new("left-pupil", GazeChannelType,"mm", data => data.LeftEye?.PupilDiameter),
                new("right-pupil", GazeChannelType,"mm", data => data.RightEye?.PupilDiameter),
            };
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("gaze3d", GazeChannelType, "mm", data => data.Gaze3D));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("left-gaze-origin", GazeChannelType, "mm", data => data.LeftEye?.GazeOrigin));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("right-gaze-origin", GazeChannelType, "mm", data => data.RightEye?.GazeOrigin));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("left-gaze-direction", GazeChannelType, "unit vector", data => data.LeftEye?.GazeDirection));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("right-gaze-direction", GazeChannelType, "unit vector", data => data.RightEye?.GazeDirection));

            _magFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("magnetometer", ImuChannelType, "uT", data => data.Magnetometer));
            _gyrFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("gyro", ImuChannelType, "deg/s", data => data.Gyroscope));
            _accFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("accelerometer", ImuChannelType, "m/s^2", data => data.Accelerometer));

            _syncEventsFields = new List<FieldInfo<G3SyncPortData>>
            {
                new FieldInfo<G3SyncPortData>("direction", SyncEventChannelType,"", data => (int)data.Direction),
                new FieldInfo<G3SyncPortData>("value", SyncEventChannelType,"", data => data.Value)
            };
        }

        private IEnumerable<FieldInfo<T>> CreateFieldsForVector3<T>(string fieldName, string channelType, string unit,
            Func<T, Vector3?> func)
        {
            yield return new FieldInfo<T>(fieldName + ".x", channelType, unit, data => func(data)?.X);
            yield return new FieldInfo<T>(fieldName + ".y", channelType, unit, data => func(data)?.Y);
            yield return new FieldInfo<T>(fieldName + ".z", channelType, unit, data => func(data)?.Z);
        }

        private class FieldInfo<T>
        {
            public FieldInfo(string label, string channelType, string unit, Func<T, float?> selector)
            {
                Label = label;
                Unit = unit;
                Selector = selector;
                ChannelType = channelType;
            }

            public string Label { get; }
            public string Unit { get; }
            public Func<T, float?> Selector { get; }
            public string ChannelType { get; set; }
        }

        public async Task Init()
        {

            var frequency = await _api.Settings.GazeFrequency;
            var serialNum = await _api.System.RecordingUnitSerial;

            _timeSync = new RudimentaryTimeSync(_api.Rudimentary, 5000);
            _timeSync.AddRef();
            _info = new StreamInfo(GazeStreamName, GazeStreamType, _gazeFields.Count, frequency, channel_format_t.cf_float32, serialNum);

            var channels = _info.desc().append_child("channels");
            AddChannel(channels, _gazeFields);
            AddChannel(channels, _gyrFields);
            AddChannel(channels, _magFields);
            AddChannel(channels, _accFields);
            AddChannel(channels, _syncEventsFields);
            _info.desc().append_child_value("manufacturer", Company);

            // create outlet for the stream
            _outlet = new StreamOutlet(_info);

            _tokens.Add(await _api.Rudimentary.Gaze.SubscribeAsync(SendGaze));
            _tokens.Add(await _api.Rudimentary.Imu.SubscribeAsync(SendImu));
            _tokens.Add(await _api.Rudimentary.SyncPort.SubscribeAsync(SendSyncPort));
            _timer = new Timer(3000);
            _timer.Elapsed += SendKeepAlive;
            _timer.Enabled = true;

        }

        private void AddChannel<T>(XMLElement channels, List<FieldInfo<T>> fields)
        {
            foreach (var f in fields)
                channels.append_child("channel")
                    .append_child_value("label", f.Label)
                    .append_child_value("unit", f.Unit)
                    .append_child_value("type", f.ChannelType);
        }

        private async void SendKeepAlive(object sender, ElapsedEventArgs e)
        {
            if (_outlet.have_consumers())
                await _api.Rudimentary.Keepalive();
        }

        public void StopStreaming()
        {
            foreach(var t in _tokens)
                t.Dispose();
            _tokens.Clear();
        }

        private void SendGaze(G3GazeData data)
        {
            SendData(data, _gazeFields);
        }

        private void SendImu(G3ImuData data)
        {
            if (data.Magnetometer.IsValid())
                SendData(data, _magFields);
            if (data.Gyroscope.IsValid())
                SendData(data, _gyrFields);
            if (data.Accelerometer.IsValid())
                SendData(data, _accFields);
        }

        private void SendSyncPort(G3SyncPortData data)
        {
            SendData(data, _syncEventsFields);
        }

        private void SendData<T>(T data, List<FieldInfo<T>> fields) where T : IG3TimeStamped
        {
            var ts = _timeSync.ConvertToSystemTime(data.TimeStamp);
            var latency = _timeSync.GetSystemTime() - ts;
            var latencyInSeconds = latency / 1000000d;
            var lslTime = LSL.LSL.local_clock();
            var lslTimeOfSample = lslTime - latencyInSeconds;
            _outlet.push_sample(fields.Select(f => f.Selector(data) ?? float.NaN).ToArray(), lslTimeOfSample);
        }

        public async void Close()
        {
            _timer.Stop();
            _timer.Close();
            await _api.Disconnect();
            _outlet.Close();
            _outlet.Dispose();
            _info.Close();
            _info.Dispose();
        }
    }
}