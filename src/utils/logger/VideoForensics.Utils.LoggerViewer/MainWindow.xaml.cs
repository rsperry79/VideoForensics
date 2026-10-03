using System.Windows;

namespace VideoForensics.Utils.LoggerViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LogViewerViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new LogViewerViewModel();
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_viewModel != null)
            {
                var reader = typeof(LogViewerViewModel).GetField("_logReader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_viewModel);
                if (reader is NamedPipeLogReader logReader)
                {
                    logReader.StopAsync().Wait();
                    logReader.Dispose();
                }
            }
        }
    }
}
