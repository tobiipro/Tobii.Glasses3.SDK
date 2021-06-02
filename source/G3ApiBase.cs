using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace G3SDK
{
    public abstract class G3ApiBase
    {
        public LogLevel LogLevel { get; set; } = LogLevel.debug;
        private readonly int WSBUFFERSIZE = 16384;
        private long _requestId;
        private readonly Mutex _webSocketMutex = new Mutex();
        private readonly string _ip;
        private ClientWebSocket _ws;
        private Task _wsConnectTask;
        private readonly CancellationToken _cancellationToken = new CancellationToken();
        private readonly object _wsConnectLock = new object();
        private CancellationTokenSource _receiveTokenSource;
        private readonly Dictionary<long, string> _requests = new Dictionary<long, string>();

        public G3ApiBase(string ip, bool startWebSock)
        {
            _ip = ip;
            if (startWebSock)
                ReconnectWebSock();
        }
        public string IpAddress => _ip;

        internal void Log(LogLevel level, string msg)
        {
            if (level >= LogLevel)
                Console.WriteLine(msg);
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

        private static string ParametersToWsBody(object[] parameters)
        {
            if (parameters.Length == 0)
                return "null";
            if (parameters.Length == 1)
                return parameters[0].ToString();
            return ParametersToBody(parameters);
        }
        protected static string ParametersToBody(object[] parameters)
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
                    throw new Exception($"WS: Websocket closed - {rcvResult.CloseStatus}, message: {rcvResult.CloseStatusDescription}");
                if (rcvResult.MessageType == WebSocketMessageType.Close)
                    throw new Exception("WS: Websocket closed?");

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
                        foreach (var (arr, rcvRes) in buffers)
                        {
                            Array.Copy(arr.Array, arr.Offset, completeMsg, index, rcvRes.Count);
                            index += rcvRes.Count;
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

                        HandleWebSocketMessage(msg, receivedBytes, orgMsg);

                        
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

        protected abstract void HandleWebSocketMessage(WebSockMsg msg, int rcvResultCount, string orgMsg);
    }
}