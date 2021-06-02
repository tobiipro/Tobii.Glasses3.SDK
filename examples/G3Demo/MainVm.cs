using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using G3Demo.Annotations;
using G3SDK;

namespace G3Demo
{
    public class MainVm : INotifyPropertyChanged
    {
        private readonly G3Browser _browser;
        private G3Api _g3;
        private bool _trackerSelected;
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MainVm()
        {
            _browser = new G3Browser();
            BrowseForGlasses = new DelegateCommand(DoBrowseForGlasses, () => true);
            MakeCalibration = new DelegateCommand(DoMakeCalibration, () => TrackerSelected);
            ListFrequencies = new DelegateCommand(DoListFrequencies, () => TrackerSelected);
            WebRTCSetup = new DelegateCommand(DoWebRTCSetup, () => TrackerSelected);
        }

        public DelegateCommand BrowseForGlasses { get; }

        public DelegateCommand WebRTCSetup { get; }

        public DelegateCommand ListFrequencies { get; }

        public DelegateCommand MakeCalibration { get; }

        public ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public bool TrackerSelected
        {
            get => _trackerSelected;
            set
            {
                _trackerSelected = value;
                OnPropertyChanged();
                ListFrequencies.RaiseCanExecuteChanged();
                WebRTCSetup.RaiseCanExecuteChanged();
                MakeCalibration.RaiseCanExecuteChanged();
            }
        }

        private async Task DoBrowseForGlasses()
        {
            var devices = await _browser.ProbeForDevices();
            if (devices.Any())
            {
                Logs.Add($"Found {devices.Count} devices");

                _g3 = devices.FirstOrDefault();
                Logs.Add($"Connecting to {await _g3.System.RecordingUnitSerial} at {_g3.IpAddress}");
                TrackerSelected = true;
            }
        }

        private async Task DoWebRTCSetup()
        {
            var session = await _g3.WebRTC.Create();
            var sw = new Stopwatch();
            sw.Start();
            var msg = "";
            try
            {
                var offer = await session.Setup();

                msg = offer != null ? "Offer: " + offer : "Fail";
            }
            catch (Exception exception)
            {
                msg = $"Error {exception.GetType()}: {exception.Message}";
            }
            sw.Stop();
            await Task.Delay(2000);
            await _g3.WebRTC.Delete(session);

            Logs.Add(msg);
            Logs.Add($"Time: {sw.ElapsedMilliseconds}ms");
        }

        private async Task DoListFrequencies()
        {
            var frequencies = await _g3.System.AvailableGazeFrequencies();
            foreach (var f in frequencies)
            {
                Logs.Add(f.ToString());
            }
        }

        private async Task DoMakeCalibration()
        {
            var result = await _g3.Calibrate.Run();
            Logs.Add($"Calibration result: {result}");
        }
    }
}