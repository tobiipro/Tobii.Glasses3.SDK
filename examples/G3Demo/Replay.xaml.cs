using System.Windows;
using System.Windows.Controls;

namespace G3Demo
{
    /// <summary>
    /// Interaction logic for Replay.xaml
    /// </summary>
    public partial class Replay
    {
        public Replay()
        {
            InitializeComponent();
        }

        private async void Replay_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is RecordingVM _vm)
                await _vm.AttachMediaPlayer(Media, RtaVideo);
        }
    }
}
