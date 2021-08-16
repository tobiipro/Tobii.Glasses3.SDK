using System;
using System.Collections.Generic;
using System.Numerics;

namespace G3SDK
{
    public class CalibratedMagnetometer : IObservable<G3ImuData>
    {
        private bool _calibrating;
        private bool _calibrated;
        private IG3Api _api;
        private IDisposable _token;

        private MagData _x;
        private MagData _y;
        private MagData _z;
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
            _x = new MagData();
            _y = new MagData();
            _z = new MagData();
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
                UpdateCalib(_x, imu.Magnetometer.X);
                UpdateCalib(_y, imu.Magnetometer.Y);
                UpdateCalib(_z, imu.Magnetometer.Z);

                var avgDelta = (_x.AvgDelta + _y.AvgDelta + _z.AvgDelta) / 3;

                _x.Scale = avgDelta / _x.AvgDelta;
                _y.Scale = avgDelta / _y.AvgDelta;
                _z.Scale = avgDelta / _z.AvgDelta;
            }

            if (_calibrated)
            {
                var correctedX = (imu.Magnetometer.X - _x.Offset) * _x.Scale;
                var correctedY = (imu.Magnetometer.Y - _y.Offset) * _y.Scale;
                var correctedZ = (imu.Magnetometer.Z - _z.Offset) * _z.Scale;
                var mag = new Vector3(correctedX, correctedY, correctedZ);
                foreach(var obs in _observers.Values)
                    obs.OnNext(new G3ImuData(imu.TimeStamp, 
                        Vector3Extensions.INVALID, 
                        Vector3Extensions.INVALID, 
                        mag));
            }
        }

        private void UpdateCalib(MagData magData, float value)
        {
            magData.Min = Math.Min(magData.Min, value);
            magData.Max = Math.Max(magData.Max, value);
            magData.AvgDelta = (magData.Max - magData.Min) / 2;
            magData.Offset = (magData.Max + magData.Min) / 2;
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
