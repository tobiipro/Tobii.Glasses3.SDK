using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using G3SDK;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using Task = System.Threading.Tasks.Task;

namespace G3ToScreenMapper
{
    public class ScreenMapper
    {
        private readonly G3Api _api;
        private readonly Dispatcher _dispatcher;
        private VideoCapture _rtspSource;
        private ScreenWithMarkers _screen;
        private Mat _videoToScreen;
        private Mat _screenToVideo;
        private Size _videoSize;
        private Mat _videoToWarped;


        public ScreenMapper(G3Api api, Dispatcher dispatcher)
        {
            _api = api;
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            _rtspSource = new VideoCapture(_api.LiveRtspUrl(), VideoCaptureAPIs.FFMPEG);
            Task.Run(ListenToFrames);

        }

        private void ListenToFrames()
        {
            while (true)
            {
                var g = _rtspSource.Grab();
                if (g)
                {
                    var mat = _rtspSource.RetrieveMat();
                    if (_videoSize.Width == 0)
                        _videoSize = new Size(mat.Width, mat.Height);
                    FindMarkersInImage(mat);
                }

                Task.Delay(5);
            }
        }

        private void FindMarkersInImage(Mat openCvImage)
        {
            var sw = new Stopwatch();
            sw.Start();
            var parameters = DetectorParameters.Create();

            var dict = CvAruco.GetPredefinedDictionary(PredefinedDictionaryName.Dict6X6_1000);
            CvAruco.DetectMarkers(openCvImage, dict, out var markers, out var ids, parameters, out var rejected);

            var VideoPoints = new List<Point2d>();
            var screenPoints = new List<Point2d>();
            for (int i = 0; i < ids.Length; i++)
            {
                var id = ids[i];
                if (_screen.Markers.ContainsKey(id))
                {
                    var videoMarker = markers[i];
                    foreach (var videoMarkerCorners in videoMarker)
                    {
                        VideoPoints.Add(new Point2d(videoMarkerCorners.X, videoMarkerCorners.Y));
                    }

                    //NB! physical markers must have first corner in top left 
                    var screenMarker = _screen.Markers[id];
                    foreach (var screenMarkerCorner in screenMarker.Corners)
                        screenPoints.Add(new Point2d(screenMarkerCorner.X, screenMarkerCorner.Y));
                }
            }
            var warpedPoints = screenPoints.Select(ScreenToWarped).ToList();

            if (screenPoints.Count >= 3)
            {
                var t = Cv2.FindHomography(VideoPoints, screenPoints);
                if (t.Cols == 3)
                {
                    _videoToScreen = t;
                    _screenToVideo = Cv2.FindHomography(screenPoints, VideoPoints);
                    _videoToWarped = Cv2.FindHomography(VideoPoints, warpedPoints);
                }
            }

            sw.Stop();


            if (OnImage != null)
            {
                _dispatcher.Invoke(() => OnImage?.Invoke(this, MatToBitmap(openCvImage)));
            }

            if (_videoToScreen != null && OnWarpedImage != null)
            {
                var warpedImage = openCvImage.EmptyClone();
                Cv2.WarpPerspective(openCvImage, warpedImage, _videoToWarped, warpedImage.Size());
                _dispatcher.Invoke(() => OnWarpedImage?.Invoke(this, MatToBitmap(warpedImage)));
            }

            if (OnImageResults != null)
            {
                var imageResults = new ImageResults(
                    _screenToVideo != null ? Cv2.PerspectiveTransform(_screen.Coords, _screenToVideo) : new Point2f[0],
                    markers,
                    rejected,
                    ids, _videoSize);
                _dispatcher.Invoke(() => OnImageResults?.Invoke(this, imageResults));
            }
        }

        private Point2d ScreenToNormalizedWarped(Point2d p)
        {
            return new Point2d((p.X / _screen.Width + 1d) / 3, (p.Y / _screen.Height + 1d) / 3);
        }

        private Point2d ScreenToWarped(Point2d p)
        {
            return new Point2d(((p.X / _screen.Width + 1d) / 3) * _videoSize.Width, ((p.Y / _screen.Height + 1d) / 3) * _videoSize.Height);
        }

        private Point2d NormalizedScreenToNormalizedWarped(Point2d p)
        {
            return new Point2d((p.X + 1) / 3, (p.Y + 1) / 3);
        }

        public event EventHandler<BitmapSource> OnWarpedImage;
        public event EventHandler<BitmapSource> OnImage;

        public static BitmapSource MatToBitmap(Mat image)
        {
            return OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToBitmapSource(image);
        }


        public event EventHandler<ImageResults> OnImageResults;

        public void InitScreen(int width, int height, float markerSize)
        {
            _screen = new ScreenWithMarkers(width, height, markerSize);
        }

        public void AddMarker(int id, Pos p, int dist)
        {
            _screen.AddMarker(id, p, dist);
        }
        public void AddMarkerXY(int id, float x, float y)
        {
            _screen.AddMarkerXY(id, x, y);
        }

        public Vector2 MapFromNormalizedVideoToNormalizedWarpedImage(Vector2 normalizedGaze2D)
        {
            if (normalizedGaze2D.IsValid() && _videoToScreen != null)
            {
                var gazeInVideoPixels = new Point2f(normalizedGaze2D.X * _videoSize.Width, normalizedGaze2D.Y * _videoSize.Height);

                var gazeInWarpedCoords = Cv2.PerspectiveTransform(new[] { gazeInVideoPixels }, _videoToWarped).Last();

                return new Vector2(gazeInWarpedCoords.X/_videoSize.Width, gazeInWarpedCoords.Y/_videoSize.Height);
            }
            return Vector2Extensions.INVALID;
        }
    }

    public class ImageResults
    {
        public Point2f[] MappedScreen { get; }
        public Point2f[][] MarkerCorners { get; }
        public Point2f[][] Rejected { get; }
        public int[] MarkerIds { get; }
        public Size VideoSize { get; }

        public ImageResults(Point2f[] mappedScreen, Point2f[][] markerCorners, Point2f[][] rejected, int[] markerIds,
            Size videoSize)
        {
            MappedScreen = mappedScreen;
            MarkerCorners = markerCorners;
            Rejected = rejected;
            MarkerIds = markerIds;
            VideoSize = videoSize;
        }
    }
}