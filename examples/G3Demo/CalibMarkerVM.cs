using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Web.Management;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using G3SDK;


namespace G3Demo
{
    public class CalibMarkerVM : ViewModelBase
    {
        private readonly G3Api _g3;
        private readonly Brush _black = new SolidColorBrush(Colors.Black);
        private readonly Brush _blue = new SolidColorBrush(Colors.Blue);
        private readonly Brush _red = new SolidColorBrush(Colors.Red);
        private readonly Brush _green = new SolidColorBrush(Colors.Green);
        private Brush _markerColor;
        private double _scale = 3.6;
        private bool _isCalibrating;
        private Brush _centerColor;

        public CalibMarkerVM(G3Api g3, Dispatcher dispatcher) : base(dispatcher)
        {
            _g3 = g3;
            if (File.Exists("markersize.dat"))
            {
                if (double.TryParse(File.ReadAllText("markersize.dat"), NumberStyles.Any, CultureInfo.InvariantCulture, out var res))
                    _scale = res;
            }
            _markerColor = _black;
            _centerColor = _black;
            ChangeScale = new DelegateCommand(DoChangeSize, () => true);
            Calibrate = new DelegateCommand(DoCalibrate, () => !_isCalibrating);
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

        public event EventHandler<bool> OnCalibrationResult;
    }
}