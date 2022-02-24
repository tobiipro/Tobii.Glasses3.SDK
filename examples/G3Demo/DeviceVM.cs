using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using G3SDK;
using G3SDK.WPF;
using OpenCvSharp;
using OxyPlot;
using Unosquare.FFME.Common;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace G3Demo
{
    public class DeviceVM : ViewModelBase
    {
        private readonly string _hostName;
        private readonly IG3Api _g3;
        private readonly Timer _calibMarkerTimer;
        private readonly Timer _externalTimeReferenceTimer;

        private string _gyr;
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
        private readonly RtspPlayerVM _rtspPlayerVM;
        private string _gazeBuffer;

        public DeviceVM(string hostName, IG3Api g3, Dispatcher dispatcher) : base(dispatcher)
        {
            _hostName = hostName;
            _g3 = g3;
            _rtspPlayerVM = new RtspPlayerVM();
            _rtspPlayerVM.OnMediaAssigned += async (sender, args) => await _rtspPlayerVM.Connect(g3, VideoStream.Scene);
            ShowCalibrationMarkerWindow = new DelegateCommand(p => DoShowCalibrationMarkerWindow(), () => true);
            StartRecording = new DelegateCommand(DoStartRecording, CanStartRec);
            StopRecording = new DelegateCommand(DoStopRecording, () => IsRecording);
            TakeSnapshot = new DelegateCommand(DoTakeSnapshot, () => IsRecording);
            ScanQRCode = new DelegateCommand(DoScanQRCode, () => true);
            CalibrateMagStart = new DelegateCommand(o => _calibMag.StartCalibration(), () => true);
            CalibrateMagStop = new DelegateCommand(o => _calibMag.StartCalibration(), () => true);

            _calibMarkerTimer = new Timer(2000);
            _calibMarkerTimer.Elapsed += async (sender, args) =>
            {
                if (_showCalibMarkers)
                    await _g3.Calibrate.EmitMarkers();
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

            _g3.Settings.Changed.SubscribeAsync(OnSettingsChanged);
            _g3.Recorder.Started.SubscribeAsync(g => IsRecording = true);
            _g3.Recorder.Stopped.SubscribeAsync(g => IsRecording = false);
            _g3.System.Storage.StateChanged.SubscribeAsync(OnCardStateChanged);
            GazePlotEnabled = false;

            _rtspPlayerVM.RtspDataDemuxer.OnGaze += (sender, data) =>
            {
                Gaze = $"Gaze: {data.Gaze2D.X:F3};{data.Gaze2D.Y:F3}";
                GazeBuffer = $"GazeBuffer: {_rtspPlayerVM.GazeQueueSize} samples";

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

            _rtspPlayerVM.OnVideoFrame += (sender, args) =>
            {
                if (_grabNextImage)
                {
                    _grabNextImage = false;
                    _grabbedImage = (Bitmap)args.Bitmap.CreateDrawingBitmap().Clone();
                }
            };

            _rtspPlayerVM.RtspDataDemuxer.OnSyncPort += (sender, data) => Sync = $"Sync: {data.Direction}={data.Value}";
            _rtspPlayerVM.RtspDataDemuxer.OnImu += (sender, data) =>
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
            _rtspPlayerVM.RtspDataDemuxer.OnEvent += (sender, e) => Event = $"Event: {e.Tag}, {e.Obj}";
            _rtspPlayerVM.RtspDataDemuxer.OnUnknownEvent += (sender, e) => Msg = $"** {e.Item1}";
            _rtspPlayerVM.RtspDataDemuxer.OnUnknownEvent2 += (sender, e) => Msg = $"-- {e.Item1}";
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
            _qrTimer.Tick += (sender, args) => QrDetect();
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

        public RtspPlayerVM RtspPlayerVm => _rtspPlayerVM;

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

        public bool ShowCalibMarkers
        {
            get => _showCalibMarkers;
            set
            {
                _showCalibMarkers = value;
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
        public DelegateCommand ScanQRCode { get; }

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

        public async Task<string> ConfigureWifiFromQR(string data)
        {
            if (WifiSettings.TryParseFromQR(data, out var wifi) && (string.IsNullOrEmpty(wifi.Encryption) || !string.IsNullOrEmpty(wifi.Pwd)))
            {
                return await ConfigureWifi(wifi);
            }

            return "invalid QR code";
        }
        
        private async Task<string> ConfigureWifi(WifiSettings wifi)
        {
            if (!await _g3.Network.WifiHwEnabled)
                return "wifi not supported";

            if (!await _g3.Network.WifiEnable)
                await _g3.Network.SetWifiEnable(true);
            await _g3.Network.Wifi.Disconnect();

            await _g3.Network.Wifi.Scan();

            foreach (WifiConfiguration c in await _g3.Network.Wifi.Configurations.Children())
            {
                if (await c.SsidName == wifi.Ssid && await c.Psk == wifi.Pwd)
                {
                    var configId = await c.Name;
                    if (await _g3.Network.Wifi.Connect(Guid.Parse(configId)))
                        return "Connection to existing network config successful?";
                    return "Connection to existing network config failed";
                }
            }

            var networks = await _g3.Network.Wifi.Networks.FindBySsid(wifi.Ssid);
            if (networks.Any())
            {
                var res = await _g3.Network.Wifi.ConnectNetwork(networks.First(), wifi.Pwd);
                if (res)
                    return "Connection to new network successful?";
                return "Connection to new network failed";
            }

            return "Unable to find config or network with ssid " + wifi.Ssid;
        }

        private Bitmap _grabbedImage;
        private bool _grabNextImage;
        private readonly QRCodeDetector _qrCodeDetector = new QRCodeDetector();
        private readonly System.Windows.Forms.Timer _qrTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        private string _qrData;

        private async void QrDetect()
        {
            _qrTimer.Enabled = false;
            _grabNextImage = true;
            while (_grabbedImage == null)
                await Task.Delay(20);

            var sw2 = Stopwatch.StartNew();
            var s = Convert(_grabbedImage);
            var img = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(s);
            _grabbedImage.Dispose();
            _grabbedImage = null;
            var smallImage = new Mat();
            Cv2.Resize(img, smallImage, new OpenCvSharp.Size(img.Width / 2, img.Height / 2));

            var x = sw2.ElapsedMilliseconds;
            var data = _qrCodeDetector.DetectAndDecode(smallImage, out var points);

            sw2.Stop();
            QrData = $"{data} ({x} / {sw2.ElapsedMilliseconds - x}ms)";
            if (!string.IsNullOrEmpty(data) && data.StartsWith("WIFI"))
            {
                var res = await ConfigureWifiFromQR(data);
                QrData = QrData + " " + res;
            }
            else
            {
                _qrTimer.Enabled = true;
            }
            smallImage.Release();
            img.Release();
        }

        public static BitmapSource Convert(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var pixelFormats = PixelFormats.Bgr24;
            switch (bitmap.PixelFormat)
            {
                case PixelFormat.Format32bppArgb:
                    pixelFormats = PixelFormats.Bgra32;
                    break;
                case PixelFormat.Format32bppRgb:
                    pixelFormats = PixelFormats.Bgr32;
                    break;
            }
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                pixelFormats, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        public string QrData
        {
            get => _qrData;
            set
            {
                if (value == _qrData) return;
                _qrData = value;
                OnPropertyChanged();
            }
        }

        private void DoScanQRCode(object obj)
        {
            _qrTimer.Enabled = true;
        }
    }
}