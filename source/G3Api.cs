using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Boolean;

namespace G3SDK
{
    public class G3Api
    {
        //http://192.168.0.113/upgradefile

        public LogLevel LogLevel { get; set; } = LogLevel.debug;
        private readonly string _ip;
        private ClientWebSocket _ws;
        private Task _wsConnectTask;
        private readonly CancellationToken _cancellationToken = new CancellationToken();
        private long _requestId;

        private readonly Mutex _webSocketMutex = new Mutex();


        public G3Api(string ip, bool startWebSock = true)
        {
            _ip = ip;
            if (startWebSock)
                ReconnectWebSock();
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

        private void ReconnectWebSock()
        {
            _ws?.Dispose();
            _wsConnectTask = null;
            _ws = new ClientWebSocket();
            ServicePointManager.MaxServicePointIdleTime = int.MaxValue;
            _ws.Options.AddSubProtocol("g3api");
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            _ws.Options.SetBuffer(WSBUFFERSIZE, WSBUFFERSIZE);
            EnsureConnected();
        }

        public Settings Settings { get; }

        public Rudimentary Rudimentary { get; }

        public Network Network { get; }

        public SignalHandler SignalHandler { get; }

        public Calibrate Calibrate { get; }

        public WebRTC WebRTC { get; }
        public Upgrade Upgrade { get; }

        public Recorder Recorder { get; }
        public string IpAddress => _ip;
        public SystemObj System { get; }
        public Recordings Recordings { get; }


        internal void Log(LogLevel level, string msg)
        {
            if (level >= LogLevel)
                Console.WriteLine(msg);
        }

        private Task EnsureConnected()
        {
            lock (_wsConnectLock)
            {
                if (_wsConnectTask == null)
                {
                    var connect = _ws.ConnectAsync(new Uri($"ws://{_ip}/websocket"), _cancellationToken);
                    _wsConnectTask = connect.ContinueWith(OnConnect, _cancellationToken);
                }
            }
            return _wsConnectTask;
        }

        public async Task Disconnect()
        {
            if (_receiveTokenSource != null)
            {
                _receiveTokenSource.Cancel();
                await Task.Delay(20, CancellationToken.None);
            }
            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Just closing...", CancellationToken.None);
                }
                catch
                {
                }
            }
            _wsConnectTask = null;
        }


        private void OnConnect(Task t)
        {
            Receive();
        }

        private async void Receive()
        {
            await EnsureConnected();
            _wsConnectTask.Wait(_cancellationToken);

            _receiveTokenSource = new CancellationTokenSource();
            var buffers = new List<(ArraySegment<byte> arr, WebSocketReceiveResult rcvRes)>();
            var receivedBytes = 0;
            while (!_receiveTokenSource.IsCancellationRequested)
            {
                var rcvBytes = new byte[WSBUFFERSIZE];

                var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                if (_ws.CloseStatus.HasValue && _ws.CloseStatus != WebSocketCloseStatus.Empty)
                    throw new Exception($"WS: Websock closed - {_ws.CloseStatus}, message: {_ws.CloseStatusDescription}");
                WebSocketReceiveResult rcvResult;
                try
                {
                    rcvResult = await _ws.ReceiveAsync(rcvBuffer, _receiveTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    Log(LogLevel.info, $"Websocket cancelled");
                    break;
                }
                catch (WebSocketException e)
                {
                    Log(LogLevel.error, $"WebSocketException, trying to reconnect: {e.Message}");
                    ReconnectWebSock();
                    await EnsureConnected();
                    buffers.Clear();
                    receivedBytes = 0;
                    continue;
                }
                catch (SocketException e)
                {
                    Log(LogLevel.error, $"SocketException, trying to reconnect: {e.Message}");
                    ReconnectWebSock();
                    await EnsureConnected();
                    buffers.Clear();
                    receivedBytes = 0;
                    continue;
                }

                if (rcvResult.MessageType == WebSocketMessageType.Binary)
                    throw new Exception("WS: Unhandled binary message");
                if (rcvResult.CloseStatus.HasValue && rcvResult.CloseStatus != WebSocketCloseStatus.Empty)
                    throw new Exception($"WS: Websock closed - {rcvResult.CloseStatus}, message: {rcvResult.CloseStatusDescription}");
                if (rcvResult.MessageType == WebSocketMessageType.Close)
                    throw new Exception("WS: Websock closed?");

                buffers.Add((rcvBuffer, rcvResult));
                receivedBytes += rcvResult.Count;

                if (!rcvResult.EndOfMessage)
                {
                    Log(LogLevel.debug, $"WS << PARTIAL {rcvResult.Count} bytes, total {receivedBytes}");
                }
                else
                {
                    try
                    {
                        WebSockMsg msg;
                        var completeMsg = new byte[receivedBytes];
                        var index = 0;
                        foreach (var a in buffers)
                        {
                            Array.Copy(a.arr.Array, a.arr.Offset, completeMsg, index, a.rcvRes.Count);
                            index += a.rcvRes.Count;
                        }

                        var orgMsg = Encoding.UTF8.GetString(completeMsg);
                        try
                        {
                            msg = JsonConvert.DeserializeObject<WebSockMsg>(orgMsg);
                        }
                        catch (Exception e)
                        {
                            Log(LogLevel.error, $"WS: Failed to parse message to WebSockMsg. error: [{e.Message}] msg=[{orgMsg}]");
                            continue;
                        }

                        if (msg.error.HasValue)
                        {
                            if (msg.id.HasValue && _requests.TryGetValue(msg.id.Value, out var orgRequest))
                                throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}\nRequest contents: [{orgRequest}]");

                            throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}");
                        }

                        if (SignalHandler.HandleMessage(msg, rcvResult.Count))
                        {
                            Log(LogLevel.info, $"WS << {receivedBytes} bytes: {TrimTo(orgMsg, 80)}");
                        }
                        else
                        {
                            Log(LogLevel.warning, $"WS Unhandled message << {receivedBytes} bytes: {TrimTo(orgMsg, 80)}");
                        }
                    }
                    finally
                    {
                        buffers.Clear();
                        receivedBytes = 0;
                    }
                }
            }
            _receiveTokenSource.Dispose();
            _receiveTokenSource = null;
        }

        private string TrimTo(string s, int length)
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

        public async Task<long> SendToWebSocket(string path, Method method, params object[] parameters)
        {
            await EnsureConnected();
            _wsConnectTask.Wait(_cancellationToken);
            var id = _requestId++; ;

            var msg = $"{{\"path\":\"//{path}\",\"id\":{id},\"method\":\"{method}\",\"body\":{ParametersToWsBody(parameters)}}}";
            Log(LogLevel.info, $"WS: >> {msg}");
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg));

            _webSocketMutex.WaitOne();
            _requests[id] = msg;
            try
            {
                var t = _ws.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                t.Wait();
            }
            finally
            {
                _webSocketMutex.ReleaseMutex();
            }

            return id;
        }

        private string ParametersToWsBody(object[] parameters)
        {
            if (parameters.Length == 0)
                return "null";
            if (parameters.Length == 1)
                return parameters[0].ToString();
            return ParametersToBody(parameters);
        }

        private static string ParametersToBody(object[] parameters)
        {
            var body = new StringBuilder();
            body.Append('[');
            if (parameters != null)
            {
                var first = true;
                foreach (var p in parameters)
                {
                    if (!first)
                        body.Append(", ");
                    if (p == null)
                        body.Append("null");
                    else
                    {
                        var res = JsonConvert.SerializeObject(p, Formatting.None);
                        body.Append(res);
                    }
                    first = false;
                }
            }
            body.Append(']');
            return body.ToString();
        }

        private readonly HttpClient _client = new HttpClient();
        private readonly object _wsConnectLock = new object();
        private CancellationTokenSource _receiveTokenSource;
        private readonly Dictionary<long, string> _requests = new Dictionary<long, string>();
        private int WSBUFFERSIZE = 16384;

        internal async Task<string> PostRequest(string path, string body, LogLevel logLevel = LogLevel.info)
        {
            var url = $"http://{_ip}/rest/{path}";
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
            var requestUriString = $"http://{_ip}/{path.TrimStart('/')}";
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
                return x.ReadToEnd();
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