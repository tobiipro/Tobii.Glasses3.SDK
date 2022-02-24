using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace G3SDK
{
    public class CalibratedMagnetometer : IObservable<G3ImuData>
    {
        private bool _calibrating;
        private bool _calibrated;
        private IG3Api _api;
        private IDisposable _token;
        private MagnetometerCalibration _magcalib = new MagnetometerCalibration();

        private readonly Dictionary<int, IObserver<G3ImuData>> _observers = new Dictionary<int, IObserver<G3ImuData>>();
        private int _observerCounter;
        private int _startCounter;

        public CalibratedMagnetometer(IG3Api api)
        {
            _api = api;
        }

        public void Start()
        {
            if (_token == null)
            {
                _token = _api.Rudimentary.Imu.Subscribe(ReceiveImu);
            }

            _startCounter++;
            _magcalib.ResetCalibration();
        }

        public void Stop()
        {
            _startCounter--;
            if (_startCounter == 0)
            {
                _token.Dispose();
                _token = null;
            }
        }

        public void StartCalibration()
        {
            _calibrating = true;
        }

        public void EndCalibration()
        {
            _calibrating = false;
            _calibrated = true;
        }

        protected void ReceiveImu(G3ImuData imu)
        {
            if (!imu.Magnetometer.IsValid())
                return;

            if (_calibrating)
            {
                _magcalib.AddSampleToCalibration(imu.Magnetometer);
            }

            if (_calibrated)
            {
                var calibratedMagData = _magcalib.CalibrateMagnetometerData(imu.Magnetometer);
                foreach(var obs in _observers.Values)
                    obs.OnNext(new G3ImuData(imu.TimeStamp, 
                        Vector3Extensions.INVALID, 
                        Vector3Extensions.INVALID, 
                        calibratedMagData));
            }
        }

        public IDisposable Subscribe(IObserver<G3ImuData> observer)
        {
            _observerCounter++;
            _observers[_observerCounter] = observer;
            return new ObserverToken(this, _observerCounter);
        }

        private void Unsubscribe(int observerId)
        {
            _observers.Remove(observerId);
        }

        internal class ObserverToken : IDisposable
        {
            private readonly CalibratedMagnetometer _calibratedMagnetometer;
            private readonly int _observerId;

            public ObserverToken(CalibratedMagnetometer calibratedMagnetometer, int observerId)
            {
                _calibratedMagnetometer = calibratedMagnetometer;
                _observerId = observerId;
            }

            public void Dispose()
            {
                _calibratedMagnetometer.Unsubscribe(_observerId);
            }
        }
    }

    public class MagnetometerCalibration
    {
        private MagData _x;
        private MagData _y;
        private MagData _z;

        public MagnetometerCalibration()
        {
            ResetCalibration();
        }

        public void ResetCalibration()
        {
            _x = new MagData();
            _y = new MagData();
            _z = new MagData();
        }

        public Vector3 CalibrateMagnetometerData(Vector3 magnetometer)
        {
            var correctedX = (magnetometer.X - _x.Offset) * _x.Scale;
            var correctedY = (magnetometer.Y - _y.Offset) * _y.Scale;
            var correctedZ = (magnetometer.Z - _z.Offset) * _z.Scale;
            return new Vector3(correctedX, correctedY, correctedZ);
        }

        public string SaveCalibration()
        {
            var sb = new StringBuilder();
            WriteCalibration(sb, _x);
            WriteCalibration(sb, _y);
            WriteCalibration(sb, _z);
            return sb.ToString();
        }

        public void LoadCalibration(string calib)
        {
            var lines = calib.Split('\n');
            LoadCalibration(lines, 0, _x);
            LoadCalibration(lines, 2, _y);
            LoadCalibration(lines, 4, _z);
        }

        private void LoadCalibration(string[] lines, int index, MagData magData)
        {
            float.TryParse(lines[index], NumberStyles.Float, CultureInfo.InvariantCulture, out magData.Offset);
            float.TryParse(lines[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out magData.Scale);
        }

        private void WriteCalibration(StringBuilder sb, MagData magData)
        {
            sb.AppendLine(magData.Offset.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine(magData.Scale.ToString(CultureInfo.InvariantCulture));
        }

        public void AddSampleToCalibration(Vector3 magnetometer)
        {
            UpdateCalib(_x, magnetometer.X);
            UpdateCalib(_y, magnetometer.Y);
            UpdateCalib(_z, magnetometer.Z);

            var avgDelta = (_x.AvgDelta + _y.AvgDelta + _z.AvgDelta) / 3;

            _x.Scale = avgDelta / _x.AvgDelta;
            _y.Scale = avgDelta / _y.AvgDelta;
            _z.Scale = avgDelta / _z.AvgDelta;
        }

        private void UpdateCalib(MagData magData, float value)
        {
            magData.Min = Math.Min(magData.Min, value);
            magData.Max = Math.Max(magData.Max, value);
            magData.AvgDelta = (magData.Max - magData.Min) / 2;
            magData.Offset = (magData.Max + magData.Min) / 2;
        }

        private class MagData
        {
            public float Min = float.MaxValue;
            public float Max = float.MinValue;
            public float AvgDelta;
            public float Offset;
            public float Scale;
        }
    }
}
