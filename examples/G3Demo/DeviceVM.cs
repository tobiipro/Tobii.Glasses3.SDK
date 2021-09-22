using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Threading;
using G3SDK;
using OxyPlot;
using Unosquare.FFME.Common;
using Zeroconf;

namespace G3Demo
{
    public class DeviceVM : ViewModelBase
    {
        private readonly string _hostName;
        private readonly RtspDataDemuxer _rtspDataDemuxer;
        private readonly Queue<G3GazeData> _gazeQueue = new Queue<G3GazeData>();
        private readonly IG3Api _g3;
        private readonly Timer _calibMarkerTimer;
        private readonly Timer _externalTimeReferenceTimer;

        private G3GazeData _lastValidGaze;
        private string _gyr;
        private double _gazeY;
        private double _gazeX;
        private string _gazeBuffer;
        private float _gazeMarkerSize = 10;
        private double _videoHeight;
        private double _videoWidth;
        private double _markerCenterX = int.MinValue;
        private double _markerCenterY = int.MinValue;
        private bool _gazeOverlay;
        private string _msg;
        private string _gaze;
        private string _mag;
        private string _acc;
        private bool _isRecording;
        private SpaceState _spaceState = SpaceState.Unknown;
        private int[] _frequencies = { };
        private int _frequency;
        private CardState _cardState = CardState.NotInserted;
        private string _sync;
        private string _event;
        private bool _showCalibMarkers;
        private CalibMarker _win;
        private bool _selected;
        private bool _isCalibrated;
        private double _lastExternalTimeRoundtrip;
        private int _externalTimeReferenceIndex;
        private readonly CalibratedMagnetometer _calibMag;

        public DeviceVM(string hostName, IG3Api g3, Dispatcher dispatcher) : base(dispatcher)
        {
            _hostName = hostName;
            _g3 = g3;
            ShowCalibrationMarkerWindow = new DelegateCommand(p => DoShowCalibrationMarkerWindow(), () => true);
            StartRecording = new DelegateCommand(DoStartRecording, CanStartRec);
            StopRecording = new DelegateCommand(DoStopRecording, () => IsRecording);
            TakeSnapshot = new DelegateCommand(DoTakeSnapshot, () => IsRecording);
            CalibrateMagStart = new DelegateCommand(o => _calibMag.StartCalibration(), () => true);
            CalibrateMagStop = new DelegateCommand(o => _calibMag.StartCalibration(), () => true);

            _calibMarkerTimer = new Timer(2000);
            _calibMarkerTimer.Elapsed += async (sender, args) =>
            {
                if (_showCalibMarkers)
                    await _g3.Calibrate.EmitMarkers();
                else
                {
                    MarkerCenterX = int.MinValue;
                    MarkerCenterY = int.MinValue;
                }
            };
            _calibMarkerTimer.Enabled = true;

            _externalTimeReferenceTimer = new Timer(5000);
            _externalTimeReferenceTimer.Elapsed += async (sender, args) =>
            {
                var sw = Stopwatch.StartNew();
                await _g3.Recorder.SendEvent("ExternalTimeReference",
                    new ExternalTimeReference(DateTime.UtcNow, DateTime.Now, Environment.MachineName, _lastExternalTimeRoundtrip, _externalTimeReferenceIndex++));
                _lastExternalTimeRoundtrip = sw.Elapsed.TotalMilliseconds;
            };
            _externalTimeReferenceTimer.Enabled = true;

            _g3.Calibrate.Marker.SubscribeAsync(OnCalibMarker);
            _g3.Settings.Changed.SubscribeAsync(OnSettingsChanged);
            _g3.Recorder.Started.SubscribeAsync(g => IsRecording = true);
            _g3.Recorder.Stopped.SubscribeAsync(g => IsRecording = false);
            _g3.System.Storage.StateChanged.SubscribeAsync(OnCardStateChanged);
            GazePlotEnabled = false;

            _rtspDataDemuxer = new RtspDataDemuxer();
            _rtspDataDemuxer.OnGaze += (sender, data) =>
            {
                Gaze = $"Gaze: {data.Gaze2D.X:F3};{data.Gaze2D.Y:F3}";
                _gazeQueue.Enqueue(data);
                Dispatcher.Invoke(() =>
                {
                    if (GazePlotEnabled)
                    {
                        AddPoint(GazeXSeries, data.TimeStamp, data.Gaze2D.X);
                        AddPoint(GazeYSeries, data.TimeStamp, data.Gaze2D.Y);
                    }

                    if (PupilPlotEnabled)
                    {
                        AddPoint(PupilLeftSeries, data.TimeStamp, data.LeftEye?.PupilDiameter ?? float.NaN);
                        AddPoint(PupilRightSeries, data.TimeStamp, data.RightEye?.PupilDiameter ?? float.NaN);
                    }
                });
            };
            _rtspDataDemuxer.OnSyncPort += (sender, data) => Sync = $"Sync: {data.Direction}={data.Value}";
            _rtspDataDemuxer.OnImu += (sender, data) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (AccPlotEnabled && data.Accelerometer.IsValid())
                    {
                        AddPoint(AccXSeries, data.TimeStamp, data.Accelerometer.X);
                        AddPoint(AccYSeries, data.TimeStamp, data.Accelerometer.Y);
                        AddPoint(AccZSeries, data.TimeStamp, data.Accelerometer.Z);
                    }

                    if (GyrPlotEnabled && data.Gyroscope.IsValid())
                    {
                        AddPoint(GyrXSeries, data.TimeStamp, data.Gyroscope.X);
                        AddPoint(GyrYSeries, data.TimeStamp, data.Gyroscope.Y);
                        AddPoint(GyrZSeries, data.TimeStamp, data.Gyroscope.Z);
                    }

                    if (MagPlotEnabled && data.Magnetometer.IsValid())
                    {
                        AddPoint(MagXSeries, data.TimeStamp, data.Magnetometer.X);
                        AddPoint(MagYSeries, data.TimeStamp, data.Magnetometer.Y);
                        AddPoint(MagZSeries, data.TimeStamp, data.Magnetometer.Z);
                    }
                });

                if (data.Magnetometer.IsValid()) Mag = $"Mag: {FormatV3(data.Magnetometer)}";
                if (data.Gyroscope.IsValid()) Gyr = $"Gyr: {FormatV3(data.Gyroscope)}";
            };
            _rtspDataDemuxer.OnEvent += (sender, e) => Event = $"Event: {e.Tag}, {e.Obj}";
            _rtspDataDemuxer.OnUnknownEvent += (sender, e) => Msg = $"** {e.Item1}";
            _rtspDataDemuxer.OnUnknownEvent2 += (sender, e) => Msg = $"-- {e.Item1}";
            HideGaze();
            _calibMag = new CalibratedMagnetometer(_g3);
            _calibMag.Start();
            _calibMag.Subscribe(data =>
                {
                    if (MagPlotEnabled && data.Magnetometer.IsValid())
                    {
                        AddPoint(CalibMagXSeries, data.TimeStamp, data.Magnetometer.X);
                        AddPoint(CalibMagYSeries, data.TimeStamp, data.Magnetometer.Y);
                        AddPoint(CalibMagZSeries, data.TimeStamp, data.Magnetometer.Z);
                    }

                }
            );
        }


        private void AddPoint(ThrottlingObservableCollection<DataPoint> data, TimeSpan time, float value)
        {
            data.Add(new DataPoint(time.TotalSeconds, value));

            while (data.Last().X - data.First().X > 3)
                data.RemoveFirst();
        }

        private Task DoTakeSnapshot()
        {
            return _g3.Recorder.Snapshot();
        }

        #region ViewModel properties
        public string Id => _hostName;
        public bool Selected
        {
            get => _selected;
            set
            {

                if (value == _selected) return;
                _selected = value;
                OnPropertyChanged();
            }
        }
        public string Serial { get; private set; }

        public double GazeX
        {
            get => _gazeX;
            set
            {
                if (value.Equals(_gazeX)) return;
                _gazeX = value;
                OnPropertyChanged();
            }
        }

        public double GazeY
        {
            get => _gazeY;
            set
            {
                if (value.Equals(_gazeY)) return;
                _gazeY = value;
                OnPropertyChanged();
            }
        }

        public int Frequency
        {
            get => _frequency;
            set
            {
                if (value == _frequency) return;
                _frequency = value;
                OnPropertyChanged();
                _g3.Settings.SetGazeFrequency(value);
            }
        }

        public int[] Frequencies
        {
            get => _frequencies;
            set
            {
                if (Equals(value, _frequencies)) return;
                _frequencies = value;
                OnPropertyChanged();
            }
        }
        public SpaceState SpaceState
        {
            get => _spaceState;
            set
            {
                if (_spaceState == value) return;
                _spaceState = value;
                OnPropertyChanged();
                RaiseCanExecuteChange(StartRecording);
            }
        }
        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (_isRecording == value)
                    return;
                _isRecording = value;
                RaiseCanExecuteChange(StartRecording);
                RaiseCanExecuteChange(StopRecording);
                RaiseCanExecuteChange(TakeSnapshot);
                OnPropertyChanged();
            }
        }

        public CardState CardState
        {
            get => _cardState;
            set
            {
                if (value == _cardState) return;
                _cardState = value;
                OnPropertyChanged();
                RaiseCanExecuteChange(StartRecording);

            }
        }
        public double MarkerCenterY
        {
            get => _markerCenterY;
            set
            {
                if (value.Equals(_markerCenterY)) return;
                _markerCenterY = value;
                OnPropertyChanged();
            }
        }

        public double MarkerCenterX
        {
            get => _markerCenterX;
            set
            {
                if (value.Equals(_markerCenterX)) return;
                _markerCenterX = value;
                OnPropertyChanged();
            }
        }
        public bool ShowCalibMarkers
        {
            get => _showCalibMarkers;
            set
            {
                _showCalibMarkers = value;
                OnPropertyChanged();
            }
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

        public string GazeBuffer
        {
            get => _gazeBuffer;
            set
            {
                if (value == _gazeBuffer) return;
                _gazeBuffer = value;
                OnPropertyChanged();
            }
        }

        public string Event
        {
            get => _event;
            set
            {
                if (value == _event) return;
                _event = value;
                OnPropertyChanged();
            }
        }

        public string Sync
        {
            get => _sync;
            set
            {
                if (value == _sync) return;
                _sync = value;
                OnPropertyChanged();
            }
        }

        public string Acc
        {
            get => _acc;
            set
            {
                if (value == _acc) return;
                _acc = value;
                OnPropertyChanged();
            }
        }
        public string Gyr
        {
            get => _gyr;
            set
            {
                if (value == _gyr) return;
                _gyr = value;
                OnPropertyChanged();
            }
        }

        public string Mag
        {
            get => _mag;
            set
            {
                if (value == _mag) return;
                _mag = value;
                OnPropertyChanged();
            }
        }

        public string Gaze
        {
            get => _gaze;
            set
            {
                if (value == _gaze) return;
                _gaze = value;
                OnPropertyChanged();
            }
        }

        public string Msg
        {
            get => _msg;
            set
            {
                if (value == _msg) return;
                _msg = value;
                OnPropertyChanged();
            }
        }

        public bool GazeOverlay
        {
            get => _gazeOverlay;
            set
            {
                if (value == _gazeOverlay) return;
                _gazeOverlay = value;
                OnPropertyChanged();
                _g3.Settings.SetGazeOverlay(value);
            }
        }

        public Uri LiveVideoUri => _g3?.LiveRtspUri();
        #endregion

        #region Commands
        public ICommand ShowCalibrationMarkerWindow { get; }
        public DelegateCommand StartRecording { get; }
        public DelegateCommand StopRecording { get; }
        public DelegateCommand TakeSnapshot { get; }

        public bool IsCalibrated
        {
            get => _isCalibrated;
            set
            {
                if (value == _isCalibrated) return;
                _isCalibrated = value;
                OnPropertyChanged();
            }
        }

        public ThrottlingObservableCollection<DataPoint> GazeXSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> GazeYSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> AccXSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> AccYSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> AccZSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> GyrXSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> GyrYSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> GyrZSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> MagXSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> MagYSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> MagZSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> CalibMagXSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> CalibMagYSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> CalibMagZSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> PupilLeftSeries { get; } = new ThrottlingObservableCollection<DataPoint>();
        public ThrottlingObservableCollection<DataPoint> PupilRightSeries { get; } = new ThrottlingObservableCollection<DataPoint>();

        public bool GazePlotEnabled { get; set; }
        public bool PupilPlotEnabled { get; set; }
        public bool AccPlotEnabled { get; set; }
        public bool GyrPlotEnabled { get; set; }
        public bool MagPlotEnabled { get; set; }
        public bool CalibMagPlotEnabled { get; set; }

        public ICommand CalibrateMagStop { get; }
        public ICommand CalibrateMagStart { get; }
        private bool CanStartRec()
        {
            return !IsRecording && CardState == CardState.Available &&
                   (SpaceState == SpaceState.Low || SpaceState == SpaceState.Good);
        }
        private void DoShowCalibrationMarkerWindow()
        {
            if (_win == null)
            {
                var vm = new CalibMarkerVM(_g3, Dispatcher);
                _win = new CalibMarker { DataContext = vm };
                _win.Closed += (sender, args) => _win = null;
                vm.OnCalibrationResult += (sender, res) => IsCalibrated = res;
            }
            _win.Show();
        }
        #endregion

        public void CloseView()
        {
            if (_win != null)
            {
                _win.Close();
            }
        }

        private string FormatV3(Vector3 v)
        {
            return v.IsValid() ? $"{v.X:F3};{v.Y:F3};{v.Z:F3}" : "---";
        }

        public async Task InitAsync()
        {
            Serial = await _g3.System.RecordingUnitSerial;
            SpaceState = await _g3.System.Storage.SpaceState;
            CardState = await _g3.System.Storage.CardState;
            GazeOverlay = await _g3.Settings.GazeOverlay;
            Frequencies = await _g3.System.AvailableGazeFrequencies();
            Frequency = await _g3.Settings.GazeFrequency;
            IsRecording = (await _g3.Recorder.Duration).HasValue;
        }

        private void OnCardStateChanged((SpaceState spaceState, CardState cardState) state)
        {
            CardState = state.cardState;
            SpaceState = state.spaceState;
        }

        private async void OnSettingsChanged(string s)
        {
            if (s == "gaze-overlay")
                GazeOverlay = await _g3.Settings.GazeOverlay;
        }


        private void OnCalibMarker(G3MarkerData m)
        {
            MarkerCenterX = m.Marker2D.X * _videoWidth - GazeMarkerSize / 2;
            MarkerCenterY = m.Marker2D.Y * _videoHeight - GazeMarkerSize / 2;
        }

        public void HandleData(DataFrame frame, StreamInfo stream)
        {
            var bytes = frame.GetPacketData();
            if (bytes != null)
            {
                _rtspDataDemuxer.HandleData(bytes, frame.StartTime, frame.StreamIndex, stream.StreamIndex, stream.StreamId);
            }
        }

        public void DrawGaze(TimeSpan argsStartTime, double width, double height)
        {
            _videoWidth = width;
            _videoHeight = height;
            while (_gazeQueue.Count > 1 && _gazeQueue.Peek().TimeStamp < argsStartTime)
            {
                var g = _gazeQueue.Dequeue();
                if (g.Gaze2D.IsValid())
                    _lastValidGaze = g;
            }
            if (_lastValidGaze != null && (argsStartTime - _lastValidGaze.TimeStamp).TotalMilliseconds < 150)
            {
                GazeX = _lastValidGaze.Gaze2D.X * width - GazeMarkerSize / 2;
                GazeY = _lastValidGaze.Gaze2D.Y * height - GazeMarkerSize / 2;
            }
            else
            {
                HideGaze();
            }

            GazeBuffer = $"GazeBuffer: {_gazeQueue.Count} samples";
            if (_gazeQueue.Count >= 2)
            {
                var bufferLength = _gazeQueue.Last().TimeStamp - _gazeQueue.First().TimeStamp;
                GazeBuffer += $" {bufferLength.TotalMilliseconds:F0} ms";
            }
        }

        public async Task<(bool, Guid)> DoStartRecording()
        {
            var res = await _g3.Recorder.Start();
            var guid = await _g3.Recorder.UUID;
            return (res, guid);
        }

        public async Task<bool> DoStopRecording()
        {
            return await _g3.Recorder.Stop();
        }

        private void HideGaze()
        {
            GazeX = int.MinValue;
            GazeY = int.MinValue;
        }

        public async Task<bool> Calibrate()
        {
            var res = await _g3.Calibrate.Run();
            IsCalibrated = res;
            return res;
        }

        public RecordingsVM CreateRecordingsVM()
        {
            return new RecordingsVM(Dispatcher, _g3);
        }
    }
}