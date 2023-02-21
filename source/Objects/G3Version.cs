using System.Collections.Generic;
using System.IO;

namespace G3SDK
{
    public class G3Version
    {
        private readonly List<int> _versionParts;
        private readonly string _versionString;
        
        public G3Version(string versionString)
        {
            _versionString = versionString;
            _versionParts = new List<int>();
            foreach (var p in versionString.Split(new[] { '.', '+' }))
            {
                if (int.TryParse(p, out var pValue))
                    _versionParts.Add(pValue);
            }
        }

        public bool LessThan(G3Version other)
        {
            return !GreaterOrEqualTo(other);
        }

        public bool GreaterOrEqualTo(G3Version other)
        {
            for (var i = 0; i < other._versionParts.Count; i++)
            {
                if (_versionParts[i] < other._versionParts[i])
                    return false;

                if (_versionParts[i] > other._versionParts[i])
                    return true;
            }

            return true;
        }

        public override string ToString()
        {
            return string.Join(".", _versionParts);
        }

        public static G3Version ReadFromFile(string fileName)
        {
            if (!File.Exists(fileName))
                return Unknown;
            var v = File.ReadAllText(fileName);
            return new G3Version(v);
        }

        public static G3Version Latest => Version_1_32;

        public static G3Version Version_1_32 { get; } = new G3Version("1.32");
        public static G3Version Version_1_29_Sarek { get; } = new G3Version("1.29+sarek");
        public static G3Version Version_1_28_Granskott { get; } = new G3Version("1.28+granskott");
        public static G3Version Version_1_26_cremla { get; } = new G3Version("1.26+cremla");
        public static G3Version Version_1_23_pumpa { get; } = new G3Version("1.23+pumpa");
        public static G3Version Version_1_21_talgoxe { get; } = new G3Version("1.21+talgoxe");
        public static G3Version Version_1_20_Crayfish { get; } = new G3Version("1.20+crayfish");
        public static G3Version Version_1_14_Nudelsoppa { get; } = new G3Version("1.14+nudelsoppa");
        public static G3Version Version_1_11_Flytt { get; } = new G3Version("1.11+flytt");
        public static G3Version Version_1_7_SommarRegn { get; } = new G3Version("1.7+sommarregn");
        public static G3Version Unknown { get; } = new G3Version("0.0");
    }
}