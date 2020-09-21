using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using static System.Single;

namespace G3SDK
{
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

    public class G3SyncPortData
    {
        public TimeSpan TimeStamp { get; }
        public Direction Direction { get; }
        public int Value { get; }

        public G3SyncPortData(TimeSpan timeStamp, Direction direction, int value)
        {
            TimeStamp = timeStamp;
            Direction = direction;
            Value = value;
        }
    }
    public class G3Event
    {
        public TimeSpan TimeStamp { get; }
        public string Tag{ get; }
        public string Obj{ get; }

        public G3Event(TimeSpan timeStamp, string tag, string obj)
        {
            TimeStamp = timeStamp;
            Tag = tag;
            Obj = obj;
        }
    }

    public enum Direction
    {
        In,
        Out 
    }


    public class G3ImuData
    {
        public TimeSpan TimeStamp { get; }
        public Vector3D Accelerometer { get; }
        public Vector3D AngularVelocity { get; }
        public Vector3D Magnetometer { get; }

        public G3ImuData(TimeSpan timeStamp, Vector3D accelerometer, Vector3D angularVelocity, Vector3D magnetometer)
        {
            TimeStamp = timeStamp;
            Accelerometer = accelerometer;
            AngularVelocity = angularVelocity;
            Magnetometer = magnetometer;
        }
    }
    public class G3GazeData
    {
        public G3GazeData(TimeSpan timeStamp, Vector2D gaze2D, Vector3D gaze3D, EyeData leftEye, EyeData rightEye)
        {
            TimeStamp = timeStamp;
            Gaze2D = gaze2D;
            Gaze3D = gaze3D;
            LeftEye = leftEye;
            RightEye = rightEye;
        }

        public TimeSpan TimeStamp { get; }
        public EyeData LeftEye { get; }
        public EyeData RightEye { get; }
        public Vector2D Gaze2D { get; }
        public Vector3D Gaze3D { get; }
        public class EyeData
        {
            public EyeData(Vector3D gazeOrigin, Vector3D gazeDirection, float pupilDiameter)
            {
                GazeOrigin = gazeOrigin;
                GazeDirection = gazeDirection;
                PupilDiameter = pupilDiameter;
            }
            public Vector3D GazeOrigin { get; }
            public Vector3D GazeDirection { get; }
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
            {
                var start = data.First().TimeStamp;
                for (int i = 0; i < data.Count; i++)
                    data[i] = data[i].CloneWithTimestamp(data[i].TimeStamp - start);
                return true;
            }

            return false;
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
        public G3MarkerData(TimeSpan timeStamp, Vector2D marker2D, Vector3D marker3D)
        {
            TimeStamp = timeStamp;
            Marker2D = marker2D;
            Marker3D = marker3D;
        }

        public TimeSpan TimeStamp { get; }
        public Vector2D Marker2D { get; }
        public Vector3D Marker3D { get; }
    }

    public struct Vector2D
    {
        public Vector2D(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
        public bool IsValid => !IsNaN(X) && !IsNaN(Y);
        public bool IsZero => Math.Abs(X) < Epsilon && Math.Abs(Y) < Epsilon;

        public static Vector2D Invalid = new Vector2D(NaN, NaN);

        public override string ToString()
        {
            return $"X={X} Y={Y}";
        }

        public float Dist(Vector2D other)
        {
            return (float)Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2));
        }

        public Vector2D Interpolate(Vector2D other, double factor)
        {
            return new Vector2D(
                X.Interpolate(other.X, factor),
                Y.Interpolate(other.Y, factor));
        }
    }

    public static class FloatExtensions
    {
        public static float Interpolate(this float me, float other, double factor)
        {
            return (float) (me + (other - me) * factor);
        }
    }
    public static class Vector2DExtensions
    {
        public static Vector2D Average(this IEnumerable<Vector2D> points)
        {
            return new Vector2D(
                points.Sum(p => p.X) / points.Count(),
                points.Sum(p => p.Y) / points.Count()
            );
        }
    }

    public static class ArrayExtensions
    {
        public static Vector2D ToVector2D(this float[] arr)
        {
            return arr == null ? Vector2D.Invalid : new Vector2D(arr[0], arr[1]);
        }
        public static Vector3D ToVector3D(this float[] arr)
        {
            return arr == null ? Vector3D.Invalid : new Vector3D(arr[0], arr[1], arr[2]);
        }
    }


    public static class Vector3DExtensions
    {
        public static Vector3D Average(this IEnumerable<Vector3D> points)
        {
            return new Vector3D(
                points.Sum(p => p.X) / points.Count(),
                points.Sum(p => p.Y) / points.Count(),
                points.Sum(p => p.Z) / points.Count()
            );
        }
    }
    public struct Vector3D
    {
        public Vector3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public bool IsValid => !IsNaN(X) && !IsNaN(Y) && !IsNaN(Z);
        public bool IsZero => Math.Abs(X) < Epsilon && Math.Abs(Y) < Epsilon && Math.Abs(Z) < Epsilon;

        public override string ToString()
        {
            return $"X={X} Y={Y} Z={Z}";
        }

        public static Vector3D Invalid = new Vector3D(NaN, NaN, NaN);

        public Vector3D Normalize()
        {
            var length = Length();
            return new Vector3D(X / length, Y / length, Z / length);
        }

        public float Length()
        {
            var x = X;
            var y = Y;
            var z = Z;

            // Computation of length can overflow/underflow easily because it 
            // first computes squared length, so we first divide by
            // the largest coefficient.
            var maxCoordinate = Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z)));

            if (maxCoordinate.EqualByEpsilon(0.0f))
            {
                return 0;
            }

            x /= maxCoordinate;
            y /= maxCoordinate;
            z /= maxCoordinate;

            return (float)Math.Sqrt((x * x) + (y * y) + (z * z)) * maxCoordinate;
        }

        public bool EqualsWithinEpsilon(Vector3D other, double epsilon)
        {
            return (Math.Abs(X - other.X) < epsilon)
                   && (Math.Abs(Y - other.Y) < epsilon)
                   && (Math.Abs(Z - other.Z) < epsilon);
        }

        public double AngleInDegrees(Vector3D other)
        {
            var normalized1 = this.Normalize();
            var normalized2 = other.Normalize();
            return normalized1.AngleInDegreesToNormalized(normalized2);
        }

        /// <summary>
        /// Angle the between two normalized vectors.
        /// </summary>
        /// <param name="normalizedOther">The vector 2.</param>
        /// <returns>The angle between two points.</returns>
        private double AngleInDegreesToNormalized(Vector3D normalizedOther)
        {
            var ratio = DotProduct(normalizedOther);

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
            var length = Subtract(normalizedOther).Length();
            var theta = 2.0 * Math.Asin(Math.Min(length / 2.0, 1.0));
            if (ratio < 0)
            {
                theta = Math.PI - theta;
            }

            return theta * (180.0 / Math.PI);
        }

        public double DotProduct(Vector3D other)
        {
            return (X * other.X) +
                   (Y * other.Y) +
                   (Z * other.Z);
        }


        public Vector3D Subtract(Vector3D other)
        {
            return new Vector3D(X - other.X, Y - other.Y, Z - other.Z);
        }

        public Vector3D Average(Vector3D other)
        {
            return new Vector3D(
                (X + other.X) / 2,
                (Y + other.Y) / 2,
                (Z + other.Z) / 2);
        }
        public Vector3D Interpolate(Vector3D other, double factor)
        {
            return new Vector3D(
                X.Interpolate(other.X, factor),
                Y.Interpolate(other.Y, factor),
                Z.Interpolate(other.Z, factor));
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