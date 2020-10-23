﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class ParserHelpers
    {
        public static readonly Dictionary<string, CardState> CardStateTranslations = new Dictionary<string, CardState>();

        static ParserHelpers()
        {
            CardStateTranslations["not-inserted"] = CardState.NotInserted;
            CardStateTranslations["available"] = CardState.Available;
            CardStateTranslations["read-only"] = CardState.ReadOnly;
            CardStateTranslations["busy"] = CardState.Busy;
            CardStateTranslations["error"] = CardState.Error;
        }

        public static G3GazeData ParseGazeFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj["type"].Value<string>() == "gaze")
            {
                var timeStamp = obj["timestamp"].Value<float>();
                var data = obj["data"] as JObject;
                return ParseGazeData(data, timeStamp);
            }

            return null;
        }

        public static G3ImuData ParseImuFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj["type"].Value<string>() == "imu")
            {
                var timeStamp = obj["timestamp"].Value<float>();
                var data = obj["data"] as JObject;
                return ParseImuData(data, timeStamp);
            }

            return null;
        }

        public static G3SyncPortData ParseSyncPortFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj["type"].Value<string>() == "syncport")
            {
                var timeStamp = obj["timestamp"].Value<float>();
                var data = obj["data"] as JObject;
                return ParseSyncPortData(data, timeStamp);
            }

            return null;
        }
        public static G3Event ParseEventFromJson(string json)
        {
            var obj = (JObject)JsonConvert.DeserializeObject(json);
            if (obj.Count == 3 && obj["type"].Value<string>() == "event")
            {
                var timeStamp = obj["timestamp"].Value<float>();
                var data = obj["data"] as JObject;
                return ParseEvent(data, timeStamp);
            }

            return null;
        }

        public static G3SyncPortData ParseSyncPortData(JObject data, float ts)
        {
            var dir = ParseEnum(data["direction"].Value<string>(), Direction.In);
            var value = data["value"].Value<int>();
            return new G3SyncPortData(TimeSpan.FromSeconds(ts), dir, value);
        }

        public static G3Event ParseEvent(JObject data, float ts)
        {
            var tag = data["tag"].Value<string>();
            var obj = data["object"];
            return new G3Event(TimeSpan.FromSeconds(ts), tag, JsonConvert.SerializeObject(obj));
        }

        public static G3GazeData ParseGazeData(JObject data, float timeStamp)
        {
            var gaze2D = (data["gaze2d"] as JArray).Arr2Vector2();
            var gaze3D = (data["gaze3d"] as JArray).Arr2Vector3();
            var eyeRight = ParseEyeData(data["eyeright"] as JObject);
            var eyeLeft = ParseEyeData(data["eyeleft"] as JObject);
            return new G3GazeData(TimeSpan.FromSeconds(timeStamp), gaze2D, gaze3D, eyeRight, eyeLeft);
        }

        public static G3ImuData ParseImuData(JObject data, float timeStamp)
        {
            var anglvel = ParseV3(data["gyroscope"]);
            var accel = ParseV3(data["accelerometer"]);
            var magn = ParseV3(data["magnetometer"]);
            return new G3ImuData(TimeSpan.FromSeconds(timeStamp), accel, anglvel, magn);
        }

        private static Vector3 ParseV3(JToken jToken)
        {
            if (jToken == null)
                return Vector3Extensions.INVALID;
            return (jToken as JArray).Arr2Vector3();
        }

        public static G3GazeData.EyeData ParseEyeData(JObject eyeData)
        {
            if (eyeData == null)
                return null;
            var gazeOrigin = (eyeData["gazeorigin"] as JArray).Arr2Vector3();
            var gazeDirection = (eyeData["gazedirection"] as JArray).Arr2Vector3();
            var pupilDiameter = eyeData["pupildiameter"]?.Value<float>() ?? Single.NaN;
            return new G3GazeData.EyeData(gazeOrigin, gazeDirection, pupilDiameter);
        }

        public static CardState ParseCardState(string arg)
        {
            var values = arg.Split('+');
            CardState res = 0;
            foreach (var v in values)
            {
                if (CardStateTranslations.TryGetValue(v, out var flag))
                    res |= flag;
            }

            return res;
        }

        public static SpaceState ParseSpaceState(string s)
        {
            return (SpaceState)Enum.Parse(typeof(SpaceState), s, true);
        }

        public static BatteryState ParseBatteryState(string s)
        {
            return (BatteryState)Enum.Parse(typeof(BatteryState), s, true);
        }

        public static TimeSpan ParseTimeSpan(string s)
        {
            return TimeSpan.FromSeconds(float.Parse(s, CultureInfo.InvariantCulture));
        }

        public static DateTime? ParseDateOptional(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            if (s == "null")
                return null;
            return DateTime.Parse(s);
        }
        public static DateTime ParseDate(string s)
        {
            return DateTime.Parse(s);
        }

        public static TimeSpan? ParseDurationToTimespan(string s)
        {
            var seconds = float.Parse(s, CultureInfo.InvariantCulture);
            if (seconds <= 0)
                return null;
            return TimeSpan.FromSeconds(seconds);
        }

        public static List<G3SyncPortData> ParseEventDataFromCompressedStream(Stream compressedData)
        {
            var result = new List<G3SyncPortData>();

            using (var eventData = new GZipStream(compressedData, CompressionMode.Decompress))
            using (var x = new StreamReader(eventData))
            {
                while (!x.EndOfStream)
                {
                    var line = x.ReadLine();
                    var g3SyncPortData = ParseSyncPortFromJson(line);
                    if (g3SyncPortData != null)
                        result.Add(g3SyncPortData);
                }
            }
            return result;
        }
        public static List<G3ImuData> ParseImuDataFromCompressedStream(Stream compressedData)
        {
            var result = new List<G3ImuData>();

            using (var imuData = new GZipStream(compressedData, CompressionMode.Decompress))
            using (var x = new StreamReader(imuData))
            {
                while (!x.EndOfStream)
                {
                    var line = x.ReadLine();
                    var g3ImuData = ParseImuFromJson(line);
                    if (g3ImuData != null)
                        result.Add(g3ImuData);
                }
            }
            return result;
        }

        public static List<G3GazeData> ParseGazeDataFromCompressedStream(Stream compressedData)
        {
            var result = new List<G3GazeData>();

            using (var gazeData = new GZipStream(compressedData, CompressionMode.Decompress))
            using (var x = new StreamReader(gazeData))
            {
                while (!x.EndOfStream)
                {
                    var line = x.ReadLine();
                    var g3GazeData = ParseGazeFromJson(line);
                    if (g3GazeData != null)
                        result.Add(g3GazeData);
                }
            }
            return result;
        }

        public static Guid ParseGuid(string arg)
        {
            if (arg == "null")
                return Guid.Empty;
            return Guid.Parse(arg);
        }

        public static T ParseEnum<T>(string s, T defaultValue) where T : struct, Enum
        {
            if (Enum.TryParse(s.Replace("-", ""), true, out T res))
                return res;
            var v = JsonConvert.DeserializeObject<T>(s);
            //if (v != null)
            return v;
            return defaultValue;
        }

        public static G3GazeData SignalToGaze(List<JToken> bodyValues)
        {
            var ts = (float)bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseGazeData(data, ts);
            return gaze;
        }
        public static G3Event SignalToEvent(List<JToken> bodyValues)
        {
            var ts = (float)bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseEvent(data, ts);
            return gaze;
        }

        public static G3ImuData SignalToIMU(List<JToken> bodyValues)
        {
            var ts = (float)bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var gaze = ParseImuData(data, ts);
            return gaze;
        }

        public static G3SyncPortData SignalToSyncPort(List<JToken> bodyValues)
        {
            var ts = (float)bodyValues[0].Value<double>();
            var data = bodyValues[1] as JObject;
            var sync = ParseSyncPortData(data, ts);
            return sync;
        }

        public static bool SignalToBool(List<JToken> arg)
        {
            return arg[0].Value<bool>();
        }
        public static string SignalToString(List<JToken> arg)
        {
            return arg[0].Value<string>();
        }


        public static Ipv6Method Ipv6MethodParser(string arg)
        {
            return ParseEnum(arg, Ipv6Method.unknown);
        }
        public static Ipv4Method Ipv4MethodParser(string arg)
        {
            return ParseEnum(arg, Ipv4Method.unknown);
        }

        public static GazeFrequency ConvertGazeFrequencyFromString(string s)
        {
            if (s == "100hz")
                return GazeFrequency.Freq100hz;
            if (s == "50hz")
                return GazeFrequency.Freq50hz;
            return GazeFrequency.Default;
        }
    }
}