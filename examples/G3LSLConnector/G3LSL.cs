using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Runtime.InteropServices;
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

        public static readonly string GazeChannelType = "Eye tracking";
        public static readonly string ImuChannelType = "Imu data";
        public static readonly string SyncEventChannelType = "Sync events";
        private const double GyroAccFrequency = 117;
        private const double MagFrequency = 10;

        private readonly G3Api _api;
        private readonly List<FieldInfo<G3Gaze2dData>> _gaze2dFields;
        private readonly List<FieldInfo<G3PupilData>> _pupilFields;
        private readonly List<FieldInfo<G3Gaze3dData>> _gaze3dFields;
        private readonly List<FieldInfo<G3GazeOriginData>> _gazeOriginFields;
        private readonly List<FieldInfo<G3GazeDirectionData>> _gazeDirectionFields;
        private readonly List<FieldInfo<G3MagData>> _magFields;
        private readonly List<FieldInfo<G3AccData>> _accFields;
        private readonly List<FieldInfo<G3GyroData>> _gyrFields;

        private readonly List<StreamInfo> _allStreamInfo = new();
        private readonly Dictionary<Type, StreamOutlet> _allStreamOutlets = new();
        private readonly IList<IDisposable> _tokens = new List<IDisposable>();
        private Timer _timer;
        private RudimentaryTimeSync _timeSync;
        private readonly List<FieldInfo<G3SyncPortData>> _syncEventsFields;
        private List<string> _allStreamNames = new();

        public G3LSL(G3Api api)
        {
            _api = api;
            _gaze2dFields = new List<FieldInfo<G3Gaze2dData>>
            {
                new("gaze2d.x", GazeChannelType, "normalized", data => data.Data.Gaze2D.X),
                new("gaze2d.y", GazeChannelType, "normalized", data => data.Data.Gaze2D.Y),
            };
            _pupilFields = new List<FieldInfo<G3PupilData>>
            {
                new("left-pupil", GazeChannelType, "mm", data => data.Data.LeftEye?.PupilDiameter),
                new("right-pupil", GazeChannelType, "mm", data => data.Data.RightEye?.PupilDiameter),
            };

            _gaze3dFields = new List<FieldInfo<G3Gaze3dData>>(CreateFieldsForVector3<G3Gaze3dData>("gaze3d", GazeChannelType, "mm", data => data.Data.Gaze3D));

            _gazeOriginFields = new List<FieldInfo<G3GazeOriginData>>(CreateFieldsForVector3<G3GazeOriginData>("left-gaze-origin", GazeChannelType, "mm", data => data.Data.LeftEye?.GazeOrigin));
            _gazeOriginFields.AddRange(CreateFieldsForVector3<G3GazeOriginData>("right-gaze-origin", GazeChannelType, "mm", data => data.Data.RightEye?.GazeOrigin));

            _gazeDirectionFields = new List<FieldInfo<G3GazeDirectionData>>(CreateFieldsForVector3<G3GazeDirectionData>("left-gaze-direction", GazeChannelType, "unit vector", data => data.Data.LeftEye?.GazeDirection));
            _gazeDirectionFields.AddRange(CreateFieldsForVector3<G3GazeDirectionData>("right-gaze-direction", GazeChannelType, "unit vector", data => data.Data.RightEye?.GazeDirection));

            _magFields = new List<FieldInfo<G3MagData>>(CreateFieldsForVector3<G3MagData>("magnetometer", ImuChannelType, "uT", data => data.Mag));
            _gyrFields = new List<FieldInfo<G3GyroData>>(CreateFieldsForVector3<G3GyroData>("gyro", ImuChannelType, "deg/s", data => data.Gyro));
            _accFields = new List<FieldInfo<G3AccData>>(CreateFieldsForVector3<G3AccData>("accelerometer", ImuChannelType, "m/s^2", data => data.Accelerometer));

            _syncEventsFields = new List<FieldInfo<G3SyncPortData>>
            {
                new("direction", SyncEventChannelType, "", data => (int)data.Direction),
                new("value", SyncEventChannelType, "", data => data.Value)
            };
        }

        public IEnumerable<string> AllStreamNames()
        {
            return _allStreamNames;
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

            var gazeFrequency = await _api.Settings.GazeFrequency;
            var serialNum = await _api.System.RecordingUnitSerial;

            _timeSync = new RudimentaryTimeSync(_api.Rudimentary, 5000);
            _timeSync.AddRef();

            CreateOutlet(_gaze2dFields, typeof(G3Gaze2dData), gazeFrequency, serialNum, "Gaze2d", "Gaze2d");
            CreateOutlet(_pupilFields, typeof(G3PupilData), gazeFrequency, serialNum, "Pupil", "Pupil");
            CreateOutlet(_gaze3dFields, typeof(G3Gaze3dData), gazeFrequency, serialNum, "Gaze3d", "Gaze3d");
            CreateOutlet(_gazeOriginFields, typeof(G3GazeOriginData), gazeFrequency, serialNum, "GazeOrigin", "GazeOrigin");
            CreateOutlet(_gazeDirectionFields, typeof(G3GazeDirectionData), gazeFrequency, serialNum, "GazeDirection", "GazeDirection");


            CreateOutlet(_gyrFields, typeof(G3GyroData), GyroAccFrequency, serialNum, "Gyroscope", "Gyroscope");
            CreateOutlet(_accFields, typeof(G3AccData), GyroAccFrequency, serialNum, "Accelerometer", "Accelerometer");
            CreateOutlet(_magFields, typeof(G3MagData), MagFrequency, serialNum, "Magnetometer", "Magnetometer");
            CreateOutlet(_syncEventsFields, typeof(G3SyncPortData), MagFrequency, serialNum, "SyncInOut", "SyncInOut");


            _tokens.Add(await _api.Rudimentary.Gaze.SubscribeAsync(SendGaze));
            _tokens.Add(await _api.Rudimentary.Imu.SubscribeAsync(SendImu));
            _tokens.Add(await _api.Rudimentary.SyncPort.SubscribeAsync(SendSyncPort));
            _timer = new Timer(3000);
            _timer.Elapsed += SendKeepAlive;
            _timer.Enabled = true;
        }

        private void CreateOutlet<T>(List<FieldInfo<T>> fields, Type type, double frequency, string sourceid,
            string streamName, string streamType)
        {

            var gazeStreamInfo = new StreamInfo(streamName, streamType, fields.Count, frequency,
                channel_format_t.cf_float32, sourceid);

            var gazeChannels = gazeStreamInfo.desc().append_child("channels");
            AddChannel(gazeChannels, fields);
            gazeStreamInfo.desc().append_child_value("manufacturer", Company);
            // create outlet for the stream
            var gazeOutlet = new StreamOutlet(gazeStreamInfo);
            _allStreamInfo.Add(gazeStreamInfo);
            _allStreamOutlets[type] = gazeOutlet;
            _allStreamNames.Add(streamName);
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
            if (_allStreamOutlets.Values.Any(o => o.have_consumers()))
                await _api.Rudimentary.Keepalive();
        }

        public void StopStreaming()
        {
            foreach (var t in _tokens)
                t.Dispose();
            _tokens.Clear();
        }

        private void SendGaze(G3GazeData data)
        {
            SendData(new G3Gaze2dData(data), _gaze2dFields);
            SendData(new G3Gaze3dData(data), _gaze3dFields);
            SendData(new G3PupilData(data), _pupilFields);
            SendData(new G3GazeOriginData(data), _gazeOriginFields);
            SendData(new G3GazeDirectionData(data), _gazeDirectionFields);
        }

        private void SendImu(G3ImuData data)
        {
            if (data.Magnetometer.IsValid())
                SendData(new G3MagData(data.TimeStamp, data.Magnetometer), _magFields);
            if (data.Gyroscope.IsValid())
                SendData(new G3GyroData(data.TimeStamp, data.Gyroscope), _gyrFields);
            if (data.Accelerometer.IsValid())
                SendData(new G3AccData(data.TimeStamp, data.Accelerometer), _accFields);
        }

        private void SendSyncPort(G3SyncPortData data)
        {
            SendData(data, _syncEventsFields);
        }

        private void SendData<T>(T data, List<FieldInfo<T>> fields) where T : IG3TimeStamped
        {
            var outlet = _allStreamOutlets[typeof(T)];
            if (!outlet.have_consumers())
                return;

            var ts = _timeSync.ConvertToSystemTime(data.TimeStamp);
            var latency = _timeSync.GetSystemTime() - ts;
            var latencyInSeconds = latency / 1000000d;
            var lslTime = LSL.LSL.local_clock();
            var lslTimeOfSample = lslTime - latencyInSeconds;
            var values = fields.Select(f =>
            {
                var v = f.Selector(data);
                if (v.HasValue && !float.IsNaN(v.Value))
                    return v.Value;
                return float.NaN;
            }).ToArray();
            outlet.push_sample(values, lslTimeOfSample);
        }

        public async void Close()
        {
            _timer.Stop();
            _timer.Close();
            await _api.Disconnect();
            foreach (var o in _allStreamOutlets.Values)
            {
                o.Close();
                o.Dispose();
            }

            _allStreamOutlets.Clear();
            foreach (var i in _allStreamInfo)
            {
                i.Close();
                i.Dispose();
            }

            _allStreamInfo.Clear();
        }
    }

    public class G3AccData : G3TimeStamped
    {
        public Vector3 Accelerometer { get; }

        public G3AccData(in TimeSpan timeStamp, Vector3 acc) : base(in timeStamp)
        {
            Accelerometer = acc;
        }
    }

    public class G3GyroData : G3TimeStamped
    {
        public Vector3 Gyro { get; }

        public G3GyroData(in TimeSpan timeStamp, Vector3 gyro) : base(in timeStamp)
        {
            Gyro = gyro;
        }
    }
    public class G3MagData : G3TimeStamped
    {
        public Vector3 Mag { get; }

        public G3MagData(in TimeSpan timeStamp, Vector3 mag) : base(in timeStamp)
        {
            Mag = mag;
        }
    }

    public class G3PupilData : G3TimeStamped
    {
        public G3GazeData Data { get; }

        public G3PupilData(G3GazeData data) : base(data.TimeStamp)
        {
            Data = data;
        }
    }

    public class G3Gaze2dData : G3TimeStamped
    {
        public G3GazeData Data { get; }

        public G3Gaze2dData(G3GazeData data) : base(data.TimeStamp)
        {
            Data = data;
        }
    }
    public class G3Gaze3dData : G3TimeStamped
    {
        public G3GazeData Data { get; }

        public G3Gaze3dData(G3GazeData data) : base(data.TimeStamp)
        {
            Data = data;
        }
    }
    public class G3GazeOriginData : G3TimeStamped
    {
        public G3GazeData Data { get; }

        public G3GazeOriginData(G3GazeData data) : base(data.TimeStamp)
        {
            Data = data;
        }
    }
    public class G3GazeDirectionData : G3TimeStamped
    {
        public G3GazeData Data { get; }

        public G3GazeDirectionData(G3GazeData data) : base(data.TimeStamp)
        {
            Data = data;
        }
    }
}