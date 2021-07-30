using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using G3SDK;

namespace G3Demo
{
    public class RecordingsVM : ViewModelBase
    {
        private readonly IG3Api _g3;
        private bool _scanning;
        private RecordingVM _selectedRecording;
        public ObservableCollection<RecordingVM> Recordings { get; } = new ObservableCollection<RecordingVM>();

        public RecordingVM SelectedRecording
        {
            get => _selectedRecording;
            set
            {
                if (Equals(value, _selectedRecording)) return;
                _selectedRecording = value;
                OnPropertyChanged();
            }
        }

        public RecordingsVM(Dispatcher dispatcher, IG3Api g3) : base(dispatcher)
        {
            _g3 = g3;
            _g3.Recordings.ScanStart.SubscribeAsync(n => _scanning = true);
            _g3.Recordings.ScanDone.SubscribeAsync(async n =>
            {
                _scanning = false;
                await SyncRecordings();
            });
            _g3.Recordings.ChildAdded.SubscribeAsync(async s =>
            {
                if (!_scanning)
                    await SyncRecordings();
            });
            _g3.Recordings.ChildRemoved.SubscribeAsync(async s =>
            {
                if (!_scanning)
                    await SyncRecordings();
            });

            FireAndCatch(SyncRecordings());

        }

        private void FireAndCatch(Task task)
        {
            task.ContinueWith(t =>
            {
                Dispatcher.Invoke(() => throw t.Exception);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task SyncRecordings()
        {
            var deviceRecordings = await _g3.Recordings.Children();
            foreach (var r in deviceRecordings)
            {
                if (Recordings.All(rec => rec.Id != r.UUID))
                {
                    var recordingVm = await RecordingVM.Create(Dispatcher, r, _g3);
                    Dispatcher.Invoke(() =>
                    {
                        var inserted = false;
                        // insert recording in reverse creation-date (newest on top)
                        for (var i = 0; i < Recordings.Count; i++)
                            if (Recordings[i].Created < recordingVm.Created)
                            {
                                Recordings.Insert(i, recordingVm);
                                inserted = true;
                                break;
                            }
                        if (!inserted)
                            Recordings.Add(recordingVm);
                    });
                }
            }

            foreach (var rec in Recordings.ToArray())
            {
                if (deviceRecordings.All(r => r.UUID != rec.Id))
                    Dispatcher.Invoke(() => Recordings.Remove(rec));
            }
        }
    }

    internal class TimeStampComparer : IComparer<G3GazeData>
    {
        public int Compare(G3GazeData x, G3GazeData y)
        {
            return x.TimeStamp.CompareTo(y.TimeStamp);
        }
    }
}