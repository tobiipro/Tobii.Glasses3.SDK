using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using G3SDK;
using Newtonsoft.Json.Serialization;

namespace G3DocumentExtractor
{
    class Program
    {
        static void Main()
        {
            var browser = new G3Browser();
            List<G3Api> devices;
            do
            {
                Console.WriteLine("Looking for devices");
                devices = browser.ProbeForDevices().Result;
                if (!devices.Any())
                    Thread.Sleep(1000);
            } while (devices.Count == 0);

            foreach (var d in devices)
            {
                ExtractDoc(d).Wait();
            }
        }

        private static async Task ExtractDoc(G3Api g3Api)
        {
            var root = new G3Object(g3Api, "");
            var objects = new Dictionary<string, JObject>
            {
                ["/"] = await root.GetDocJson()
            };
            if (!(await g3Api.WebRTC.Children()).Any())
            {
                Console.WriteLine("Creating webrtc session");
                await g3Api.WebRTC.Create();
            }

            if (!(await g3Api.Recordings.Children()).Any() && (await g3Api.System.Storage.CardState) ==CardState.Available)
            {
                Console.WriteLine("Creating empty recording");
                var res = await g3Api.Recorder.Start();
                Thread.Sleep(2000);
                await g3Api.Recorder.Stop();
            }

            await RecurseChildren(root, objects);

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new OrderedContractResolver(),
            };

            var single = new Dictionary<string, bool>
            {
                {"/webrtc/", false}, 
                {"/recordings/", false}
            };

            var doc = new JObject();
            foreach (var p in objects.Keys.OrderBy(s => s))
            {
                var skip = false;
                foreach (var k in single.Keys.ToArray())
                {
                    if (p.StartsWith(k))
                    {
                        skip = single[k];
                        single[k] = true;
                    }
                }
                // replace known values/types with human readable names
                var objectName = Regex.Replace(p, @"[0-9a-f]{8}[-]?(?:[0-9a-f]{4}[-]?){3}[0-9a-f]{12}", "<UUID>");
                objectName = Regex.Replace(objectName, @"TG03B-[0-9]{12}", "<SerialNumber>");
                Console.WriteLine(objectName + (skip?" skip":""));

                if (!skip)
                {
                    doc[objectName] = objects[p];
                }
            }
            
            var json = JsonConvert.SerializeObject(doc, Formatting.Indented, settings);

            // replace known numeric constants with human readable names
            json = json.Replace("1.7976931348623157E+308", "\"Double.Max\"");
            json = json.Replace("9223372036854776000", "\"Int64.Max\"");
            

            var fileName = (await g3Api.System.Version).Replace('+', '-') + ".json";
            File.WriteAllText(fileName, json);
            Console.WriteLine("Done... Press Enter");
            Console.ReadLine();
        }

        private static async Task RecurseChildren(G3Object obj, Dictionary<string, JObject> objects)
        {
            foreach (var child in await obj.GetApiChildren())
            {
                objects[child.Path] = await child.GetDocJson();
                await RecurseChildren(child, objects);
            }
        }
    }

    public class OrderedContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            return base.CreateProperties(type, memberSerialization).OrderBy(p => p.PropertyName, StringComparer.Ordinal).ToList();
        }
    }
}
