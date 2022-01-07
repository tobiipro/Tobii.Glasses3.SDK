using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using G3MetricsDataCollection;
using Unosquare.FFME;
using Unosquare.FFME.Common;

namespace G3SDK.WPF
{
    public class RtspPlayerVM : INotifyPropertyChanged
    {
        private readonly RtspDataDemuxer _rtspDataDemuxer = new RtspDataDemuxer();
        private readonly ConcurrentQueue<G3GazeData> _gazeQueue = new ConcurrentQueue<G3GazeData>();

        private static readonly CoordVm Invalid = new CoordVm(float.MinValue, float.MinValue);

        private G3GazeData _lastValidGaze;
        private CoordVm _marker = Invalid;
        private CoordVm _gaze = Invalid;
        private float _gazeMarkerSize = 40;
        private IDisposable _markerSubscriber;
        private IG3Api _g3;
        private MediaElement _media;
        private DateTime _lastMarker;
        private VideoStream _videoStream;
        private bool _buffering;
        private readonly Timer _bufferingTimer;

        public RtspPlayerVM()
        {
            _bufferingTimer = new Timer(UpdateBufferingProgress);
            Library.FFmpegDirectory = ".";
        }

        private void UpdateBufferingProgress(object state)
        {
            OnPropertyChanged(nameof(BufferingProgress));
        }

        public async Task Connect(IG3Api g3, VideoStream videoStream)
        {
            _g3 = g3;
            while (_gazeQueue.TryDequeue(out var dummy))
            { }

            var rtspUrl = $"rtsp://{_g3.IpAddress}:8554/live/all?gaze-overlay=False";
            await _media.Open(new Uri(rtspUrl));
            _rtspDataDemuxer.OnGaze += ReceiveGaze;
            _videoStream = videoStream;
            switch (videoStream)
            {
                case VideoStream.Scene:
                    _markerSubscriber = await _g3.Calibrate.Marker.SubscribeAsync(HandleMarker);
                    break;
                case VideoStream.Eyes:
                    await _media.ChangeMedia();
                    break;
            }

        }

        private void ReceiveGaze(object sender, G3GazeData e)
        {
            _gazeQueue.Enqueue(e);
            OnGaze?.Invoke(this, e);
        }

        public RtspDataDemuxer RtspDataDemuxer => _rtspDataDemuxer;

        public async Task Disconnect()
        {
            _rtspDataDemuxer.OnGaze -= ReceiveGaze;
            Gaze = Invalid;
            Marker = Invalid;
            _markerSubscriber?.Dispose();
            _markerSubscriber = null;
            await _media.Close();
        }

        public void DrawGaze(object sender, RenderingVideoEventArgs args)
        {
            while (_gazeQueue.TryPeek(out var g) && g.TimeStamp < args.StartTime)
            {
                if (_gazeQueue.TryDequeue(out g) && g.Gaze2D.IsValid())
                    _lastValidGaze = g;
            }

            if (_videoStream == VideoStream.Scene && _lastValidGaze != null && (args.StartTime - _lastValidGaze.TimeStamp).TotalMilliseconds < 150)
            {
                Gaze = GetTopLeft(_lastValidGaze.Gaze2D);
            }
            else
            {
                Gaze = Invalid;
            }

            if ((DateTime.Now - _lastMarker).TotalMilliseconds > 200)
            {
                Marker = Invalid;
            }
            OnVideoFrame.Invoke(this, args);
        }

        private CoordVm GetTopLeft(Vector2 normalized)
        {
            return new CoordVm(
                (float)(normalized.X * _media.ActualWidth - GazeMarkerSize / 2),
                (float)(normalized.Y * _media.ActualHeight - GazeMarkerSize / 2));
        }

        public float GazeMarkerSize
        {
            get => _gazeMarkerSize;
            set
            {
                if (value.Equals(_gazeMarkerSize)) return;
                _gazeMarkerSize = value;
                OnPropertyChanged();
            }
        }

        public CoordVm Gaze
        {
            get => _gaze;
            set
            {
                if (value.Equals(_gaze)) return;
                _gaze = value;
                OnPropertyChanged();
            }
        }

        public void HandleData(object sender, DataFrameReceivedEventArgs args)
        {
            var bytes = args.Frame.GetPacketData();
            if (bytes != null)
            {
                _rtspDataDemuxer.HandleData(bytes, args.Frame.StartTime, args.Frame.StreamIndex, args.Stream.StreamIndex, args.Stream.StreamId);
            }
        }

        private void HandleMarker(G3MarkerData marker)
        {
            if (_videoStream == VideoStream.Scene && marker.Marker2D.X >= 0)
            {
                Marker = GetTopLeft(marker.Marker2D);
            }
            else
            {
                Marker = Invalid;
            }

            _lastMarker = DateTime.Now;
        }

        public CoordVm Marker
        {
            get => _marker;
            set
            {
                _marker = value;
                OnPropertyChanged();
            }
        }

        public int GazeQueueSize => _gazeQueue.Count;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetMedia(MediaElement media)
        {
            _media = media;
            _media.RenderingVideo += DrawGaze;
            _media.DataFrameReceived += HandleData;
            _media.BufferingStarted += (s,a) =>
            {
                Buffering = true;
                _bufferingTimer.Change(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
            };
            _media.BufferingEnded += (s, a) =>
            {
                Buffering = false;
                _bufferingTimer.Change(-1, -1);
            };
            OnMediaAssigned?.Invoke(this, EventArgs.Empty);
        }

        public double? BufferingProgress
        {
            get { return _media?.BufferingProgress; }
            set { }
        }

        public bool Buffering
        {
            get => _buffering;
            set
            {
                _buffering = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler<G3GazeData> OnGaze;
        public event EventHandler<RenderingVideoEventArgs> OnVideoFrame;
        public event EventHandler OnMediaAssigned;
    }
}