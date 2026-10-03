using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace VideoForensics.Utils.LoggerViewer
{
    /// <summary>
    /// ViewModel for the Logger Viewer application.
    /// </summary>
    public class LogViewerViewModel : INotifyPropertyChanged
    {
        private readonly NamedPipeLogReader _logReader;
        private readonly ObservableCollection<LogEntry> _displayedEntries;
        private string _selectedLogLevel = "All";
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private bool _autoScroll = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<LogEntry> DisplayedEntries => _displayedEntries;

        public string SelectedLogLevel
        {
            get => _selectedLogLevel;
            set
            {
                if (_selectedLogLevel != value)
                {
                    _selectedLogLevel = value;
                    OnPropertyChanged(nameof(SelectedLogLevel));
                    RefreshFilteredEntries();
                }
            }
        }

        public ConnectionState ConnectionState
        {
            get => _connectionState;
            set
            {
                if (_connectionState != value)
                {
                    _connectionState = value;
                    OnPropertyChanged(nameof(ConnectionState));
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusColor));
                }
            }
        }

        public string ConnectionStatusText => ConnectionState switch
        {
            ConnectionState.Connected => "Connected",
            ConnectionState.Disconnected => "Disconnected",
            ConnectionState.Reconnecting => "Reconnecting...",
            _ => "Unknown"
        };

        public string ConnectionStatusColor => ConnectionState switch
        {
            ConnectionState.Connected => "#00AA00",
            ConnectionState.Disconnected => "#FF0000",
            ConnectionState.Reconnecting => "#FFAA00",
            _ => "#AAAAAA"
        };

        public bool AutoScroll
        {
            get => _autoScroll;
            set
            {
                if (_autoScroll != value)
                {
                    _autoScroll = value;
                    OnPropertyChanged(nameof(AutoScroll));
                }
            }
        }

        public int EntryCount => _displayedEntries.Count;

        public ICommand ClearCommand { get; }
        public ICommand SaveCommand { get; }

        public LogViewerViewModel()
        {
            _displayedEntries = new ObservableCollection<LogEntry>();
            _logReader = new NamedPipeLogReader();

            ClearCommand = new RelayCommand(_ => Clear());
            SaveCommand = new RelayCommand(_ => Save());

            _logReader.EntryReceived += OnLogEntryReceived;
            _logReader.ConnectionStateChanged += OnConnectionStateChanged;
        }

        public async void Start()
        {
            await _logReader.StartAsync();
        }

        private void OnLogEntryReceived(LogEntry entry)
        {
            // Add to display if it matches the current filter
            if (MatchesFilter(entry))
            {
                _displayedEntries.Add(entry);
                OnPropertyChanged(nameof(EntryCount));
            }
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            ConnectionState = state;
        }

        private bool MatchesFilter(LogEntry entry)
        {
            if (_selectedLogLevel == "All")
            {
                return true;
            }

            return entry.Level.Equals(_selectedLogLevel, StringComparison.OrdinalIgnoreCase) ||
                   (entry.Level == "Information" && _selectedLogLevel == "Info");
        }

        private void RefreshFilteredEntries()
        {
            var allEntries = _logReader.GetBufferedEntries();
            _displayedEntries.Clear();

            foreach (var entry in allEntries)
            {
                if (MatchesFilter(entry))
                {
                    _displayedEntries.Add(entry);
                }
            }

            OnPropertyChanged(nameof(EntryCount));
        }

        private void Clear()
        {
            _displayedEntries.Clear();
            OnPropertyChanged(nameof(EntryCount));
        }

        private void Save()
        {
            if (_displayedEntries.Count == 0)
            {
                System.Windows.MessageBox.Show("No entries to save.", "Information");
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".csv",
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = $"logs-{DateTime.Now:yyyy-MM-dd-HHmmss}.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(dialog.FileName))
                    {
                        // Write header
                        writer.WriteLine("Timestamp,Level,Message");

                        // Write entries
                        foreach (var entry in _displayedEntries)
                        {
                            var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                            var message = EscapeCsv(entry.Message);
                            writer.WriteLine($"\"{timestamp}\",\"{entry.Level}\",\"{message}\"");
                        }
                    }

                    System.Windows.MessageBox.Show($"Logs saved to {dialog.FileName}", "Success");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error saving logs: {ex.Message}", "Error");
                }
            }
        }

        private static string EscapeCsv(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return input.Replace("\"", "\"\"");
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple relay command for MVVM.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }
}
