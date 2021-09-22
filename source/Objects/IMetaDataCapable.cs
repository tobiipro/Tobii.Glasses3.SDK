using System;
using System.Text;
using System.Threading.Tasks;

namespace G3SDK
{
    public interface IMetaDataCapable : IG3Object
    {
        Task<bool> MetaInsert(string key, string data);
        Task<bool> MetaInsert(string key, byte[] data);
        Task<string> MetaLookupString(string key);
        Task<byte[]> MetaLookup(string key);
        Task<string[]> MetaKeys();
    }

    public static class MetaDataCapableHelpers
    {
        public static async Task<bool> MetaInsert(G3Api g3Api, string path, string key, string data)
        {
            return await MetaInsert(g3Api, path, key, Encoding.UTF8.GetBytes(data));
        }

        public static async Task<bool> MetaInsert(G3Api g3Api, string path, string key, byte[] data)
        {
            var b64 = Convert.ToBase64String(data);
            if (b64 == "")
                b64 = null;
            return await g3Api.ExecuteCommandBool(path, "meta-insert", LogLevel.info, key, b64);
        }

        public static async Task<string> MetaLookupString(G3Api g3Api, string path, string key)
        {
            var bytes = await MetaLookup(g3Api, path, key);
            return Encoding.UTF8.GetString(bytes);
        }

        public static async Task<byte[]> MetaLookup(G3Api g3Api, string path, string key)
        {
            var b64 = await g3Api.ExecuteCommand<string>(path, "meta-lookup", LogLevel.info, key);
            if (b64 == null)
                return new byte[0];

            return Convert.FromBase64String(b64);
        }

        public static async Task<string[]> MetaKeys(G3Api g3Api, string path)
        {
            var keys = await g3Api.ExecuteCommand<string[]>(path, "meta-keys", LogLevel.info);
            return keys;
        }
    }

}