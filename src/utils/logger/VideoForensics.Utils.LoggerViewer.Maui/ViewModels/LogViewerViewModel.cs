using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Microsoft.Maui;
using Microsoft.Maui.Controls;
using VideoForensics.Utils.LoggerViewer.Maui.Models;
using VideoForensics.Utils.LoggerViewer.Maui.Services;

namespace VideoForensics.Utils.LoggerViewer.Maui.ViewModels
{
    /// <summary>
    /// ViewModel for the Logger Viewer UI.
    /// Manages log entries display, filtering, and connection state.
    /// </summary>
    public class LogViewerViewModel : INotifyPropertyChanged
    {
        private readonly LogReaderService _logReaderService;
        private readonly ObservableCollection<LogEntry> _allEntries;
        private ObservableCollection<LogEntry> _filteredEntries;
        private string _selectedLogLevel = "All";
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private bool _autoScroll = true;
        private string _connectionStatus = "Disconnected";

        public event PropertyChangedEventHandler? PropertyChanged;

        public ICommand ClearCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }

        public ObservableCollection<LogEntry> FilteredEntries
        {
            get => _filteredEntries;
            set
            {
                if (_filteredEntries != value)
                {
                    _filteredEntries = value;
                    OnPropertyChanged(nameof(FilteredEntries));
                }
            }
        }

        public string SelectedLogLevel
        {
            get => _selectedLogLevel;
            set
            {
                if (_selectedLogLevel != value)
                {
                    _selectedLogLevel = value;
                    OnPropertyChanged(nameof(SelectedLogLevel));
                    ApplyFilter();
                }
            }
        }

        public ConnectionState CurrentConnectionState
        {
            get => _connectionState;
            set
            {
                if (_connectionState != value)
                {
                    _connectionState = value;
                    OnPropertyChanged(nameof(CurrentConnectionState));
                    UpdateConnectionStatus();
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged(nameof(ConnectionStatus));
                }
            }
        }

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

        public int EntryCount => _allEntries.Count;

        public LogViewerViewModel(LogReaderService logReaderService)
        {
            _logReaderService = logReaderService ?? throw new ArgumentNullException(nameof(logReaderService));
            _allEntries = new();
            _filteredEntries = new();

            ClearCommand = new RelayCommand(OnClear);
            SaveCommand = new RelayCommand(OnSave);
            ConnectCommand = new RelayCommand(OnConnect);
            DisconnectCommand = new RelayCommand(OnDisconnect);

            // Subscribe to log reader events
            _logReaderService.EntryReceived += OnLogEntryReceived;
            _logReaderService.ConnectionStateChanged += OnConnectionStateChanged;
        }

        private void OnLogEntryReceived(LogEntry entry)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _allEntries.Add(entry);
                OnPropertyChanged(nameof(EntryCount));
                ApplyFilter();
            });
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentConnectionState = state;
            });
        }

        private void ApplyFilter()
        {
            var filtered = _selectedLogLevel == "All"
                ? _allEntries.ToList()
                : _allEntries.Where(e => e.Level == _selectedLogLevel).ToList();

            FilteredEntries.Clear();
            foreach (var entry in filtered)
            {
                FilteredEntries.Add(entry);
            }
        }

        private void OnClear()
        {
            _allEntries.Clear();
            FilteredEntries.Clear();
            OnPropertyChanged(nameof(EntryCount));
        }

        private async void OnSave()
        {
            try
            {
                var fileName = $"logs-{DateTime.Now:yyyy-MM-dd_HHmmss}.csv";
                var defaultLocation = FileSystem.Current.CacheDirectory;
                var filePath = Path.Combine(defaultLocation, fileName);

                var csv = GenerateCsv(_filteredEntries);
                await File.WriteAllTextAsync(filePath, csv);

                await Application.Current?.MainPage?.DisplayAlert("Success", $"Logs saved to {filePath}", "OK")!;
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Error", $"Failed to save logs: {ex.Message}", "OK")!;
            }
        }

        private string GenerateCsv(ObservableCollection<LogEntry> entries)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Timestamp,Level,Category,Message");
            foreach (var entry in entries)
            {
                var category = string.IsNullOrEmpty(entry.Category) ? "" : entry.Category;
                var message = entry.Message.Replace("\"", "\"\""); // Escape quotes
                sb.AppendLine($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{entry.Level}\",\"{category}\",\"{message}\"");
            }
            return sb.ToString();
        }

        private async void OnConnect()
        {
            try
            {
                OnClear();
                await _logReaderService.StartAsync();
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Error", $"Failed to connect: {ex.Message}", "OK")!;
            }
        }

        private async void OnDisconnect()
        {
            try
            {
                await _logReaderService.StopAsync();
            }
            catch (Exception ex)
            {
                await Application.Current?.MainPage?.DisplayAlert("Error", $"Failed to disconnect: {ex.Message}", "OK")!;
            }
        }

        private void UpdateConnectionStatus()
        {
            ConnectionStatus = _connectionState switch
            {
                ConnectionState.Disconnected => "● Disconnected",
                ConnectionState.Connecting => "● Connecting...",
                ConnectionState.Connected => "● Connected",
                ConnectionState.Reconnecting => "● Reconnecting...",
                _ => "● Unknown"
            };
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Simple RelayCommand implementation for MAUI.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public event EventHandler? CanExecuteChanged;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
