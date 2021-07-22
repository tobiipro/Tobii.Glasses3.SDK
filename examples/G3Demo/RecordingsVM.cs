using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using FFmpeg.AutoGen;
using G3SDK;
using Unosquare.FFME;

namespace G3Demo
{
    public class RecordingsVM : ViewModelBase
    {
        private readonly G3Api _g3;
        private bool _scanning;
        private RecordingVM _selectedRecording;
        public ObservableCollection<RecordingVM> Recordings { get; } = new ObservableCollection<RecordingVM>();

        public RecordingVM SelectedRecording
        {
            get => _selectedRecording;
            set
            {
                if (Equals(value, _selectedRecording)) return;
                _selectedRecording = value;
                OnPropertyChanged();
            }
        }

        public RecordingsVM(Dispatcher dispatcher, G3Api g3) : base(dispatcher)
        {
            _g3 = g3;
            _g3.Recordings.ScanStart.SubscribeAsync(n => _scanning = true);
            _g3.Recordings.ScanDone.SubscribeAsync(async n =>
            {
                _scanning = false;
                await SyncRecordings();
            });
            _g3.Recordings.ChildAdded.SubscribeAsync(async s =>
            {
                if (!_scanning)
                    await SyncRecordings();
            });
            _g3.Recordings.ChildRemoved.SubscribeAsync(async s =>
            {
                if (!_scanning)
                    await SyncRecordings();
            });

            FireAndCatch(SyncRecordings());

        }

        private void FireAndCatch(Task task)
        {
            task.ContinueWith(t =>
            {
                Dispatcher.Invoke(() => throw t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task SyncRecordings()
        {
            var deviceRecordings = await _g3.Recordings.Children();
            foreach (var r in deviceRecordings)
            {
                if (Recordings.All(rec => rec.Id != r.UUID))
                    Recordings.Add(await RecordingVM.Create(Dispatcher, r));
            }

            foreach (var rec in Recordings.ToArray())
            {
                if (deviceRecordings.All(r => r.UUID != rec.Id))
                    Recordings.Remove(rec);
            }
        }
    }

    public class RecordingVM : ViewModelBase
    {
        public static async Task<RecordingVM> Create(Dispatcher d, Recording r)
        {
            var vm = new RecordingVM(d, r);
            await vm.Init();
            return vm;
        }
        private readonly Recording _recording;
        private TimeSpan _duration;
        private string _visibleName;
        private DateTime _created;
        private readonly List<G3GazeData> _gaze = new List<G3GazeData>();
        private ConcurrentQueue<G3GazeData> _gazeQueue;
        private double _durationInSeconds;
        private MediaElement _media;
        private TimeSpan _position;
        private double _gazeLoadedUntil;
        private Timer _gazeLoadTimer;

        private RecordingVM(Dispatcher dispatcher, Recording recording) : base(dispatcher)
        {
            _recording = recording;

            Play = new DelegateCommand(DoPlay, () => true);
        }

        public async Task AttachMediaPlayer(MediaElement media)
        {
            _media = media;
            _media.RenderingVideo += (sender, args) =>
            {
                InternalSetPosition(args.StartTime);
            };
            await _media.Open(VideoUri);
            await PrepareReplay();
        }

        private void InternalSetPosition(TimeSpan t)
        {
            _position = t;
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(PositionInSeconds));
        }

        private async Task DoPlay()
        {
            if (_gaze == null)
                await PrepareReplay();

            if (_media.IsPlaying)
                await _media.Pause();
            else
                await _media.Play();
        }

        private async Task Init()
        {
            Duration = await _recording.Duration;
            VisibleName = await _recording.VisibleName;
            Created = await _recording.Created;
            VideoUri = await _recording.GetUri("scenevideo.mp4");
        }

        private async Task PrepareReplay()
        {
            var res = await _recording.GazeDataAsync();
            _gazeQueue = res.Item1;
            _gazeLoadTimer = new Timer(1000);
            _gazeLoadTimer.Elapsed += (sender, args) => FlushGazeQueue();
            _gazeLoadTimer.Enabled = true;

            res.Item2.ConfigureAwait(false).GetAwaiter().OnCompleted(() =>
            {
                _gazeLoadTimer.Enabled = false;
                FlushGazeQueue();
                GazeLoadedUntil = DurationInSeconds;
            });
        }

        private void FlushGazeQueue()
        {
            while (_gazeQueue.TryDequeue(out var g))
            {
                _gaze.Add(g);
            }
            GazeLoadedUntil = _gaze.Last().TimeStamp.TotalSeconds;
        }

        public DateTime Created
        {
            get => _created;
            set
            {
                if (value.Equals(_created)) return;
                _created = value;
                OnPropertyChanged();
            }
        }

        public string VisibleName
        {
            get => _visibleName;
            set
            {
                if (value == _visibleName) return;
                _visibleName = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set
            {
                if (value.Equals(_duration)) return;
                _duration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DurationInSeconds));
            }
        }

        public Guid Id => _recording.UUID;

        public Uri VideoUri { get; private set; }

        public double DurationInSeconds => Duration.TotalSeconds;

        public DelegateCommand Play { get; }

        public TimeSpan Position
        {
            get => _position;
            set
            {
                if (value.Equals(_position)) return;
                _position = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PositionInSeconds));
                _media.Position = value;
            }
        }

        public double PositionInSeconds
        {
            get { return Position.TotalSeconds; }
            set { Position = TimeSpan.FromSeconds(value); }
        }

        public double GazeLoadedUntil
        {
            get => _gazeLoadedUntil;
            set
            {
                if (value.Equals(_gazeLoadedUntil)) return;
                _gazeLoadedUntil = value;
                OnPropertyChanged();
            }
        }
    }
}