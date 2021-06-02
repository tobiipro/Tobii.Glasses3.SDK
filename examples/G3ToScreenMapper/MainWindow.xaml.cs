using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using G3SDK;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace G3ToScreenMapper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private G3Api _api;

        private readonly Brush _lineCol = new SolidColorBrush(Colors.Aqua);
        private readonly Brush _textCol = new SolidColorBrush(Colors.OrangeRed);
        private readonly Brush _red = new SolidColorBrush(Colors.Red);
        private readonly Brush _white = new SolidColorBrush(Colors.White);
        private ScreenMapper _screenMapper;
        private Timer _timer;
        private readonly ArucoOverlay _ovl;
        private readonly ArucoOverlayVM _arucoVM;

        public MainWindow()
        {
            InitializeComponent();

            SubscribeToSceneCamera();
            _arucoVM = new ArucoOverlayVM();
            _ovl = new ArucoOverlay(_arucoVM);
            _ovl.Show();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _ovl.Close();
        }

        private void InitLaptopScreen()
        {
            var width = 345;
            var height = 194;
            var markerSize = 20;
            _screenMapper.InitScreen(width, height, markerSize);
            _screenMapper.AddMarker(2, Pos.Left, height / 2 - markerSize / 2);
            _screenMapper.AddMarker(5, Pos.Right, height / 2 - markerSize / 2);
            _screenMapper.AddMarker(6, Pos.Bottom, width / 2 - markerSize / 2);
            _screenMapper.AddMarker(10, Pos.Top, width / 2 - markerSize);
        }

        private void Init30InchScreen()
        {
            var screenWidthInMM = 640;
            var screenHeightInMM = 400;

            var horizPixelsPerMM =_arucoVM.Width / screenWidthInMM;
            var vertPixelsPerMM =_arucoVM.Height/ screenHeightInMM;
            _screenMapper.InitScreen(screenWidthInMM, screenHeightInMM, _arucoVM.MarkerSize/horizPixelsPerMM);


            foreach (var m in _arucoVM.Images)
            {
                _screenMapper.AddMarkerXY(m.Id, m.X/horizPixelsPerMM, m.Y/vertPixelsPerMM);
            }
        }

        private async void SubscribeToSceneCamera()
        {
            var browser = new G3Browser();
            _api = null;
            while (_api == null)
            {
                var devices = await browser.ProbeForDevices();
                if (devices.Any())
                    _api = devices.First();
                await Task.Delay(100);
            }

            _btnCalibrate.IsEnabled = true;
            _screenMapper = new ScreenMapper(_api, Dispatcher);
            _screenMapper.OnImageResults += ScreenMapperOnOnImageResults;
            _screenMapper.OnWarpedImage += (sender, source) =>
            {
                _warpedImage.Source = source;
                _warpedImage.Height = source.Height / 2;
                _warpedImage.Width = source.Width / 2;
                _warpedCanvas.Width = _warpedImage.Width;
                _warpedCanvas.Height = _warpedImage.Height;

            };
            _screenMapper.OnImage += (sender, source) =>
            {
                _img.Source = source;
                _img.Height = source.Height / 2;
                _img.Width = source.Width / 2;
                _gazeCanvas.Width = _img.Width;
                _gazeCanvas.Height = _img.Height;
                _markerCanvas.Width = _img.Width;
                _markerCanvas.Height = _img.Height;
            };

            Init30InchScreen();
            //InitHuawei();
            var gazeToken = await _api.Rudimentary.Gaze.SubscribeAsync(GazeReceived);

            _timer = new Timer(1000);
            _timer.Elapsed += async (sender, args) =>
            {
                await _api.Rudimentary.Keepalive();
            };
            _timer.Enabled = true;

            _screenMapper.Start();


        }

        private void ScreenMapperOnOnImageResults(object sender, ImageResults e)
        {
            _markerCanvas.Children.Clear();
            DrawCorners(-1, e.MappedScreen, _white, _markerCanvas, e.VideoSize);

            for (var c = 0; c < e.MarkerCorners.Length; c++)
            {
                DrawCorners(e.MarkerIds[c], e.MarkerCorners[c], _lineCol, _markerCanvas, e.VideoSize);
            }

            for (var c = 0; c < e.Rejected.Length; c++)
            {
                DrawCorners(-1, e.Rejected[c], _textCol, _markerCanvas, e.VideoSize);
            }
        }

        private void GazeReceived(G3GazeData g3GazeData)
        {
            Dispatcher.Invoke(() =>
            {
                SetGazeEllipsePos(_gazeMarker, g3GazeData.Gaze2D, _gazeCanvas);
                SetGazeEllipsePos(_gazeMarker2, g3GazeData.Gaze2D, _gazeCanvas);

                var warpedGaze2D = _screenMapper.MapFromNormalizedVideoToNormalizedWarpedImage(g3GazeData.Gaze2D);

                SetGazeEllipsePos(_warpedGazeMarker, warpedGaze2D, _warpedCanvas);
                SetGazeEllipsePos(_warpedGazeMarker2, warpedGaze2D, _warpedCanvas);
            });
        }

        private void SetGazeEllipsePos(Ellipse gaze, Vector2 gaze2D, Canvas canvas)
        {
            Canvas.SetLeft(gaze, gaze2D.IsValid() ? (gaze2D.X * canvas.Width) - gaze.Width / 2 : -1000);
            Canvas.SetTop(gaze, gaze2D.IsValid() ? gaze2D.Y * canvas.Height - gaze.Height / 2 : -1000);
        }

        public static BitmapImage ByteArrayToBitmapImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
            var image = new BitmapImage();
            using (var mem = new MemoryStream(imageData))
            {
                mem.Position = 0;
                image.BeginInit();
                image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = null;
                image.StreamSource = mem;
                image.EndInit();
            }

            image.Freeze();
            return image;
        }

        public static Mat ByteArrayToOpenCVImage(byte[] img)
        {
            return Cv2.ImDecode(img, ImreadModes.Color);
        }


        private void DrawCorners(int id, Point2f[] corners, Brush color, Canvas canvas, Size videoSize)
        {
            var scaleX = canvas.ActualWidth / videoSize.Width;
            var scaleY = canvas.ActualHeight / videoSize.Height;
            for (int i = 0; i < corners.Length; i++)
            {
                var c1 = corners[i];
                var c2 = corners[(i + 1) % corners.Length];
                canvas.Children.Add(new Line()
                {
                    X1 = c1.X * scaleX,
                    X2 = c2.X * scaleX,
                    Y1 = c1.Y * scaleY,
                    Y2 = c2.Y * scaleY,
                    Stroke = color,
                    StrokeThickness = 2
                });
            }

            if (corners.Length > 0)
            {
                canvas.Children.Add(new Line()
                {
                    X1 = corners[0].X * scaleX - 4,
                    X2 = corners[0].X * scaleX + 4,
                    Y1 = corners[0].Y * scaleY - 4,
                    Y2 = corners[0].Y * scaleY + 4,
                    Stroke = _red,
                    StrokeThickness = 3
                });
                if (id >= 0)
                {
                    var x = corners.Average(corn => corn.X) * scaleX;
                    var y = corners.Average(corn => corn.Y) * scaleY;
                    Text(x, y, id.ToString());
                }
            }
        }

        private void Text(double x, double y, string text)
        {

            var textBlock = new TextBlock { Text = text, Foreground = _textCol, FontSize = 10 };

            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);

            _markerCanvas.Children.Add(textBlock);
        }


        private async void Button_Click_1(object sender, System.Windows.RoutedEventArgs e)
        {
            var res = await _api.Calibrate.Run();
            Dispatcher.Invoke(() =>
            {
                _btnCalibrate.Background = new SolidColorBrush(res ? Colors.Green : Colors.Red);
            });
        }
    }
}
