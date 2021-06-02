using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using G3SDK;
using LSL;

namespace G3LSLConnector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly G3Browser _browser;
        private List<G3Api> _devices;
        private List<G3LSL> _connectors;

        public MainWindow()
        {
            InitializeComponent();
            _browser = new G3Browser();
        }

        private async void StartLSL(object sender, RoutedEventArgs e)
        {
            startBtn.IsEnabled = false;
            _devices = await _browser.ProbeForDevices();
            _connectors = new List<G3LSL>();
            foreach (var api in _devices)
            {
                var connector = new G3LSL(api);
                await connector.Init();
                _connectors.Add(connector);
            }

            stopBtn.IsEnabled = true;
            inspectBtn.IsEnabled = true;
        }

        private void StopLSL(object sender, RoutedEventArgs e)
        {
            stopBtn.IsEnabled = false;
            inspectBtn.IsEnabled = false;
            foreach (var connector in _connectors)
            {
                connector.Close();
            }

            _connectors = null;

            startBtn.IsEnabled = true;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            log.Text = InspectStream();
        }

        public string InspectStream()
        {
            var results = LSL.LSL.resolve_stream("name", G3LSL.GazeStreamName, timeout: 5);
            if (!results.Any())
            {
                return "No inlet found";
            }

            var sb = new StringBuilder();

            foreach (var r in results)
            {
                using var inlet = new StreamInlet(r);

                // get the full stream info (including custom meta-data) and dissect it
                using var inf = inlet.info();
                sb.AppendLine("The stream's XML meta-data is: ");
                sb.AppendLine(inf.as_xml());
                sb.AppendLine("The manufacturer is: " + inf.desc().child_value("manufacturer"));
                sb.AppendLine("The channel labels are as follows:");
                var ch = inf.desc().child("channels").child("channel");
                for (int k = 0; k < inf.channel_count(); k++)
                {
                    sb.AppendLine("* " + ch.child_value("label"));
                    ch = ch.next_sibling();
                }
            }

            results.DisposeArray();
            return sb.ToString();
        }
    }
}
