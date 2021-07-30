using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public abstract class DynamicChildNode : G3Object, IDynamicChildNode
    {
        public DynamicChildNode(G3Api g3Api, string path) : base(g3Api, path)
        {
            ChildAdded = AddSignal("child-added", list => list[0].Value<string>());
            ChildRemoved = AddSignal("child-removed", list => list[0].Value<string>());
        }

        protected async Task<string[]> GetChildren()
        {
            var s = await G3Api.GetRestRequest(Path);
            var msg = JsonConvert.DeserializeObject<Recordings.ApiObjectDescription>(s);
            return msg.children;
        }

        public IG3Observable<string> ChildRemoved { get; }

        public IG3Observable<string> ChildAdded { get; }
    }

    public interface IDynamicChildNode
    {
        IG3Observable<string> ChildRemoved { get; }
        IG3Observable<string> ChildAdded { get; }
    }
}