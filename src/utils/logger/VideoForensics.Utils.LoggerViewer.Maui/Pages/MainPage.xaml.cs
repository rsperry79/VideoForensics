using Microsoft.Maui.Controls;
using VideoForensics.Utils.LoggerViewer.Maui.Services;
using VideoForensics.Utils.LoggerViewer.Maui.ViewModels;

namespace VideoForensics.Utils.LoggerViewer.Maui.Pages;

public partial class MainPage : ContentPage
{
    private readonly LogReaderService _logReaderService;

    public MainPage(LogViewerViewModel viewModel, LogReaderService logReaderService)
    {
        InitializeComponent();
        _logReaderService = logReaderService;
        BindingContext = viewModel;

        // Set initial picker selection
        LogLevelPicker.SelectedIndex = 0;
    }

    private void OnLogLevelChanged(object? sender, EventArgs e)
    {
        if (LogLevelPicker.SelectedItem is string selectedLevel && BindingContext is LogViewerViewModel viewModel)
        {
            viewModel.SelectedLogLevel = selectedLevel;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Auto-connect on app start
        await _logReaderService.StartAsync();
        ConnectButton.Text = "Disconnect";
        if (BindingContext is LogViewerViewModel viewModel)
        {
            ConnectButton.Command = viewModel.DisconnectCommand;
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        await _logReaderService.StopAsync();
    }
}
