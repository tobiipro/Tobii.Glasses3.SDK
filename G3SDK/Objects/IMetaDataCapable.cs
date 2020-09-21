using System;
using System.Text;
using System.Threading.Tasks;

namespace G3SDK
{
    public interface IMetaDataCapable: IG3Object
    {
    }

    public static class MetaDataCapableExtensions
    {
        public static async Task<bool> MetaInsert(IMetaDataCapable root, string key, string data)
        {
            return await MetaInsert(root, key, Encoding.UTF8.GetBytes(data));
        }

        public static async Task<bool> MetaInsert(IMetaDataCapable root, string key, byte[] data)
        {
            var b64 = Convert.ToBase64String(data);
            if (b64 == "")
                b64 = null;
            return await root.G3Api.ExecuteCommandBool(root.Path, "meta-insert", LogLevel.info, key, b64);
        }

        public static async Task<string> MetaLookupString(IMetaDataCapable root, string key)
        {
            var bytes = await MetaLookup(root, key);
            return Encoding.UTF8.GetString(bytes);
        }

        public static async Task<byte[]> MetaLookup(IMetaDataCapable root, string key)
        {
            var b64 = await root.G3Api.ExecuteCommand<string>(root.Path, "meta-lookup", LogLevel.info, key);
            return Convert.FromBase64String(b64);
        }

        public static async Task<string[]> MetaKeys(IMetaDataCapable root)
        {
            var keys = await root.G3Api.ExecuteCommand<string[]>(root.Path, "meta-keys", LogLevel.info);
            return keys;
        }
    }

}