using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public interface IG3Observable<T> : IObservable<T>
    {
        bool IsSubscribed { get; }
        Task<IDisposable> SubscribeAsync(IObserver<T> observer);
    }

    public interface ISignal
    {
        void HandleResponse(object msgBody);
        void HandleSignal(object msgBody);
        string SignalName { get; }
    }

    public class SignalHandler
    {
        private readonly G3Api _api;

        internal readonly ConcurrentDictionary<long, ISignal> _signalByRequestId = new ConcurrentDictionary<long, ISignal>();
        internal readonly ConcurrentDictionary<long, ISignal> _signalBySignalId = new ConcurrentDictionary<long, ISignal>();
        internal readonly ConcurrentDictionary<string, ISignal> _signalBySignalPath = new ConcurrentDictionary<string, ISignal>();

        public SignalHandler(G3Api api)
        {
            _api = api;
        }

        internal IG3Observable<T> CreateSignal<T>(string path, string signalName, Func<List<JToken>, T> bodyTranslator)
        {
            var signalPath = $"{path}:{signalName}";
            if (!_signalBySignalPath.TryGetValue(signalPath, out var signal))
            {
                var typedSignal = new Signal<T>(this, path, signalName, bodyTranslator);
                _signalBySignalPath[signalPath] = typedSignal;
                return typedSignal;
            }

            return signal as IG3Observable<T>;
        }


        public async Task<long> SendToWebSocket(string signalPath, Method method, params object[] parameters)
        {
            return await _api.SendToWebSocket(signalPath, method, parameters);
        }

        internal class Signal<T> : IG3Observable<T>, ISignal
        {
            private readonly SignalHandler _signalHandler;
            private readonly string _path;
            private readonly Func<List<JToken>, T> _bodyTranslator;
            private readonly List<IObserver<T>> _subscribers = new List<IObserver<T>>();
            private readonly object _lock = new object();
            private long _requestId = -1;
            private long _signalId = -1;

            public Signal(SignalHandler signalHandler, string path, string signalName, Func<List<JToken>, T> bodyTranslator)
            {
                _signalHandler = signalHandler;
                _path = path;
                _bodyTranslator = bodyTranslator;
                SignalName = signalName;
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                if (_requestId == -1)
                {
                    Task.Run(StartSubscription);
                }
                _subscribers.Add(observer);
                return new UnSubscriber(this, observer);
            }

            private async Task StartSubscription()
            {
                var requestId = await _signalHandler.SendToWebSocket(SignalPath, Method.POST);
                lock (_lock)
                {
                    _requestId = requestId;
                    _signalHandler.SetSignalByRequestId(_requestId, this);
                }
            }

            public string SignalName { get; }

            public string SignalPath => $"{_path}:{SignalName}";

            public bool IsSubscribed
            {
                get
                {
                    lock (_lock)
                    {
                        return _signalId != -1;
                    }
                }
            }

            public async Task<IDisposable> SubscribeAsync(IObserver<T> observer)
            {
                if (_requestId == -1)
                {
                    await Task.Run(StartSubscription);
                }
                _subscribers.Add(observer);
                return new UnSubscriber(this, observer);
            }

            internal class UnSubscriber : IDisposable
            {
                private readonly Signal<T> _signal;
                private readonly IObserver<T> _observer;

                public UnSubscriber(Signal<T> signal, IObserver<T> observer)
                {
                    _signal = signal;
                    _observer = observer;
                }

                public async void Dispose()
                {
                    await _signal.Unsubscribe(_observer);
                }
            }

            private async Task Unsubscribe(IObserver<T> observer)
            {
                _subscribers.Remove(observer);
                if (!_subscribers.Any())
                {
                    await _signalHandler.StopSignal(SignalPath, _signalId, _requestId);
                    lock (_lock)
                    {
                        _requestId = -1;
                        _signalId = -1;
                    }

                }
            }

            public void HandleResponse(object msgBody)
            {
                lock (_lock)
                {
                    _signalId = (long)msgBody;
                }
                _signalHandler._signalBySignalId[_signalId] = this;
            }

            public void HandleSignal(object msgBody)
            {
                var arr = (JArray)msgBody;
                var list = new List<JToken>();
                foreach (var x in arr)
                    list.Add(x);

                var v = _bodyTranslator(list);
                foreach (var o in _subscribers.ToArray())
                    o.OnNext(v);
            }
        }

        private void SetSignalByRequestId<T>(long requestId, Signal<T> signal)
        {
            _signalByRequestId[requestId] = signal;
        }

        private async Task StopSignal(string path, long signalId, long requestId)
        {
            await SendToWebSocket(path, Method.POST, signalId);
            _signalByRequestId.TryRemove(requestId, out var signal);
            _signalBySignalId.TryRemove(signalId, out signal);
        }

        public bool HandleMessage(WebSockMsg msg)
        {
            if (msg.id.HasValue && _signalByRequestId.TryGetValue(msg.id.Value, out var signal) && msg.body != null)
            {
                signal.HandleResponse(msg.body);
                return true;
            }

            if (msg.signal.HasValue && _signalBySignalId.TryGetValue(msg.signal.Value, out signal))
            {
                signal.HandleSignal(msg.body);
                return true;
            }

            return false;
        }
    }

    public class Notification
    {
    }


    public static class IObservableExtensions
    {
        public static IDisposable Subscribe<T>(this IG3Observable<T> obs, Action<T> onNext)
        {
            var observer = new AnonymousObserver<T>(onNext);
            return obs.Subscribe(observer);
        }
        public static Task<IDisposable> SubscribeAsync<T>(this IG3Observable<T> obs, Action<T> onNext)
        {
            var observer = new AnonymousObserver<T>(onNext);
            return obs.SubscribeAsync(observer);
        }

    }
}