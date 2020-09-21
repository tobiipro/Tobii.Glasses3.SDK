using System.Collections.Generic;
using System.Threading.Tasks;
using Zeroconf;

namespace G3SDK
{
    public class G3Browser
    {
        public async Task<List<G3Api>> ProbeForDevices()
        {
            var results = await ZeroconfResolver.ResolveAsync("_tobii-g3api._tcp.local.");
            var res = new List<G3Api>();
            foreach (var host in results)
            {
                var address = host.DisplayName + ".local";
                address = host.IPAddress;
                res.Add(new G3Api(address));
            }

            return res;
        }
    }
}