using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Rudimentary : G3Object, IRudimentary
    {
        private readonly RWProperty<int> _sceneQuality;
        private readonly RWProperty<int> _sceneScale;
        private readonly ROProperty<G3GazeData> _gazeSample;
        private readonly ROProperty<G3ImuData> _imuSample;
        private readonly ROProperty<G3SyncPortData> _syncPortSample;
        private readonly ROProperty<G3Event> _eventSample;

        public Rudimentary(G3Api g3Api) : base(g3Api, "rudimentary")
        {
            _gazeSample = AddROProperty("gaze-sample", ParserHelpers.ParseGazeFromJson);
            _imuSample = AddROProperty("imu-sample", ParserHelpers.ParseImuFromJson);
            _syncPortSample = AddROProperty("sync-port-sample", ParserHelpers.ParseSyncPortFromJson);
            _eventSample = AddROProperty("event-sample", ParserHelpers.ParseEventFromJson);
            Gaze = AddSignal("gaze", ParserHelpers.SignalToGaze);
            Event = AddSignal("event", ParserHelpers.SignalToEvent);
            Imu = AddSignal("imu", ParserHelpers.SignalToIMU);
            SyncPort = AddSignal("sync-port", ParserHelpers.SignalToSyncPort);
            Scene = AddSignal("scene", ParseB64);

            _sceneScale = AddRWProperty("scene-scale", int.Parse);
            _sceneQuality = AddRWProperty("scene-quality", int.Parse);
        }

        public Task<G3GazeData> GazeSample => _gazeSample.Value();
        public Task<G3ImuData> ImuSample => _imuSample.Value();
        public Task<G3Event> EventSample => _eventSample.Value();
        public Task<G3SyncPortData> SyncPortSample => _syncPortSample.Value();
        public Task<int> SceneScale => _sceneScale.Value();

        public Task<int> SceneQuality => _sceneQuality.Value();

        public Task<bool> SetSceneQuality(int quality)
        {
            return _sceneQuality.Set(quality);
        }

        public Task<bool> SetSceneScale(int scale)
        {
            return _sceneScale.Set(scale);
        }

        private byte[] ParseB64(List<JToken> list)
        {
            var b64 = list[1].Value<string>();
            return Convert.FromBase64String(b64);
        }

        public IG3Observable<byte[]> Scene { get; }

        public IG3Observable<G3SyncPortData> SyncPort { get; }

        public IG3Observable<G3GazeData> Gaze { get; }

        public IG3Observable<G3Event> Event { get; }

        public IG3Observable<G3ImuData> Imu { get; set; }

        public async Task<bool> Keepalive()
        {
            return await G3Api.ExecuteCommandBool(Path, "keepalive", LogLevel.info);
        }

        public async Task<bool> Calibrate()
        {
            return await G3Api.ExecuteCommandBool(Path, "calibrate", LogLevel.info);
        }

        public async Task<bool> SendEvent(string tag, object obj)
        {
            return await G3Api.ExecuteCommandBool(Path, "send-event", LogLevel.info, tag, obj);
        }
    }

    public interface IRudimentary
    {
        Task<G3GazeData> GazeSample { get; }
        Task<G3ImuData> ImuSample { get; }
        Task<G3Event> EventSample { get; }
        Task<G3SyncPortData> SyncPortSample { get; }
        Task<int> SceneScale { get; }
        Task<int> SceneQuality { get; }
        IG3Observable<byte[]> Scene { get; }
        IG3Observable<G3SyncPortData> SyncPort { get; }
        IG3Observable<G3GazeData> Gaze { get; }
        IG3Observable<G3Event> Event { get; }
        IG3Observable<G3ImuData> Imu { get; }
        Task<bool> SetSceneQuality(int quality);
        Task<bool> SetSceneScale(int scale);
        Task<bool> Keepalive();
        Task<bool> Calibrate();
        Task<bool> SendEvent(string tag, object obj);
    }
}