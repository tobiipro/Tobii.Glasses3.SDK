using System.Collections.Generic;
using OpenCvSharp;

namespace G3ToScreenMapper
{
    public class MarkerPos
    {
        private readonly int _id;
        private readonly ScreenWithMarkers _screenWithMarkers;
        public float Top { get; }
        public float Left { get; }

        public float Bottom => Top + _screenWithMarkers.MarkerSize;
        public float Right => Left + _screenWithMarkers.MarkerSize;

        public IEnumerable<Point2f> Corners { get; }

        public MarkerPos(int id, Pos p, int from00, ScreenWithMarkers screenWithMarkers)
        {
            _id = id;
            _screenWithMarkers = screenWithMarkers;
            switch (p)
            {
                case Pos.Top:
                    Left = from00;
                    Top = -_screenWithMarkers.MarkerSize;
                    break;
                case Pos.Bottom:
                    Left = from00;
                    Top = _screenWithMarkers.Height;
                    break;
                case Pos.Left:
                    Left = -_screenWithMarkers.MarkerSize;
                    Top = from00;
                    break;
                case Pos.Right:
                    Left = _screenWithMarkers.Width;
                    Top = from00;
                    break;
            }

            Corners = new[]
            {
                new Point2f(Left, Top),
                new Point2f(Right, Top),
                new Point2f(Right, Bottom),
                new Point2f(Left, Bottom),
            };
        }

        public MarkerPos(int id, float x, float y, ScreenWithMarkers screenWithMarkers)
        {
            _id = id;
            _screenWithMarkers = screenWithMarkers;
            Top = y;
            Left = x;
            Corners = new[]
            {
                new Point2f(Left, Top),
                new Point2f(Right, Top),
                new Point2f(Right, Bottom),
                new Point2f(Left, Bottom),
            };


        }
    }
}