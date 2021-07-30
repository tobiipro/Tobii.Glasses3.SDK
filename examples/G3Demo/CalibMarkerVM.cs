using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Media;
using System.Windows.Threading;
using G3SDK;


namespace G3Demo
{
    public class CalibMarkerVM : ViewModelBase
    {
        private readonly IG3Api _g3;
        private readonly Brush _black = new SolidColorBrush(Colors.Black);
        private readonly Brush _white = new SolidColorBrush(Colors.White);
        private readonly Brush _blue = new SolidColorBrush(Colors.Blue);
        private readonly Brush _red = new SolidColorBrush(Colors.Red);
        private readonly Brush _green = new SolidColorBrush(Colors.Green);
        private Brush _markerColor;
        private double _scale = 3.6;
        private bool _isCalibrating;
        private Brush _centerColor;
        private readonly Timer _timer;
        private Brush _middleColor;

        public CalibMarkerVM(IG3Api g3, Dispatcher dispatcher) : base(dispatcher)
        {
            _g3 = g3;
            if (File.Exists("markersize.dat"))
            {
                if (double.TryParse(File.ReadAllText("markersize.dat"), NumberStyles.Any, CultureInfo.InvariantCulture, out var res))
                    _scale = res;
            }
            _markerColor = _black;
            _centerColor = _black;
            _middleColor = _white;
            ChangeScale = new DelegateCommand(DoChangeSize, () => true);
            Calibrate = new DelegateCommand(DoCalibrate, () => !_isCalibrating);
            _timer = new Timer(2000);
            _timer.Elapsed += async (sender, args) =>
            {
                await _g3.Calibrate.EmitMarkers();
                await _g3.Rudimentary.Keepalive();
            };
            _timer.Enabled = true;
            _g3.Calibrate.Marker.SubscribeAsync(m => { CenterColor = m.Marker2D.IsValid() ? _green : _red; });
            _g3.Rudimentary.Gaze.SubscribeAsync(g => MiddleColor = g.Gaze2D.IsValid() ? _white : _red);
        }

        private async Task DoCalibrate()
        {
            _isCalibrating = true;
            RaiseCanExecuteChange(Calibrate);
            CenterColor = _blue;
            var res = await _g3.Calibrate.Run();
            if (res)
            {
                Utils.Play("success");
                MarkerColor = _green;
            }
            else
            {
                Utils.Play("failure");
                MarkerColor = _red;
            }

            OnCalibrationResult?.Invoke(this, res);
            CenterColor = _black;
            await Task.Delay(TimeSpan.FromSeconds(2));
            MarkerColor = _black;
            _isCalibrating = false;
            RaiseCanExecuteChange(Calibrate);
        }

        private void DoChangeSize(object o)
        {
            Scale *= 1 + double.Parse(o.ToString(), CultureInfo.InvariantCulture);
            File.WriteAllText("markersize.dat", Scale.ToString(CultureInfo.InvariantCulture));
        }

        public DelegateCommand ChangeScale { get; set; }

        public Brush MarkerColor
        {
            get => _markerColor;
            set
            {
                if (Equals(value, _markerColor)) return;
                _markerColor = value;
                OnPropertyChanged();
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (value.Equals(_scale)) return;
                _scale = value;
                OnPropertyChanged();
            }
        }

        public DelegateCommand Calibrate { get; }

        public Brush CenterColor
        {
            get => _centerColor;
            set
            {
                if (Equals(value, _centerColor)) return;
                _centerColor = value;
                OnPropertyChanged();
            }
        }

        public Brush MiddleColor
        {
            get => _middleColor;
            set
            {
                if (Equals(value, _middleColor)) return;
                _middleColor = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler<bool> OnCalibrationResult;
    }
}