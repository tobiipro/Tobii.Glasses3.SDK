using System.Collections.Generic;

namespace G3Demo
{
    internal class WifiSettings
    {
        public string Ssid { get; }
        public string Encryption { get; set; }
        public string Pwd { get; set; }
        public bool Hidden { get; set; }

        public WifiSettings(string ssid)
        {
            Ssid = ssid;
        }
        /// <summary>
        /// Parses the string contents of a WiFi QR code according to the specs in https://github.com/zxing/zxing/wiki/Barcode-Contents#wi-fi-network-config-android-ios-11
        /// </summary>
        /// <param name="s">the contents of the QR code</param>
        /// <param name="settings">If successfully parsed, the result will be returned here</param>
        /// <returns>true if parsing was successful</returns>
        public static bool TryParseFromQR(string s, out WifiSettings settings)
        {
            var parameters = s.Split(new[] { ':' }, 2);
            if (parameters.Length != 2 || parameters[0] != "WIFI")
            {
                settings = null;
                return false;
            }

            var parts = parameters[1].Replace("\\;", "\\@").Split(';');
            var x = new Dictionary<string, string>();
            foreach (var part in parts)
            {
                var keyValue = part.Split(new[] { ':' }, 2);
                if (keyValue.Length == 2)
                    x[keyValue[0]] = keyValue[1]
                        .Replace("\\:", ":")
                        .Replace("\\@", ";")
                        .Replace("\\\\", "\\")
                        .Replace("\\\"", "\"")
                        .Replace("\\,", ",");
            }

            if (!x.ContainsKey("S"))
            {
                // no SSID
                settings = null;
                return false;
            }

            settings = new WifiSettings(x["S"]);
            if (x.TryGetValue("T", out var encryption))
                settings.Encryption = encryption;
            if (x.TryGetValue("P", out var pwd))
                settings.Pwd = pwd;
            if (x.TryGetValue("E", out var eapMethod))
                settings.EapMethod = eapMethod;
            if (x.TryGetValue("A", out var eapAnon))
                settings.EapAnonymousIdentity = eapAnon;   
            if (x.TryGetValue("I", out var eapIdentity))
                settings.EapIdentity = eapIdentity;      
            if (x.TryGetValue("PH2", out var eapPhase2Method))
                settings.EapPhase2Method = eapPhase2Method;
            if (x.TryGetValue("H", out var hidden))
            {
                if (bool.TryParse(hidden, out var hidden2))
                    settings.Hidden = hidden2;
                else
                    settings.EapPhase2Method = hidden;
            }


            return true;

        }

        public string EapPhase2Method { get; set; }

        public string EapIdentity { get; set; }

        public string EapAnonymousIdentity { get; set; }

        public string EapMethod { get; set; }
    }
}