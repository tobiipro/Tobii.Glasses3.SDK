using System;
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
        private DeviceDetailsVm _selectedTracker;
        private LiveViewVM _liveView;
        private Task _initialBrowseTask;

        public MainVm(Dispatcher dispatcher): base(dispatcher)
        {
            _browser = new G3Browser();
            BrowseForGlasses = new DelegateCommand(DoBrowseForGlasses, () => true);
            Unosquare.FFME.Library.FFmpegDirectory = "ffmpeg\\ffmpeg-4.4-full_build-shared\\bin";
            EnsureFFMPEG();
            _initialBrowseTask = DoBrowseForGlasses();
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

        public ObservableCollection<DeviceDetailsVm> Devices { get; } = new ObservableCollection<DeviceDetailsVm>();

        public DeviceDetailsVm SelectedTracker
        {
            get => _selectedTracker;
            set
            {
                _selectedTracker = value;
                if (LiveView != null)
                    LiveView.Close();
                LiveView = value.CreateLiveViewVM();
            }
        }

        public void Close()
        {
            if (LiveView != null)
                LiveView.Close();
        }

        public LiveViewVM LiveView
        {
            get => _liveView;
            set
            {
                if (Equals(value, _liveView)) return;
                _liveView = value;
                OnPropertyChanged();
            }
        }

        private async Task DoBrowseForGlasses()
        {
            if (_initialBrowseTask != null && !_initialBrowseTask.IsCompleted)
                return;

            var devices = await _browser.ScanZeroConf();

            foreach (var d in devices)
            {
                if (!Devices.Any(device => device.Id == d.Id))
                {
                    Logs.Add($"Found new device: {d.Id}");
                    var deviceVm = new DeviceDetailsVm(d, Dispatcher);
                    Devices.Add(deviceVm);
                    await deviceVm.Init();
                }
            }

            _initialBrowseTask = null;
        }
    }
}