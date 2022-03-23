using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public interface IHwTests : IG3Object
    {
        Task<bool> Running { get; }
        Task<List<HwTestResult>> Result { get; }
        Task<bool> Run();
        IG3Observable<int> Done { get; }
    }

    public class HwTests : G3Object, IHwTests
    {
        private readonly ROProperty<bool> _running;
        private readonly ROProperty<List<HwTestResult>> _result;

        public HwTests(G3Api api, string parentUrl) : base(api, $"{parentUrl}/hwtests")
        {
            _running = AddROProperty("running", bool.Parse);
            _result = AddROProperty("result", s=>ParseResult(s));
            Done = AddSignal("done", ConvertDone);

        }

        public Task<bool> Running => _running.Value();
        public Task<List<HwTestResult>> Result => _result.Value();

        private List<HwTestResult> ParseResult(string s)
        {
            var l = JsonConvert.DeserializeObject(s);
            var o = l as JObject;
            var res = new List<HwTestResult>();
            foreach (var k in o.Properties())
            {
                var testObj = k.Value as JObject;
                var pass = testObj["result"].Value<bool>();
                var name = testObj["test"].Value<string>();
                var data = testObj["data"].ToString();
                res.Add(new HwTestResult(k.Name, pass, name, data));
            }

            return res;
        }
        public Task<bool> Run()
        {
            return G3Api.ExecuteCommandBool(Path, "run", LogLevel.warning);
        }

        private int ConvertDone(List<JToken> arg)
        {
            return arg[0].Value<int>();
        }
        
        public IG3Observable<int> Done { get; }
    }

    public class HwTestResult
    {
        public HwTestResult(string id, bool pass, string name, string data)
        {
            Id = id;
            Pass = pass;
            Name = name;
            Data = data;
        }

        public string Name { get; }
        public string Id { get; }
        public bool Pass { get; }
        public string Data { get; }
    }
}