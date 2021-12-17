using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WebSocketSharper.Net;

namespace WebSocketSharper
{
    public interface IWebSocket
    {
        CompressionMethod Compression { get; set; }
        IEnumerable<Cookie> Cookies { get; }
        NetworkCredential Credentials { get; }
        bool EmitOnPing { get; set; }
        bool EnableRedirection { get; set; }
        string Extensions { get; }
        bool IsAlive { get; }
        bool IsSecure { get; }
        string Origin { get; set; }
        string Protocol { get; }
        WebSocketState ReadyState { get; }
        ClientSslConfiguration SslConfiguration { get; }
        Uri Url { get; }
        TimeSpan WaitTime { get; set; }

        IObservable<WebMessage> MessageReceived { get; }

        event EventHandler<CloseEventArgs> OnClose;
        event EventHandler<ErrorEventArgs> OnError;
        event EventHandler<MessageEventArgs> OnMessage;
        event EventHandler OnOpen;

        Task CloseTaskAsync(CloseStatusCode code, string reason);
        Task ConnectTaskAsync();
        Task<bool> PingTaskAsync(string message = null);
        Task SendTaskAsync(byte[] data);
        Task SendTaskAsync(string data);
    }
}