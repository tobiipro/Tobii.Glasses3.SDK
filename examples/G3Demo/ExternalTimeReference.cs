using System;

namespace G3Demo
{
    internal class ExternalTimeReference
    {
        public DateTime UtcTime { get; }
        public DateTime LocalTime { get; }
        public string MachineName { get; }
        public double LastExternalTimeRoundtrip { get; }
        public int Index { get; }

        public ExternalTimeReference(DateTime utcTime,
            DateTime localTime,
            string machineName,
            double lastExternalTimeRoundtrip,
            int index)
        {
            UtcTime = utcTime;
            LocalTime = localTime;
            MachineName = machineName;
            LastExternalTimeRoundtrip = lastExternalTimeRoundtrip;
            Index = index;
        }
    }
}