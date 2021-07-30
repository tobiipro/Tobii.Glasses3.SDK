using System;
using System.Diagnostics;
using System.Timers;
using Newtonsoft.Json;

namespace G3SDK
{
    public interface IRudimentaryTimeSync
    {
        bool Initialized { get; }
        long ConvertToSystemTime(TimeSpan valueTimeStamp);
        void RemoveRef();
        long GetSystemTime();
        void AddRef();
        event EventHandler<TimeSyncEventArgs> OnTimeSync;
    }

    public class RudimentaryTimeSync: IRudimentaryTimeSync
    {
        private readonly IRudimentary _streamProvider;
        private readonly Timer _timer;
        private readonly Guid _id = Guid.NewGuid();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private long _offset;
        public bool Initialized { get; private set; }
        private IDisposable _eventSubscriber;
        private int _refCount;
        private TimeSyncEventArgs _lastTimeSyncEventArgs;

        public RudimentaryTimeSync(IRudimentary streamProvider, int timerInterval = 5000)
        {
            _streamProvider = streamProvider;
            _timer = new Timer(timerInterval);
            _timer.Elapsed += TimerOnElapsed;
            _stopwatch.Start();
        }

        private async void TimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            await _streamProvider.Keepalive();
            await _streamProvider.SendEvent("G3TimeSync", new TimeSyncData(_id, GetSystemTime()));
        }

        public long GetSystemTime()
        {
            return _stopwatch.ElapsedTicks / 10;
        }

        private async void Start()
        {
            _eventSubscriber = await _streamProvider.Event.SubscribeAsync(ev => HandleEvent(ev));
            _timer.Enabled = true;
            TimerOnElapsed(this, null);
        }

        private void HandleEvent(G3Event ev)
        {
            if (ev.Tag != "G3TimeSync")
                return;

            var data = JsonConvert.DeserializeObject<TimeSyncData>(ev.Obj);
            if (data == null || data.Id != _id)
                return;

            var t2 = GetSystemTime();
            var roundtrip = t2 - data.T1;
            if (_lastTimeSyncEventArgs != null && roundtrip > 2 * _lastTimeSyncEventArgs.Roundtrip)
                return;

            var midTime = (t2 + data.T1) / 2;
            var deviceT = ev.TimeStamp.Ticks / 10;
            _lastTimeSyncEventArgs = new TimeSyncEventArgs(data.T1, t2, deviceT, midTime, roundtrip);
            _offset = deviceT - midTime;
            OnTimeSync?.Invoke(this, _lastTimeSyncEventArgs);
            Initialized = true;
        }

        public event EventHandler<TimeSyncEventArgs> OnTimeSync;

        public long ConvertToSystemTime(long deviceTime)
        {
            return deviceTime - _offset;
        }

        public long ConvertToSystemTime(TimeSpan deviceTime)
        {
            return ConvertToSystemTime(deviceTime.Ticks / 10);
        }

        private class TimeSyncData
        {
            public Guid Id { get; }
            public long T1 { get; }

            public TimeSyncData(Guid id, long t1)
            {
                Id = id;
                T1 = t1;
            }
        }

        private void Stop()
        {
            _eventSubscriber.Dispose();
            _eventSubscriber = null;
            _timer.Enabled = false;
        }

        public void AddRef()
        {
            _refCount++;
            if (_refCount == 1)
                Start();
        }

        public void RemoveRef()
        {
            _refCount--;
            if (_refCount == 0)
                Stop();
        }
    }

    public class TimeSyncEventArgs : EventArgs
    {
        public long T1 { get; }
        public long T2 { get; }
        public long DeviceT { get; }
        public long MidTime { get; }
        public long Roundtrip { get; }

        public TimeSyncEventArgs(long t1, long t2, long deviceT, long midTime, long roundtrip)
        {
            T1 = t1;
            T2 = t2;
            DeviceT = deviceT;
            MidTime = midTime;
            Roundtrip = roundtrip;
        }
    }
}