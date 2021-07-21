using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class RtspDataDemuxer
    {
        private readonly Dictionary<int, Action<JObject, TimeSpan>> _streamHandler = new Dictionary<int, Action<JObject, TimeSpan>>();
        private readonly Dictionary<string, Action<JObject, TimeSpan>> _keyProperties = new Dictionary<string, Action<JObject, TimeSpan>>();

        public RtspDataDemuxer()
        {
            _keyProperties["gaze2d"] = HandleGaze;
            _keyProperties["accelerometer"] = HandleImu;
            _keyProperties["gyroscope"] = HandleImu;
            _keyProperties["magnetometer"] = HandleImu;
            _keyProperties["direction"] = HandleSyncPort;
            _keyProperties["tag"] = HandleEvent;
        }


        public void HandleData(byte[] bytes, TimeSpan timeStamp, int frameStreamIndex, int streamStreamIndex, int streamStreamId)
        {
            var s = Encoding.UTF8.GetString(bytes);

            if (JsonConvert.DeserializeObject(s) is JObject j)
            {
                if (_streamHandler.TryGetValue(frameStreamIndex, out var action))
                {
                    action(j, timeStamp);
                    return;
                }

                foreach (var k in _keyProperties.Keys)
                {
                    if (j.ContainsKey(k))
                    {
                        var action2 = _keyProperties[k];
                        _streamHandler[frameStreamIndex] = action2;
                        action2(j, timeStamp);
                        return;
                    }
                }
                OnUnknownEvent?.Invoke(this, (j, timeStamp));
            }
            else
            {
                OnUnknownEvent2?.Invoke(this, (s, timeStamp));
            }

        }

        private void HandleImu(JObject j, TimeSpan timeStamp)
        {
            var imu = ParserHelpers.ParseImuData(j, timeStamp);
            OnImu?.Invoke(this, imu);
        }

        public void HandleGaze(JObject j, TimeSpan timeStamp)
        {
            var x = ParserHelpers.ParseGazeData(j, timeStamp);
            OnGaze?.Invoke(this, x);
        }
        public void HandleEvent(JObject j, TimeSpan timeStamp)
        {
            var x = ParserHelpers.ParseEvent(j, timeStamp);
            OnEvent?.Invoke(this, x);
        }
        public void HandleSyncPort(JObject j, TimeSpan timeStamp)
        {
            var x = ParserHelpers.ParseSyncPortData(j, timeStamp);
            OnSyncPort?.Invoke(this, x);
        }

        public event EventHandler<G3GazeData> OnGaze;
        public event EventHandler<G3ImuData> OnImu;
        public event EventHandler<G3Event> OnEvent;
        public event EventHandler<G3SyncPortData> OnSyncPort;
        public event EventHandler<(JObject, TimeSpan)> OnUnknownEvent;
        public event EventHandler<(string, TimeSpan)> OnUnknownEvent2;
    }
}