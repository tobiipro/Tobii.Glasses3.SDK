using System.Numerics;
using System.Runtime.InteropServices;

namespace G3ToScreenMapper
{
    class MyMouse
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        public static Vector2 ShowMousePosition()
        {
            POINT point;
            if (GetCursorPos(out point))
            {
                return new Vector2(point.X, point.Y);
            }

            return new Vector2(float.NaN, float.NaN);
        }
    }

    public struct POINT
    {
        public int X;
        public int Y;
    }

}
