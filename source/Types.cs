using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using static System.Single;

namespace G3SDK
{
    public interface IG3TimeStamped
    {
        TimeSpan TimeStamp { get; }
    }

    public class G3TimeStamped : IG3TimeStamped
    {
        protected G3TimeStamped(in TimeSpan timeStamp)
        {
            TimeStamp = timeStamp;
        }

        public TimeSpan TimeStamp { get; }
    }
    

    public enum SpaceState
    {
        VeryLow,
        Low,
        Good,
        Unknown
    };

    [Flags]
    public enum CardState
    {
        NotInserted,
        Available,
        ReadOnly,
        Busy,
        Error
    }

    public class WebSockMsg
    {
        public long? id { get; set; }
        public long? signal { get; set; }
        public long? error { get; set; }
        public string message { get; set; }
        public object body { get; set; }
    }

    public enum Method
    {
        GET, POST
    }

    public class G3SyncPortData : G3TimeStamped
    {
        public Direction Direction { get; }
        public int Value { get; }

        public G3SyncPortData(TimeSpan timeStamp, Direction direction, int value): base(timeStamp)
        {
            Direction = direction;
            Value = value;
        }
    }
    public class G3Event : G3TimeStamped
    {
        public string Tag { get; }
        public string Obj { get; }

        public G3Event(TimeSpan timeStamp, string tag, string obj): base(timeStamp)
        {
            Tag = tag;
            Obj = obj;
        }
    }

    public enum Direction
    {
        In,
        Out
    }

    public class G3ImuData : G3TimeStamped
    {
        public Vector3 Accelerometer { get; }
        public Vector3 Gyroscope { get; }
        public Vector3 Magnetometer { get; }

        public G3ImuData(TimeSpan timeStamp, Vector3 accelerometer, Vector3 gyroscope, Vector3 magnetometer): base(timeStamp)
        {
            Accelerometer = accelerometer;
            Gyroscope = gyroscope;
            Magnetometer = magnetometer;
        }
    }

    public class G3GazeData: G3TimeStamped
    {
        public G3GazeData(TimeSpan timeStamp, Vector2 gaze2D, Vector3 gaze3D, EyeData leftEye, EyeData rightEye): base(timeStamp)
        {
            Gaze2D = gaze2D;
            Gaze3D = gaze3D;
            LeftEye = leftEye;
            RightEye = rightEye;
        }

        public EyeData LeftEye { get; }
        public EyeData RightEye { get; }
        public Vector2 Gaze2D { get; }
        public Vector3 Gaze3D { get; }
        public class EyeData
        {
            public EyeData(Vector3 gazeOrigin, Vector3 gazeDirection, float pupilDiameter)
            {
                GazeOrigin = gazeOrigin;
                GazeDirection = gazeDirection;
                PupilDiameter = pupilDiameter;
            }
            public Vector3 GazeOrigin { get; }
            public Vector3 GazeDirection { get; }
            public float PupilDiameter { get; }
        }
    }

    public static class G3SyncPortDataExtensions
    {
        public static G3SyncPortData CloneWithTimestamp(this G3SyncPortData d, TimeSpan t)
        {
            return new G3SyncPortData(t, d.Direction, d.Value);
        }

        public static bool OffsetToZero(this List<G3SyncPortData> data)
        {
            var start = data.First().TimeStamp;
            for (int i = 0; i < data.Count; i++)
                data[i] = data[i].CloneWithTimestamp(data[i].TimeStamp - start);
            return true;
        }
    }

    public static class G3GazeDataExtensions
    {
        public static G3GazeData CloneWithTimestamp(this G3GazeData g, TimeSpan t)
        {
            return new G3GazeData(t, g.Gaze2D, g.Gaze3D, g.LeftEye, g.RightEye);
        }

        public static bool OffsetToZero(this List<G3GazeData> gaze)
        {
            if (gaze.Any() && gaze.First().TimeStamp > TimeSpan.FromMilliseconds(500))
            {
                var start = gaze.First().TimeStamp;
                for (int i = 0; i < gaze.Count; i++)
                    gaze[i] = gaze[i].CloneWithTimestamp(gaze[i].TimeStamp - start);
                return true;
            }

            return false;
        }
    }

    [DebuggerDisplay("{TimeStamp} {Marker2D} {Marker3D}")]
    public class G3MarkerData
    {
        public G3MarkerData(TimeSpan timeStamp, Vector2 marker2D, Vector3 marker3D)
        {
            TimeStamp = timeStamp;
            Marker2D = marker2D;
            Marker3D = marker3D;
        }

        public TimeSpan TimeStamp { get; }
        public Vector2 Marker2D { get; }
        public Vector3 Marker3D { get; }
    }

    public static class FloatExtensions
    {
        public static float Interpolate(this float me, float other, double factor)
        {
            return (float)(me + (other - me) * factor);
        }
    }

    public static class Vector2Extensions
    {
        public static Vector2 INVALID = new Vector2(float.NaN, float.NaN);
        public static Vector2 Average(this IEnumerable<Vector2> points)
        {
            return new Vector2(
                points.Sum(p => p.X) / points.Count(),
                points.Sum(p => p.Y) / points.Count()
            );
        }
        public static bool IsValid(this Vector2 me) => !IsNaN(me.X) && !IsNaN(me.Y);
        public static bool IsZero(this Vector2 me) => Math.Abs(me.X) < Epsilon && Math.Abs(me.Y) < Epsilon;
        public static Vector2 Interpolate(this Vector2 me, Vector2 other, double factor)
        {
            return new Vector2(
                me.X.Interpolate(other.X, factor),
                me.Y.Interpolate(other.Y, factor));
        }
    }

    public static class Vector3Extensions
    {
        public static Vector3 INVALID = new Vector3(float.NaN, float.NaN, float.NaN);
        public static bool IsValid(this Vector3 me) => !IsNaN(me.X) && !IsNaN(me.Y) && !IsNaN(me.Z);

        public static double AngleInDegrees(this Vector3 me, Vector3 other)
        {
            var normalized1 = Vector3.Normalize(me);
            var normalized2 = Vector3.Normalize(other);
            return normalized1.AngleInDegreesToNormalized(normalized2);
        }

        /// <summary>
        /// Angle the between two normalized vectors.
        /// </summary>
        /// <param name="normalizedOther">The vector 2.</param>
        /// <returns>The angle between two points.</returns>
        private static double AngleInDegreesToNormalized(this Vector3 me, Vector3 normalizedOther)
        {
            var ratio = Vector3.Dot(me, normalizedOther);

            // The "straight forward" method of acos(u.v) has large precision
            // issues when the dot product is near +/-1.  This is due to the 
            // steep slope of the acos function as we approach +/- 1.  Slight 
            // precision errors in the dot product calculation cause large
            // variation in the output value. 
            //
            //        |                   |
            //         \__                |
            //            ---___          | 
            //                  ---___    |
            //                        ---_|_ 
            //                            | ---___ 
            //                            |       ---___
            //                            |             ---__ 
            //                            |                  \
            //                            |                   |
            //       -|-------------------+-------------------|-
            //       -1                   0                   1 
            //
            //                         acos(x) 
            // 
            // To avoid this we use an alternative method which finds the
            // angle bisector by (u-v)/2: 
            //
            //                            _>
            //                       u  _-  \ (u-v)/2
            //                        _-  __-v 
            //                      _=__--
            //                    .=-----------> 
            //                            v 
            //
            // Because u and v and unit vectors, (u-v)/2 forms a right angle 
            // with the angle bisector.  The hypotenuse is 1, therefore
            // 2*asin(|u-v|/2) gives us the angle between u and v.
            //
            // The largest possible value of |u-v| occurs with perpendicular 
            // vectors and is sqrt(2)/2 which is well away from extreme slope
            // at +/-1. 
            var length = (me - normalizedOther).Length();
            var theta = 2.0 * Math.Asin(Math.Min(length / 2.0, 1.0));
            if (ratio < 0)
            {
                theta = Math.PI - theta;
            }

            return theta * (180.0 / Math.PI);
        }
        public static Vector3 Average(this IEnumerable<Vector3> points)
        {
            return new Vector3(
                points.Sum(p => p.X) / points.Count(),
                points.Sum(p => p.Y) / points.Count(),
                points.Sum(p => p.Z) / points.Count()
            );
        }

        public static Vector3 Interpolate(this Vector3 me, Vector3 other, double factor)
        {
            return new Vector3(
                me.X.Interpolate(other.X, factor),
                me.Y.Interpolate(other.Y, factor),
                me.Z.Interpolate(other.Z, factor));
        }

        public static Vector3 Average(this Vector3 me, Vector3 other)
        {
            return new Vector3(
                (me.X + other.X) / 2,
                (me.Y + other.Y) / 2,
                (me.Z + other.Z) / 2);
        }

        public static Vector3 Normalize(this Vector3 me)
        {
            return Vector3.Normalize(me);
        }

        public static bool IsZero(this Vector3 me) => Math.Abs(me.X) < Epsilon && Math.Abs(me.Y) < Epsilon && Math.Abs(me.Z) < Epsilon;

    }

    public static class ArrayExtensions
    {
        public static Vector2 ToVector2D(this float[] arr)
        {
            return arr == null ? Vector2Extensions.INVALID : new Vector2(arr[0], arr[1]);
        }
        public static Vector3 ToVector3D(this float[] arr)
        {
            return arr == null ? Vector3Extensions.INVALID : new Vector3(arr[0], arr[1], arr[2]);
        }
    }

    public enum LogLevel
    {
        verbose, debug, info, warning, error
    }

    public class StorageState
    {
        public StorageState(SpaceState spaceState, CardState cardState)
        {
            SpaceState = spaceState;
            CardState = cardState;
        }

        public SpaceState SpaceState { get; }
        public CardState CardState { get; }
    }

    /// <summary>
    /// Extension class to double
    /// </summary>
    public static class DoubleExtension
    {
        public static bool EqualByEpsilon(this double first, double second, double epsilon)
        {
            return Math.Abs(first - second) < epsilon;
        }

        public static bool EqualByEpsilon(this double first, double second)
        {
            return EqualByEpsilon(first, second, double.MinValue);
        }
    }

    public static class FloatExtension
    {
        public static bool EqualByEpsilon(this float first, float second, float epsilon)
        {
            return Math.Abs(first - second) < epsilon;
        }

        public static bool EqualByEpsilon(this float first, float second)
        {
            return EqualByEpsilon(first, second, MinValue);
        }
    }
}