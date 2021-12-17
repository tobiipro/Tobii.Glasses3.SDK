using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSocketSharper;
using WebSocket = WebSocketSharper.WebSocket;
using WebSocketState = WebSocketSharper.WebSocketState;

namespace G3SDK
{
    public abstract class G3ApiBase
    {
        public LogLevel LogLevel { get; set; } = LogLevel.debug;
        // private long _requestId;
        // private readonly Mutex _webSocketMutex = new Mutex();
        private readonly string _ip;
        //        private ClientWebSocket _ws;
        private readonly WebSocket _ws2;
        // private Task _wsConnectTask;
        // private readonly CancellationToken _cancellationToken = new CancellationToken();
        // private readonly object _wsConnectLock = new object();
        private CancellationTokenSource _receiveTokenSource;
        private readonly ConcurrentDictionary<long, string> _requests = new ConcurrentDictionary<long, string>();
        private readonly int _port = 80;
        private string _state;
        private readonly ConcurrentQueue<QueueMessage> _msgQueue = new ConcurrentQueue<QueueMessage>();
        private readonly Task _sendMessagesTask;

        public G3ApiBase(string ip, bool startWebSock)
        {
            _ip = ip;
            _state = "websocketcreated";
            _ws2 = new WebSocket(_myLogger, $"ws://{_ip}:{_port}/websocket", true, "g3api");
            _ws2.MessageReceived.Subscribe(DoReceive);
            _ws2.OnError += (sender, args) => { _state = "error"; };
            _ws2.OnOpen += (sender, args) => { _state = "open"; };
            _ws2.OnClose += (sender, args) => { _state = "closed"; };

            CreateWebSocket();
            _sendMessagesTokenSource = new CancellationTokenSource();
            _sendMessagesTask = Task.Run(() => SendMessages(_sendMessagesTokenSource.Token));
            // if (startWebSock)
            //     EnsureConnected();
        }

        private async void SendMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_msgQueue.TryDequeue(out var msg) && _ws2.ReadyState == WebSocketState.Open)
                {
                    _ws2.Send(msg.Msg);
                }
                else
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        public string IpAddress => _ip;

        internal void Log(LogLevel level, string msg)
        {
            if (level >= LogLevel)
                Console.WriteLine(msg);
        }

        // private void ReconnectWebSock()
        // {
        //     _ws?.Dispose();
        //     _ws = null;
        //     _wsConnectTask = null;
        //     ServicePointManager.MaxServicePointIdleTime = int.MaxValue;
        //     EnsureWebSocket();
        //     EnsureConnected();
        // }

        private void CreateWebSocket()
        {
            // _state = "websocketcreated";
            // _ws2 = new WebSocket(
            //     new MyLogger("ServerSocket", Microsoft.Extensions.Logging.LogLevel.Trace),
            //     $"ws://{_ip}:{_port}/websocket", true, "g3api");
            //
            // _ws2.MessageReceived.Subscribe(DoReceive);
            // _ws2.OnError += (sender, args) =>
            // {
            //     _state = "error";
            // };
            // _ws2.OnOpen += (sender, args) =>
            // {
            //     _state = "open";
            // };
            //
            // _ws2.OnClose += (sender, args) =>
            // {
            //     _state = "closed";
            // };
        }

        private void DoReceive(WebMessage webMessage)
        {
            if (webMessage.MessageType == WebSocketMessageType.Close)
            {
                // handle close
//                _wsConnectTask = null;
                return;
            }

            var orgMsg = webMessage.Text;
            WebSockMsg msg;
            try
            {
                msg = JsonConvert.DeserializeObject<WebSockMsg>(orgMsg);
            }
            catch (Exception e)
            {
                Log(LogLevel.error, $"WS: Failed to parse message to WebSockMsg. error: [{e.Message}] msg=[{orgMsg}]");
                return;
            }

            if (msg.error.HasValue)
            {
                if (msg.id.HasValue && _requests.TryGetValue(msg.id.Value, out var orgRequest))
                    throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}\nRequest contents: [{orgRequest}]");

                throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}");
            }

            HandleWebSocketMessage(msg, webMessage.Text.Length, orgMsg);
        }

        private static long msgCounter = 0;
        private static object _counterLock = new object();
        private readonly CancellationTokenSource _sendMessagesTokenSource;
        private MyLogger _myLogger = new MyLogger("ServerSocket", Microsoft.Extensions.Logging.LogLevel.Trace);


        public async Task<long> SendToWebSocket(string path, Method method, params object[] parameters)
        {
            EnsureConnected();
            //_wsConnectTask.Wait(_cancellationToken);

            long id;
            lock (_counterLock)
            {
                id = msgCounter++;
            }

            var msg = $"{{\"path\":\"//{path}\",\"id\":{id},\"method\":\"{method}\",\"body\":{ParametersToWsBody(parameters)}}}";
            Log(LogLevel.info, $"WS: >> {msg}");

            var msgObject = new QueueMessage(id, msg);
            _msgQueue.Enqueue(msgObject);

            // make sure only one send-operation is in progress
            //_webSocketMutex.WaitOne();

            // FIXME put the message in a queue that is processed by its own thread to make sure only one send is in progress.
            lock (_counterLock)
            {
                _requests[msgObject.MsgId] = msg;
            }
            //             try
            //             {
            //                 await _ws2.SendTaskAsync(msg);
            //             }
            //             finally
            //             {
            // //                _webSocketMutex.ReleaseMutex();
            //             }

            return msgObject.MsgId;
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

        public string State => _state + "|"+ _ws2?.ReadyState.ToString();
        private void EnsureConnected()
        {
            if (_ws2.ReadyState == WebSocketState.Closed || _ws2.ReadyState == WebSocketState.Connecting)
            {
                _ws2.Connect();
            }
            // //            EnsureWebSocket();
            //             lock (_wsConnectLock)
            //             {
            //                 if (_wsConnectTask == null)
            //                 {
            //                     _wsConnectTask = _ws2.Connect(); //.ContinueWith(OnConnect);
            //                 }
            //             }

            // lock (_wsConnectLock)
            // {
            //     if (_wsConnectTask == null)
            //     {
            //         _wsConnectTask = _ws2.ConnectTaskAsync();
            //     }
            // }
            //
            // return _wsConnectTask;
        }

        public async Task Disconnect()
        {
            if (_sendMessagesTokenSource != null)
            {
                _sendMessagesTokenSource.Cancel();
                await _sendMessagesTask;
            }

            if (_ws2 != null && _ws2.ReadyState == WebSocketSharper.WebSocketState.Open)
            {
                try
                {
                    await _ws2.CloseTaskAsync(CloseStatusCode.Normal, "Just closing...");
                }
                catch
                {
                }
            }
//            _wsConnectTask = null;
        }

        // private void OnConnect(Task t)
        // {
        //     Receive();
        // }
        //
        // private async void Receive()
        // {
        //     await EnsureConnected();
        //     //_wsConnectTask.Wait(_cancellationToken);
        //
        //     _receiveTokenSource = new CancellationTokenSource();
        //     var buffers = new List<(ArraySegment<byte> arr, WebSocketReceiveResult rcvRes)>();
        //     var receivedBytes = 0;
        //     while (!_receiveTokenSource.IsCancellationRequested)
        //     {
        //         var rcvBytes = new byte[WSBUFFERSIZE];
        //
        //         var rcvBuffer = new ArraySegment<byte>(rcvBytes);
        //         if (_ws.CloseStatus.HasValue && _ws.CloseStatus != WebSocketCloseStatus.Empty)
        //             throw new Exception($"WS: Websock closed - {_ws.CloseStatus}, message: {_ws.CloseStatusDescription}");
        //         WebSocketReceiveResult rcvResult;
        //         try
        //         {
        //             rcvResult = await _ws.ReceiveAsync(rcvBuffer, _receiveTokenSource.Token);
        //         }
        //         catch (TaskCanceledException)
        //         {
        //             Log(LogLevel.info, $"Websocket cancelled");
        //             break;
        //         }
        //         catch (WebSocketException e)
        //         {
        //             Log(LogLevel.error, $"WebSocketException, trying to reconnect: {e.Message}");
        //             ReconnectWebSock();
        //             await EnsureConnected();
        //             buffers.Clear();
        //             receivedBytes = 0;
        //             continue;
        //         }
        //         catch (SocketException e)
        //         {
        //             Log(LogLevel.error, $"SocketException, trying to reconnect: {e.Message}");
        //             ReconnectWebSock();
        //             await EnsureConnected();
        //             buffers.Clear();
        //             receivedBytes = 0;
        //             continue;
        //         }
        //
        //         if (rcvResult.MessageType == WebSocketMessageType.Binary)
        //             throw new Exception("WS: Unhandled binary message");
        //         if (rcvResult.CloseStatus.HasValue && rcvResult.CloseStatus != WebSocketCloseStatus.Empty)
        //             throw new Exception($"WS: Websocket closed - {rcvResult.CloseStatus}, message: {rcvResult.CloseStatusDescription}");
        //         if (rcvResult.MessageType == WebSocketMessageType.Close)
        //             throw new Exception("WS: Websocket closed?");
        //
        //         buffers.Add((rcvBuffer, rcvResult));
        //         receivedBytes += rcvResult.Count;
        //
        //         if (!rcvResult.EndOfMessage)
        //         {
        //             Log(LogLevel.debug, $"WS << PARTIAL {rcvResult.Count} bytes, total {receivedBytes}");
        //         }
        //         else
        //         {
        //             try
        //             {
        //                 WebSockMsg msg;
        //                 var completeMsg = new byte[receivedBytes];
        //                 var index = 0;
        //                 foreach (var (arr, rcvRes) in buffers)
        //                 {
        //                     Array.Copy(arr.Array, arr.Offset, completeMsg, index, rcvRes.Count);
        //                     index += rcvRes.Count;
        //                 }
        //
        //                 var orgMsg = Encoding.UTF8.GetString(completeMsg);
        //                 try
        //                 {
        //                     msg = JsonConvert.DeserializeObject<WebSockMsg>(orgMsg);
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Log(LogLevel.error, $"WS: Failed to parse message to WebSockMsg. error: [{e.Message}] msg=[{orgMsg}]");
        //                     continue;
        //                 }
        //
        //                 if (msg.error.HasValue)
        //                 {
        //                     if (msg.id.HasValue && _requests.TryGetValue(msg.id.Value, out var orgRequest))
        //                         throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}\nRequest contents: [{orgRequest}]");
        //
        //                     throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}");
        //                 }
        //
        //                 HandleWebSocketMessage(msg, receivedBytes, orgMsg);
        //
        //
        //             }
        //             finally
        //             {
        //                 buffers.Clear();
        //                 receivedBytes = 0;
        //             }
        //         }
        //     }
        //     _receiveTokenSource.Dispose();
        //     _receiveTokenSource = null;
        // }

        protected abstract void HandleWebSocketMessage(WebSockMsg msg, int rcvResultCount, string orgMsg);
    }

    public class QueueMessage
    {
        public string Msg { get; }
        public long MsgId { get; }

        public QueueMessage(long id, string msg)
        {
            MsgId = id;
            Msg = msg;
        }
    }
}