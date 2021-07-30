using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace G3SDK
{
    public class Recordings: DynamicChildNode, IRecordings
    {

        public Recordings(G3Api g3Api): base(g3Api, "recordings")
        {
            ScanStart = AddSignal("scan-start", list => new Notification());
            ScanDone = AddSignal("scan-done", list => new Notification());
            Deleted = AddSignal("deleted", ConvertGuid);
        }

        private Guid ConvertGuid(List<JToken> list)
        {
            var guidStr = list[0].Value<string>();
            return Guid.Parse(guidStr);
        }

        public async Task<bool> Delete(Guid uuid)
        {
            return await G3Api.ExecuteCommandBool(Path, "delete", LogLevel.info, uuid.ToString());
        }

        public IG3Observable<Guid> Deleted { get; set; }

        public IG3Observable<Notification> ScanStart { get; }
        public IG3Observable<Notification> ScanDone { get; }

        public async Task<List<IRecording>> Children()
        {
            return new List<IRecording>(await InternalChildren());
        }

        private async Task<List<Recording>> InternalChildren()
        {
            // {
            // "id":6,
            // "body":{
            //      "properties":{
            //          "name":{"type":"string","range":[],"mode":"r","help":"The name of the object"}
            //      },
            //      "actions":{
            //          "delete":{"return":"boolean","args":["string"],"help":"Delete recording by UUID.\nThis only marks the recording for deletion and removes it from the list of recordings, the recording is deleted when the last viewer has closed the connection to the recording (see deleted signal).\nArgument1: The uuid to delete.\nReturn: True if recording was marked for deletion, otherwise False"}
            //      },
            //      "signals":{
            //          "scan-done":{"args":[],"help":"Emitted at the end of a complete scan of the SdCard.\nSee scan-start"},
            //          "scan-start":{"args":[],"help":"Emitted at start of a complete scan of the SdCard.\nThis usually happens after the card has been inserted or during startup."},
            //          "deleted":{"args":["string"],"help":"Emitted when a recording has been deleted.\nRecordings are deleted when it has been marked for deletion by the delete action and the last viewer has closed the recording.\nArgument1: The uuid of the recording that was deleted"},
            //          "child-added":{"args":["string"],"help":"Signal is emitted when a new object has been added."},
            //          "child-removed":{"args":["string"], "help":"Signal is emitted when a object was removed."}
            //      },
            //      "children":["36c77c16-eb3f-4ab3-8501-0cfcbb51ebd9","6a28de01-5621-48b2-8fd9-109b4bcfb6ef"]
            // }
            // }
            var childIds = await GetChildren();
            var recordings = new List<Recording>();
            foreach (var child in childIds)
            {
                var uuid = Guid.Parse(child);
                recordings.Add(new Recording(G3Api, Path, uuid));
            }
            return recordings;
        }

        public override async Task<IEnumerable<G3Object>> GetSDKChildren()
        {
            return await InternalChildren();
        }

        public struct RecordingsResponse
        {
            public int? id { get; set; }
            public ApiObjectDescription body { get; set; }

        }
        public class ApiObjectDescription
        {
            public string[] children { get; set; }
        }
    }

    public interface IRecordings: IDynamicChildNode
    {
        Task<bool> Delete(Guid uuid);
        IG3Observable<Guid> Deleted { get; }
        IG3Observable<Notification> ScanStart { get; }
        IG3Observable<Notification> ScanDone { get; }
        Task<List<IRecording>> Children();
    }
}