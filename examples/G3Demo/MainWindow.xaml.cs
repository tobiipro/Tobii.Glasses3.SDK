using System;

namespace G3Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly MainVm _vm;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm = new MainVm(Dispatcher);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _vm.Close();
        }
    }
}
