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
        private static long _msgCounter = 0;
        private static readonly object ConnectLock = new object();
        private static readonly object CounterLock = new object();
        private readonly CancellationTokenSource _sendMessagesTokenSource;
        private readonly MyLogger _myLogger = new MyLogger("ServerSocket", Microsoft.Extensions.Logging.LogLevel.Trace);
        private readonly EventWaitHandle _sendMsgWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        public LogLevel LogLevel { get; set; } = LogLevel.debug;
        private readonly string _ip;
        private readonly WebSocket _ws2;
        private readonly int _port = 80;
        private string _webSocketState;
        private readonly ConcurrentQueue<QueueMessage> _msgQueue = new ConcurrentQueue<QueueMessage>();
        private readonly Task _sendMessagesTask;

        public G3ApiBase(string ip, bool startWebSock)
        {
            _ip = ip;
            _webSocketState = "websocketcreated";
            _ws2 = new WebSocket(_myLogger, $"ws://{_ip}:{_port}/websocket", true, "g3api");
            _ws2.MessageReceived.Subscribe(DoReceive);
            _ws2.OnError += (sender, args) => { HandleWebSockError(args); };
            _ws2.OnOpen += (sender, args) => { HandleWebSockOpen(args); };
            _ws2.OnClose += (sender, args) => { HandleWebSockClose(args); };

            _sendMessagesTokenSource = new CancellationTokenSource();
            _sendMessagesTask = Task.Run(() => SendMessages(_sendMessagesTokenSource.Token));
        }

        public event EventHandler<string> WebSocketMessage;


        private void HandleWebSockClose(CloseEventArgs closeEventArgs)
        {
            _webSocketState = "closed";
        }

        private void HandleWebSockOpen(EventArgs eventArgs)
        {
            _webSocketState = "open";
        }

        private void HandleWebSockError(ErrorEventArgs errorEventArgs)
        {
            _webSocketState = "error";
        }

        private void SendMessages(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_ws2.ReadyState == WebSocketState.Open && _msgQueue.TryDequeue(out var msg))
                {
                    _ws2.Send(msg.Msg);
                }
                _sendMsgWaitHandle.WaitOne();
            }
        }

        public string IpAddress => _ip;

        internal void Log(LogLevel level, string msg)
        {
            if (level >= LogLevel)
                OnLog?.Invoke(this, new LogMessage(level, msg));
        }

        public event EventHandler<LogMessage> OnLog;

        private void DoReceive(WebMessage webMessage)
        {
            if (webMessage.MessageType == WebSocketMessageType.Close)
            {
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
                Log(LogLevel.error, $"WS: Failed to parse message to WebSockMsg. error: [{e.Message}] message=[{orgMsg}]");
                return;
            }

            if (msg.error.HasValue)
            {
                throw new Exception($"Request {msg.id} failed with error ({msg.error}): {msg.message}");
            }

            HandleWebSocketMessage(msg, webMessage.Text.Length, orgMsg);
            WebSocketMessage?.Invoke(this, orgMsg);
        }


        public long SendToWebSocket(string path, Method method, params object[] parameters)
        {
            EnsureConnected();

            long id;
            lock (CounterLock)
            {
                id = _msgCounter++;
            }

            var msg = $"{{\"path\":\"//{path}\",\"id\":{id},\"method\":\"{method}\",\"body\":{ParametersToWsBody(parameters)}}}";
            Log(LogLevel.info, $"WS: >> {msg}");

            SendToWebSocket(msg);

            return id;
        }

        public void SendToWebSocket(string msg)
        {
            EnsureConnected();

            var msgObject = new QueueMessage( msg);
            _msgQueue.Enqueue(msgObject);
            _sendMsgWaitHandle.Set();
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

        public string State => _webSocketState + "|" + _ws2?.ReadyState.ToString();

        private void EnsureConnected()
        {
            lock (ConnectLock)
            {
                if (_ws2.ReadyState == WebSocketState.Closed || _ws2.ReadyState == WebSocketState.Connecting)
                {
                    _ws2.Connect();
                }
            }
        }

        public async Task Disconnect()
        {
            if (_sendMessagesTokenSource != null)
            {
                _sendMessagesTokenSource.Cancel();
                _sendMsgWaitHandle.Reset();
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
        }

        protected abstract void HandleWebSocketMessage(WebSockMsg msg, int rcvResultCount, string orgMsg);
    }

    public class QueueMessage
    {
        public string Msg { get; }
        public QueueMessage(string msg)
        {
            Msg = msg;
        }
    }
}