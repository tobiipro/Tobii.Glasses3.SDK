using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Boolean;

namespace G3SDK
{
    public class G3Api : G3ApiBase, IG3Api
    {
        private readonly HttpClient _client = new HttpClient();
        private readonly List<G3Object> _children = new List<G3Object>();

        public G3Api(string ip, bool startWebSock = true) : base(ip, startWebSock)
        {

            SignalHandler = new SignalHandler(this);
            Calibrate = Add(new Calibrate(this));
            WebRTC = Add(new WebRTC(this));
            Recorder = Add(new Recorder(this));
            System = Add(new SystemObj(this));
            Recordings = Add(new Recordings(this));
            Upgrade = Add(new Upgrade(this));
            Network = Add(new Network(this));
            Rudimentary = Add(new Rudimentary(this));
            Settings = Add(new Settings(this));
        }

        private T Add<T>(T g3Object) where T : G3Object
        {
            _children.Add(g3Object);
            return g3Object;
        }

        public IReadOnlyCollection<G3Object> Children => _children;

        protected override void HandleWebSocketMessage(WebSockMsg msg, int receivedBytes, string orgMsg)
        {
            if (SignalHandler.HandleMessage(msg))
            {
                Log(LogLevel.info, $"WS << {receivedBytes} bytes: {TrimTo(orgMsg, 80)}");
            }
            else
            {
                Log(LogLevel.warning, $"WS Unhandled message << {receivedBytes} bytes: {TrimTo(orgMsg, 80)}");
            }
        }
        public SignalHandler SignalHandler { get; }

        public ISettings Settings { get; }
        public string LiveRtspUrl(bool gazeOverlay = false)
        {
            return $"rtsp://{IpAddress}:8554/live/all?gaze-overlay={gazeOverlay}";
        }

        public Uri LiveRtspUri(bool gazeOverlay = false)
        {
            return new Uri(LiveRtspUrl(gazeOverlay));
        }

        public IRudimentary Rudimentary { get; }
        public INetwork Network { get; }
        public ICalibrate Calibrate { get; }
        public IWebRTC WebRTC { get; }
        public IUpgrade Upgrade { get; }
        public IRecorder Recorder { get; }
        public ISystem System { get; }
        public IRecordings Recordings { get; }

        private static string TrimTo(string s, int length)
        {
            if (s.Length <= length)
                return s;
            return s.Substring(0, length - 10) + "[...]" + s.Substring(s.Length - 5);
        }

        public async Task<bool> ExecuteCommandBool(string path, string command, LogLevel logLevel, params object[] parameters)
        {
            var res = await ExecuteCommand(path, command, logLevel, parameters);
            return TryParse(res, out var boolres) && boolres;
        }

        public async Task<T> ExecuteCommand<T>(string path, string command, LogLevel logLevel, params object[] parameters)
        {
            var res = await ExecuteCommand(path, command, logLevel, parameters);
            return JsonConvert.DeserializeObject<T>(res);
        }
        public async Task<string> ExecuteCommand(string path, string command, LogLevel logLevel, params object[] parameters)
        {
            var url = $"{path}!{command}";
            var body = ParametersToBody(parameters);
            var result = await PostRequest(url, body, logLevel);
            return result;
        }

        internal async Task<string> PostRequest(string path, string body, LogLevel logLevel = LogLevel.info)
        {
            var url = $"http://{IpAddress}/rest/{path}";
            var c = new ByteArrayContent(Encoding.UTF8.GetBytes(body));

            Log(logLevel, $"REST-POST: >> Url: {url}");
            Log(logLevel, $"REST-POST: >> Body: {body}");

            var resp = await _client.PostAsync(url, c);
            resp.EnsureSuccessStatusCode();

            var result = await resp.Content.ReadAsStringAsync();
            Log(logLevel, $"REST-POST: << {result}");
            return result;
        }

        internal async Task<string> GetRestRequest(string path)
        {
            return await GetRequest($"rest/{path}");
        }

        internal WebResponse GetWebResponse(string path, string contentType = "text/plain")
        {
            var requestUriString = $"http://{IpAddress}/{path.TrimStart('/')}";
            try
            {
                Log(LogLevel.info, $"REST-GET: >> {requestUriString}");
                var req = WebRequest.CreateHttp(requestUriString);
                req.Method = "GET";
                req.ContentType = contentType;
                req.Accept = "application/json, text/plain, */*";
                return req.GetResponse();
            }
            catch (WebException e)
            {
                throw new WebException($"Request failed: {e.Message}\nRequestUrl: {requestUriString}", e);
            }
        }
        
        internal async Task<Stream> GetRequestStream(string path, string contentType = "text/plain")
        {
            using (var webResponse = GetWebResponse(path, contentType))
            using (var responseStream = webResponse.GetResponseStream())
            {
                var content = new MemoryStream();
                await responseStream.CopyToAsync(content);
                content.Position = 0;

                var s = Encoding.UTF8.GetString(content.ToArray());
                if (s.Length > 50)
                {
                    Log(LogLevel.info, $"REST-GET: << {s.Length} bytes");
                    Log(LogLevel.debug, $"REST-GET: << {s}");
                }
                else
                {
                    Log(LogLevel.info, $"REST-GET: << {s}");
                }
                return content;
            }
        }

        internal async Task<string> GetRequest(string path)
        {
            using (var s = await GetRequestStream(path))
            using (var x = new StreamReader(s))
            {
                return await x.ReadToEndAsync();
            }
        }

        public async Task<string> GetProperty(string url, string propertyName)
        {
            return await GetRestRequest($"{url}.{propertyName}");
        }

        public async Task<bool> SetStringProperty(string url, string propertyName, LogLevel logLevel, string value)
        {
            return await SetProperty(url, propertyName, logLevel, "\"" + value + "\"");
        }

        public async Task<bool> SetProperty(string path, string propertyName, LogLevel logLevel, string value)
        {
            var result = await PostRequest($"{path}.{propertyName}", value, logLevel);
            return TryParse(result, out var boolres) && boolres;
        }
    }

    public interface IG3Api
    {
        ICalibrate Calibrate { get; }
        IRecorder Recorder { get; }
        ISettings Settings { get; }
        ISystem System { get; }
        string IpAddress { get; }
        string LiveRtspUrl(bool gazeOverlay = false);
        Uri LiveRtspUri(bool gazeOverlay = false);
        IRudimentary Rudimentary { get; }
        IRecordings Recordings { get; }
        IUpgrade Upgrade { get; }
        LogLevel LogLevel { get; set; }
        INetwork Network { get; }
        IWebRTC WebRTC { get; }
    }

    public interface IRecorder: IMetaDataCapable
    {
        Task<bool> SendEvent(string tag, object o);
        IG3Observable<string> Stopped { get; }
        IG3Observable<Guid> Started { get; }
        Task<string> Folder { get; }
        Task<bool> GazeOverlay { get; }
        Task<string> TimeZone { get; }
        Task<string> VisibleName { get; }
        Task<int> GazeSamples { get; }
        Task<int> ValidGazeSamples { get; }
        Task<Guid> UUID { get; }
        Task<TimeSpan?> Duration { get; }
        Task<TimeSpan> RemainingTime { get; }
        Task<DateTime?> Created { get; }
        Task<int> CurrentGazeFrequency { get; }
        Task<bool> Start();
        Task<bool> Snapshot();
        Task<bool> Stop();
        Task Cancel();
        Task<bool> SetFolder(string value);
        Task<bool> SetVisibleName(string value);
        Task<bool> RecordingInProgress();
    }

    public interface ICalibrate
    {
        Task<bool> EmitMarkers();
        IG3Observable<G3MarkerData> Marker { get; }
        Task<bool> Run();
    }

    public static class Utils
    {
        public static Vector2 Arr2Vector2(this JArray arr)
        {
            if (arr == null || arr.Count == 0)
                return Vector2Extensions.INVALID;
            var x = Convert.ToSingle((arr[0] as JValue).Value);
            var y = Convert.ToSingle((arr[1] as JValue).Value);
            return new Vector2(x, y);
        }

        public static Vector3 Arr2Vector3(this JArray arr)
        {
            if (arr == null || arr.Count == 0)
                return Vector3Extensions.INVALID;
            var x = Convert.ToSingle((arr[0] as JValue).Value);
            var y = Convert.ToSingle((arr[1] as JValue).Value);
            var z = Convert.ToSingle((arr[2] as JValue).Value);
            return new Vector3(x, y, z);
        }
    }
}