using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class WebRTC: DynamicChildNode
    {
        public WebRTC(G3Api g3Api): base(g3Api, "webrtc")
        {
        }

        public async Task<WebRTCSession> Create()
        {
            var response = await G3Api.ExecuteCommand(Path, "create", LogLevel.info);
            var guidStr = response.Trim('"');
            var guid = Guid.Parse(guidStr);
            return new WebRTCSession(G3Api, Path, guid);
        }

        public async Task<WebRTCSession> Play(Guid uuid)
        {
            var response = await G3Api.ExecuteCommand(Path, "play", LogLevel.info, uuid.ToString());
            var guidStr = response.Trim('"');
            var guid = Guid.Parse(guidStr);
            return new WebRTCSession(G3Api, Path, guid);
        }

        public async Task<bool> Delete(WebRTCSession session)
        {
            return await G3Api.ExecuteCommandBool(Path, "delete", LogLevel.info, session.Guid.ToString());
        }

        public async Task<List<WebRTCSession>> Children()
        {
            var childIds = await GetChildren();
            var sessions = new List<WebRTCSession>();
            foreach (var child in childIds)
            {
                var uuid = Guid.Parse(child);
                sessions.Add(new WebRTCSession(G3Api, Path, uuid));
            }
            return sessions;
        }


        public class WebRTCSession: G3Object
        {
            private readonly Timer _keepAliveTimer;
            private readonly RWProperty<bool> _iframeStream;
            private readonly RWProperty<string> _stunServer;
            private readonly RWProperty<string> _turnServer;

            public WebRTCSession(G3Api g3Api, string rootUrl, Guid guid): base(g3Api, $"{rootUrl}/{guid}")
            {
                _keepAliveTimer = new Timer(SendKeepAliveTimerCallback);
                _iframeStream = AddRWProperty_bool("iframe-stream");
                _stunServer = AddRWProperty("stun-server");
                _turnServer = AddRWProperty("turn-server");
                Guid = guid;
                Gaze = AddSignal("gaze", ParserHelpers.SignalToGaze);
                Event = AddSignal("event", ParserHelpers.SignalToEvent);
                Imu = AddSignal("imu", ParserHelpers.SignalToIMU);
                SyncPort = AddSignal("sync-port", ParserHelpers.SignalToSyncPort);
                TimedOut = AddSignal("timed-out", list => new Notification());
                NewIceCandidate = AddSignal("new-ice-candidate", list => new IceCandidate(list[0].Value<int>(), list[1].Value<string>()));
            }

            public Task<bool> IFrameStream => _iframeStream.Value();

            public Task<bool> SetIframeStream(bool value)
            {
                return _iframeStream.Set(value);
            }

            public Task<string> StunServer => _stunServer.Value();

            public Task<bool> SetStunServer(string value)
            {
                return _stunServer.Set(value);
            }

            public Task<string> TurnServer => _turnServer.Value();

            public Task<bool> SetTurnServer(string value)
            {
                return _turnServer.Set(value);
            }

            public Guid Guid { get; }

            private void SendKeepAliveTimerCallback(object state)
            {
                Keepalive().Wait();
            }

            public void EnableKeepAliveTimer()
            {
                _keepAliveTimer.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3));
            }
            public void DisableKeepAliveTimer()
            {
                _keepAliveTimer.Change(long.MaxValue, long.MaxValue);
            }

            public async Task Keepalive()
            {
                await G3Api.ExecuteCommand(Path, "keepalive", LogLevel.debug);
            }

            public IG3Observable<G3SyncPortData> SyncPort { get; }
            public IObservable<G3GazeData> Gaze { get; }
            public IObservable<G3Event> Event { get; }
            public IObservable<G3ImuData> Imu { get; }
            public IObservable<Notification> TimedOut { get; }
            public IObservable<IceCandidate> NewIceCandidate { get; }


            public async Task<bool> Start(string offer)
            {
                return await G3Api.ExecuteCommandBool(Path, "start", LogLevel.info, offer);
            }

            public async Task<string> Setup()
            {
                return await G3Api.ExecuteCommand(Path, "setup", LogLevel.info);
            }

            public async Task<string[]> GetIceCandidates()
            {
                return await G3Api.ExecuteCommand<string[]>(Path, "get-ice-candidates", LogLevel.info);
            }
            public async Task AddIceCandidate(IceCandidate candidate)
            {
                await G3Api.ExecuteCommand(Path, "add-ice-candidate", LogLevel.info, candidate.Index, candidate.Candidate);
            }

            public async Task<bool> SendEvent(string tag, object obj)
            {
                return await G3Api.ExecuteCommandBool(Path, "send-event", LogLevel.info, tag, obj);
            }
        }
    }


    public struct IceCandidate
    {
        public IceCandidate(int index, string candidate) : this()
        {
            Index = index;
            Candidate = candidate;
        }

        public int Index { get; }
        public string Candidate { get; }
    }
}