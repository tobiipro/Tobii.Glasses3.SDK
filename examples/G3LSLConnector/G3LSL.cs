using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using G3SDK;
using LSL;

namespace G3LSLConnector
{
    public class G3LSL
    {
        public static readonly string Company = "Tobii Pro AB";
        public static readonly string GazeStreamName = "Tobii Pro Glasses 3";
        public static readonly string GazeStreamType = "Wearable eye tracker";
        public static readonly string GazeChannelType = "Eye tracking";

        private readonly G3Api _api;
        private readonly List<FieldInfo<G3GazeData>> _gazeFields;
        private readonly List<FieldInfo<G3ImuData>> _magFields;
        private readonly List<FieldInfo<G3ImuData>> _accFields;
        private readonly List<FieldInfo<G3ImuData>> _gyrFields;

        private StreamInfo _info;
        private StreamOutlet _outlet;
        private IDisposable _token;
        private Timer _timer;
        private RudimentaryTimeSync _timeSync;

        public G3LSL(G3Api api)
        {
            _api = api;
            _gazeFields = new List<FieldInfo<G3GazeData>> {
                new("gaze2d.x", "normalized", data => data.Gaze2D.X),
                new("gaze2d.y", "normalized", data => data.Gaze2D.Y),
                new("left-pupil", "mm", data => data.LeftEye?.PupilDiameter),
                new("right-pupil", "mm", data => data.RightEye?.PupilDiameter),
            };
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("gaze3d", "mm", data => data.Gaze3D));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("left-gaze-origin", "mm", data => data.LeftEye?.GazeOrigin));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("right-gaze-origin", "mm", data => data.RightEye?.GazeOrigin));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("left-gaze-direction", "unit vector", data => data.LeftEye?.GazeDirection));
            _gazeFields.AddRange(CreateFieldsForVector3<G3GazeData>("right-gaze-direction", "unit vector", data => data.RightEye?.GazeDirection));

            _magFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("magnetometer", "uT", data => data.Magnetometer));
            _gyrFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("gyro", "deg/s", data => data.Gyroscope));
            _accFields = new List<FieldInfo<G3ImuData>>(CreateFieldsForVector3<G3ImuData>("accelerometer", "m/s^2", data => data.Accelerometer));
        }

        private IEnumerable<FieldInfo<T>> CreateFieldsForVector3<T>(string fieldName, string unit, Func<T, Vector3?> func)
        {
            yield return new FieldInfo<T>(fieldName + ".x", unit, data => func(data)?.X);
            yield return new FieldInfo<T>(fieldName + ".y", unit, data => func(data)?.Y);
            yield return new FieldInfo<T>(fieldName + ".z", unit, data => func(data)?.Z);
        }

        private class FieldInfo<T>
        {
            public FieldInfo(string label, string unit, Func<T, float?> selector)
            {
                Label = label;
                Unit = unit;
                Selector = selector;
            }

            public string Label { get; }
            public string Unit { get; }
            public Func<T, float?> Selector { get; }
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
            _info.desc().append_child_value("manufacturer", Company);

            // create outlet for the stream
            _outlet = new StreamOutlet(_info);

            _token = await _api.Rudimentary.Gaze.SubscribeAsync(SendGaze);
            _token = await _api.Rudimentary.Imu.SubscribeAsync(SendImu);
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
                    .append_child_value("type", GazeChannelType);
        }

        private async void SendKeepAlive(object sender, ElapsedEventArgs e)
        {
            if (_outlet.have_consumers())
                await _api.Rudimentary.Keepalive();
        }

        public void StopStreaming()
        {
            _token.Dispose();
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
            _timer.Close();
            await _api.Disconnect();
            _outlet.Close();
            _outlet.Dispose();
            _info.Close();
            _info.Dispose();
        }
    }
}