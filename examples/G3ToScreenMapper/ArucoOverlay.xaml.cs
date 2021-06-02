using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Timer = System.Timers.Timer;

namespace G3ToScreenMapper
{
    /// <summary>
    /// Interaction logic for ArucoOverlay.xaml
    /// </summary>
    public partial class ArucoOverlay
    {
        private readonly ArucoOverlayVM _vm;
        private Timer _timer;

        public ArucoOverlay(ArucoOverlayVM vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Colors.Transparent);
            _timer = new Timer(40);
            _timer.Elapsed += (sender, args) =>
            {
                Dispatcher.Invoke(() => _vm.MouseMove(MyMouse.ShowMousePosition()));
            };
            _timer.Enabled = true;
//            Topmost = true;
        }

        private void ArucoOverlay_OnMouseMove(object sender, MouseEventArgs e)
        {
            _vm.MouseMove(MyMouse.ShowMousePosition());
        }

        private void ArucoOverlay_OnLoaded(object sender, RoutedEventArgs e)
        {
            var screen = Screen.PrimaryScreen;
            _vm.SetSize(screen.WorkingArea.Width, screen.WorkingArea.Height);
        }
    }
}
