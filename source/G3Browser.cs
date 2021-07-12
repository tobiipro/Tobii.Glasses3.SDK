using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Zeroconf;

namespace G3SDK
{
    public class G3Browser
    {
        public async Task<List<G3Api>> ProbeForDevices()
        {
            var results = await ScanZeroConf();
            var res = new List<G3Api>();
            foreach (var host in results)
            {
                if (host.IPAddress != null)
                {
                    res.Add(new G3Api(host.IPAddress));
                }
            }

            return res;
        }

        public async Task<IReadOnlyList<IZeroconfHost>> ScanZeroConf()
        {
            return await ZeroconfResolver.ResolveAsync("_tobii-g3api._tcp.local.");
        }

        public async Task<List<G3Api>> ForceProbe(int timeout = 100, int maxParallel = 50)
        {
            var res = new List<G3Api>();
            var strHostName = Dns.GetHostName();

            // Find host by name
            var ipHostEntry = await Dns.GetHostEntryAsync(strHostName);

            // Enumerate IP addresses
            var addresses = Enumerable.Empty<IPAddress>();
            foreach (var ipAddress in ipHostEntry.AddressList.Where( i=>i.AddressFamily == AddressFamily.InterNetwork))
            {
                addresses = addresses.Concat(GetRange(ipAddress));
            }
            res.AddRange(await ForceProbe(addresses, timeout, maxParallel));

            return res;
        }

        private IEnumerable<IPAddress> GetRange(IPAddress ip, byte start=0, byte end=255)
        {
            for (int i = start; i <= end; i++)
            {
                var temp = ip.GetAddressBytes();
                temp[3] = (byte)i;
                yield return new IPAddress(temp);
            } 
        }

        /// <summary>
        /// Checks all addresses on the subnet of the supplied IP address, does not depend on ZeroConf.
        /// Use with caution!
        /// </summary>
        /// <param name="subNet">IP address in the subnet you want to search for G3 units</param>
        /// <param name="timeout">max timeout to wait for ping</param>
        /// <param name="maxParallel">Max parallel probes to run at a time.</param>
        /// <returns>A list of api-objects for the glasses that was found</returns>
        public async Task<List<G3Api>> ForceProbe(IPAddress subNet, int timeout = 100, int maxParallel = 50)
        {
            var addresses = GetRange(subNet).ToList();
            return await ForceProbe(addresses, timeout, maxParallel);
        }

        public async Task<List<G3Api>> ForceProbe(IEnumerable<IPAddress> addresses, int timeout=100, int maxParallel=50)
        {
            var res = new ConcurrentBag<G3Api>();
            var throttler = new SemaphoreSlim(initialCount: maxParallel);
            var tasks = addresses.Select(async ip =>
            {
                try
                {
                    await throttler.WaitAsync();
                    var ping = new Ping();
                    var pingReply = await ping.SendPingAsync(ip, timeout);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        try
                        {
                            var api = new G3Api(ip.ToString(), false);
                            var serial = api.System.RecordingUnitSerial.Result;
                            if (!string.IsNullOrEmpty(serial))
                            {
                                res.Add(api);
                            }
                            await api.Disconnect();
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                finally
                {
                    throttler.Release();
                }
            });
            await Task.WhenAll(tasks);

            return res.ToList();
        }
    }
}