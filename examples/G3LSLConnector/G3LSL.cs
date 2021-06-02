using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using G3SDK;
using LSL;

namespace G3LSLConnector
{
    public class G3LSL
    {
        private G3Api _api;
        private StreamInfo _info;
        private StreamOutlet _outlet;
        private IDisposable _token;
        private Timer _timer;
        private List<FieldInfo<G3GazeData>> _fields;
        private RudimentaryTimeSync _timeSync;
        public static readonly string Company = "Tobii Pro AB";
        public static readonly string GazeStreamName = "Tobii Pro Glasses 3";
        public static readonly string GazeStreamType = "Wearable eye tracker";
        public static readonly string GazeChannelType = "Eye tracking";

        public G3LSL(G3Api api)
        {
            _api = api;
            _fields = new List<FieldInfo<G3GazeData>> {
                new("gaze2d.x", "normalized", data => data.Gaze2D.X),
                new("gaze2d.y", "normalized", data => data.Gaze2D.Y),
                new("left-pupil", "mm", data => data.LeftEye?.PupilDiameter),
                new("right-pupil", "mm", data => data.RightEye?.PupilDiameter),
            };
            _fields.AddRange(CreateFields("gaze3d", "mm", data => data.Gaze3D));
            _fields.AddRange(CreateFields("left-gaze-origin", "mm", data => data.LeftEye?.GazeOrigin));
            _fields.AddRange(CreateFields("right-gaze-origin", "mm", data => data.RightEye?.GazeOrigin));
            _fields.AddRange(CreateFields("left-gaze-direction", "unit vector", data => data.LeftEye?.GazeDirection));
            _fields.AddRange(CreateFields("right-gaze-direction", "unit vector", data => data.RightEye?.GazeDirection));
        }

        private IEnumerable<FieldInfo<G3GazeData>> CreateFields(string fieldName, string unit, Func<G3GazeData, Vector3?> func)
        {
            yield return new FieldInfo<G3GazeData>(fieldName + ".x", unit, data => func(data)?.X);
            yield return new FieldInfo<G3GazeData>(fieldName + ".y", unit, data => func(data)?.Y);
            yield return new FieldInfo<G3GazeData>(fieldName + ".z", unit, data => func(data)?.Z);
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
            _info = new StreamInfo(GazeStreamName, GazeStreamType, _fields.Count, frequency, channel_format_t.cf_float32, serialNum);

            var channels = _info.desc().append_child("channels");
            foreach (var f in _fields)
                channels.append_child("channel")
                    .append_child_value("label", f.Label)
                    .append_child_value("unit", f.Unit)
                    .append_child_value("type", GazeChannelType);
            _info.desc().append_child_value("manufacturer", Company);

            // create outlet for the stream
            _outlet = new StreamOutlet(_info);

            _token = await _api.Rudimentary.Gaze.SubscribeAsync(SendGaze);
            _timer = new Timer(3000);
            _timer.Elapsed += SendKeepAlive;
            _timer.Enabled = true;

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
            var ts = _timeSync.ConvertToSystemTime(data.TimeStamp);
            var latency = _timeSync.GetSystemTime() - ts;
            var latencyInSeconds = latency / 1000000d;
            var lslTime = LSL.LSL.local_clock();
            var lslTimeOfSample = lslTime - latencyInSeconds;
            _outlet.push_sample(_fields.Select(f => f.Selector(data) ?? float.NaN).ToArray(), lslTimeOfSample);
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