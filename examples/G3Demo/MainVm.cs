using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Threading;
using G3SDK;

namespace G3Demo
{
    public class MainVm : ViewModelBase
    {
        private readonly G3Browser _browser;
        private bool _trackerSelected;
        private DeviceVM _selectedTracker;
        private DeviceVM _liveView;
        private Task _initialBrowseTask;
        private RecordingsVM _recordings;
        private readonly HashSet<string> _deviceIds = new HashSet<string>();
        private int _simCounter;

        public MainVm(Dispatcher dispatcher) : base(dispatcher)
        {
            _browser = new G3Browser();
            BrowseForGlasses = new DelegateCommand(DoBrowseForGlasses, () => true);
            AddSimulator = new DelegateCommand(DoAddSimulator, () => true);
            Unosquare.FFME.Library.FFmpegDirectory = "ffmpeg\\ffmpeg-4.4-full_build-shared\\bin";
            EnsureFFMPEG();
            _initialBrowseTask = DoBrowseForGlasses();
            StartAll = new DelegateCommand(DoStartRecordingAll, () => true);
            StopAll = new DelegateCommand(DoStopRecordingAll, () => true);
            CalibrateAll = new DelegateCommand(DoCalibrateAll, () => true);
            ClearCalibrationData = new DelegateCommand(DoClearCalibrationData, () => true);
        }

        private void DoClearCalibrationData(object obj)
        {
            foreach (var g in Devices)
            {
                g.IsCalibrated = false;
            }
        }

        public DelegateCommand ClearCalibrationData { get; }

        private async Task DoStartRecordingAll()
        {
            await RunParallel(async (device, bag) =>
            {
                if (!device.IsRecording)
                {
                    var res = await device.DoStartRecording();
                    Logs.Add($"{device.Serial}: Start recording " + (res.Item1 ? "OK" : "Fail"));
                }
            });
        }

        private async Task RunParallel(Func<DeviceVM, ConcurrentBag<string>, Task> func)
        {
            var logs = new ConcurrentBag<string>();
            var tasks = Devices.Select(async device => { await func(device, logs); });
            await Task.WhenAll(tasks);
            foreach (var s in logs)
                Logs.Add(s);
        }

        private async Task DoCalibrateAll()
        {
            await RunParallel(async (device, bag) =>
            {
                if (!device.IsCalibrated)
                {
                    var res = await device.Calibrate();

                    bag.Add($"{device.Serial}: Calibration " + (res ? "OK" : "Fail"));
                }
            });

        }
        private async Task DoStopRecordingAll()
        {
            await RunParallel(async (device, bag) =>
            {
                if (device.IsRecording)
                {
                    var res = await device.DoStopRecording();
                    Logs.Add($"{device.Serial}: Stop recording " + (res ? "OK" : "Fail"));
                }
            });
        }

        private static void EnsureFFMPEG()
        {
            if (!Directory.Exists("ffmpeg"))
            {
                Directory.CreateDirectory("ffmpeg");
                var p = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full-shared.7z";
                using (var client = new WebClient())
                {
                    client.DownloadFile(p, "ffmpeg\\ffmpeg-release-full-shared.7z");

                }
            }
        }

        public DelegateCommand BrowseForGlasses { get; }
        public DelegateCommand AddSimulator { get; }

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public bool TrackerSelected
        {
            get => _trackerSelected;
            set
            {
                _trackerSelected = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<DeviceVM> Devices { get; } = new ObservableCollection<DeviceVM>();

        public DeviceVM SelectedTracker
        {
            get => _selectedTracker;
            set
            {
                _selectedTracker = value;
                if (LiveView != null)
                    LiveView.CloseView();
                LiveView = value;
                Recordings = value.CreateRecordingsVM();
            }
        }

        public void Close()
        {
            if (LiveView != null)
                LiveView.CloseView();
        }

        public DeviceVM LiveView
        {
            get => _liveView;
            set
            {
                if (Equals(value, _liveView)) return;
                _liveView = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand StartAll { get; }
        public DelegateCommand StopAll { get; }
        public DelegateCommand CalibrateAll { get; }

        public RecordingsVM Recordings
        {
            get => _recordings;
            private set
            {
                if (Equals(value, _recordings)) return;
                _recordings = value;
                OnPropertyChanged();
            }
        }

        private async Task DoAddSimulator()
        {
            var simVM = new DeviceVM($"Simulator{_simCounter++}", new G3Simulator.G3Simulator(), Dispatcher);
            Devices.Add(simVM);
            await simVM.InitAsync();
        }

        private async Task DoBrowseForGlasses()
        {
            if (_initialBrowseTask != null && !_initialBrowseTask.IsCompleted)
                return;

            var devices = await _browser.ScanZeroConf();

            foreach (var d in devices)
            {
                if (!_deviceIds.Contains(d.Id))
                {
                    Logs.Add($"Found new device: {d.Id}");
                    var deviceVm = new DeviceVM(d.Id, new G3Api(d.IPAddress), Dispatcher);
                    Devices.Add(deviceVm);
                    _deviceIds.Add(deviceVm.Id);
                    await deviceVm.InitAsync();
                }
            }

            _initialBrowseTask = null;
        }
    }
}