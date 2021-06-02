using System;
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
    public class G3Api : G3ApiBase
    {
        private readonly HttpClient _client = new HttpClient();

        public G3Api(string ip, bool startWebSock = true) : base(ip, startWebSock)
        {
            SignalHandler = new SignalHandler(this);
            Calibrate = new Calibrate(this);
            WebRTC = new WebRTC(this);
            Recorder = new Recorder(this);
            System = new SystemObj(this);
            Recordings = new Recordings(this);
            Upgrade = new Upgrade(this);
            Network = new Network(this);
            Rudimentary = new Rudimentary(this);
            Settings = new Settings(this);
        }

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
        
        public Settings Settings { get; }
        public Rudimentary Rudimentary { get; }
        public Network Network { get; }
        public Calibrate Calibrate { get; }
        public WebRTC WebRTC { get; }
        public Upgrade Upgrade { get; }
        public Recorder Recorder { get; }
        public SystemObj System { get; }
        public Recordings Recordings { get; }

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
        
        internal async Task<Stream> GetRequestStream(string path, string contentType = "text/plain")
        {
            var requestUriString = $"http://{IpAddress}/{path.TrimStart('/')}";
            Log(LogLevel.info, $"REST-GET: >> {requestUriString}");
            var req = WebRequest.CreateHttp(requestUriString);
            req.Method = "GET";
            req.ContentType = contentType;
            req.Accept = "application/json, text/plain, */*";
            try
            {
                using (var webResponse = req.GetResponse())
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
            catch (WebException e)
            {
                throw new WebException($"Request failed: {e.Message}\nRequestUrl: {requestUriString}", e);
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