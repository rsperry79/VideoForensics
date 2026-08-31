using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Core.Utilities;

namespace VideoForensics
{
    public class MenuManager : IMenuManager
    {
        private readonly ILogger<MenuManager> _logger;
        private readonly IForensicsConfiguration _forensicsConfig;
        private readonly IForensicsConfigurationService _configService;
        private readonly IVideoDownloadService _downloadService;
        private readonly IForensicReportRenderer _reportRenderer;
        private readonly IProviderAuthService _authService;
        private readonly IDeviceDiscoveryService _deviceService;
        private readonly IEventAndConfigService _eventAndConfigService;
        private readonly IVideoForensicsDataClient _videoForensicsDataClient;
        private readonly IEventRepository _eventRepository;
        private readonly IDeviceConfigRepository _deviceConfigRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly IEvidenceValidationService _evidenceValidationService;
        private readonly IEvidenceExportService _evidenceExportService;
        private readonly IAppSettingRepository _appSettingRepository;
        private readonly IProviderAccountRepository _providerAccountRepository;
        private readonly IUserRepository _userRepository;
        private readonly ConfigToolsOrchestrator _configToolsOrchestrator;
        private readonly JammingToolsOrchestrator _jammingToolsOrchestrator;

        public MenuManager(
            ILogger<MenuManager> logger,
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            IVideoDownloadService downloadService,
            IForensicReportRenderer reportRenderer,
            IProviderAuthService authService,
            IDeviceDiscoveryService deviceService,
            IEventAndConfigService eventAndConfigService,
            IVideoForensicsDataClient videoForensicsDataClient,
            IEventRepository eventRepository,
            IDeviceConfigRepository deviceConfigRepository,
            IDeviceRepository deviceRepository,
            IMediaItemRepository mediaItemRepository,
            IEvidenceValidationService evidenceValidationService,
            IEvidenceExportService evidenceExportService,
            IAppSettingRepository appSettingRepository,
            IProviderAccountRepository providerAccountRepository,
            IUserRepository userRepository,
            ConfigToolsOrchestrator configToolsOrchestrator,
            JammingToolsOrchestrator jammingToolsOrchestrator)
        {
            _logger = logger;
            _forensicsConfig = config;
            _configService = configService;
            _downloadService = downloadService;
            _reportRenderer = reportRenderer;
            _authService = authService;
            _deviceService = deviceService;
            _eventAndConfigService = eventAndConfigService;
            _videoForensicsDataClient = videoForensicsDataClient;
            _eventRepository = eventRepository;
            _deviceConfigRepository = deviceConfigRepository;
            _deviceRepository = deviceRepository;
            _mediaItemRepository = mediaItemRepository;
            _evidenceValidationService = evidenceValidationService;
            _evidenceExportService = evidenceExportService;
            _appSettingRepository = appSettingRepository;
            _providerAccountRepository = providerAccountRepository;
            _userRepository = userRepository;
            _configToolsOrchestrator = configToolsOrchestrator;
            _jammingToolsOrchestrator = jammingToolsOrchestrator;
        }

        /// <summary>Formats an account for display as its user's email/display name, falling back to the provider name if the user record is missing.</summary>
        private async Task<string> FormatAccountLabelAsync(ProviderAccount account, CancellationToken ct)
        {
            var user = await _userRepository.GetAsync(account.UserId, ct);
            var identity = user?.Email ?? user?.DisplayName;
            return string.IsNullOrEmpty(identity) ? account.ProviderName : identity;
        }

        public async Task ShowMainMenuAsync()
        {
            AnsiConsole.MarkupLine("[bold green]═══════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("[bold green]    VideoForensics - Evidence Analysis[/]");
            AnsiConsole.MarkupLine("[bold green]  DV Victim Protection & Tamper Detection[/]");
            AnsiConsole.MarkupLine("[bold green]═══════════════════════════════════════[/]");
            AnsiConsole.MarkupLine("");

            while (true)
            {
                AnsiConsole.MarkupLine("[yellow]Main Menu:[/]");
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select an option")
                        .HighlightStyle("green")
                        .AddChoices(
                            "Run Full Forensic Workflow",
                            "Collect Evidence",
                            "Analyze Evidence",
                            "Review Evidence",
                            "Browse Events",
                            "Device Configuration",
                            "Query API",
                            "Manage Accounts",
                            "Configuration",
                            "Exit"
                        ));

                switch (choice)
                {
                    case "Run Full Forensic Workflow":
                        await RunFullForensicWorkflow();
                        break;
                    case "Collect Evidence":
                        await ShowVideoDownloads();
                        break;
                    case "Analyze Evidence":
                        await ShowAnalyzeEvidenceMenu();
                        break;
                    case "Review Evidence":
                        await ShowEvidence();
                        break;
                    case "Browse Events":
                        await ShowBrowseEventsMenu(CancellationToken.None);
                        break;
                    case "Device Configuration":
                        await ShowDeviceConfigurationMenu(CancellationToken.None);
                        break;
                    case "Query API":
                        await ShowQueryApiMenu(CancellationToken.None);
                        break;
                    case "Manage Accounts":
                        await ShowManageAccountsMenu(CancellationToken.None);
                        break;
                    case "Configuration":
                        await ShowConfigurationMenu();
                        break;
                    case "Exit":
                        AnsiConsole.MarkupLine("[yellow]Exiting VideoForensics...[/]");
                        return;
                }

                AnsiConsole.MarkupLine("");
            }
        }

        private async Task ShowEvidence()
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("REVIEW & EXPORT EVIDENCE");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine();
                Console.WriteLine("1. Review Evidence");
                Console.WriteLine("2. Export Evidence");
                Console.WriteLine("3. Back to Main Menu");
                Console.WriteLine();
                Console.Write("Select an option: ");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                    case "review":
                        await _reportRenderer.ShowEvidenceAsync(CancellationToken.None);
                        break;
                    case "2":
                    case "export":
                        await ShowExportEvidenceMenu(CancellationToken.None);
                        break;
                    case "3":
                    case "back":
                        return;
                    default:
                        Console.WriteLine("Invalid selection.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private async Task ShowForensicReports()
        {
            await _reportRenderer.ShowForensicReportsAsync(CancellationToken.None);
        }

        private async Task ShowSignalAnomalies()
        {
            await _reportRenderer.ShowSignalAnomaliesAsync(CancellationToken.None);
        }

        private async Task ShowAccessControl()
        {
            await _reportRenderer.ShowAccessControlAsync(CancellationToken.None);
        }

        private async Task ShowChainOfCustody()
        {
            await _reportRenderer.ShowChainOfCustodyAsync(CancellationToken.None);
        }

        private async Task ShowExportEvidenceMenu(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("EXPORT EVIDENCE");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            Console.Write("Enter case reference (optional): ");
            var caseReference = Console.ReadLine();

            Console.Write("Enter recipient description (optional): ");
            var recipientDescription = Console.ReadLine();

            Console.Write("Enter passphrase for AES-256 encryption (optional): ");
            var passphrase = Console.ReadLine();

            Console.Write("Enter device ID to export (or press Enter for all): ");
            var deviceIdStr = Console.ReadLine();
            Guid? deviceId = null;

            if (!string.IsNullOrEmpty(deviceIdStr) && Guid.TryParse(deviceIdStr, out var parsedId))
            {
                deviceId = parsedId;
            }

            Console.Write("Enter start date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var fromDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            Console.Write("Enter end date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var toDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            // Adjust dates to include full day boundaries: start at 00:00:01, end at 23:59:59
            var adjustedFromDate = fromDate.Date.AddSeconds(1).ToUniversalTime();
            var adjustedToDate = toDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToUniversalTime();

            // Get media items for the date range and device
            IReadOnlyList<MediaItem> mediaItems;
            if (deviceId.HasValue)
            {
                mediaItems = await _mediaItemRepository.GetByDeviceAndDateRangeAsync(
                    deviceId.Value,
                    adjustedFromDate,
                    adjustedToDate,
                    ct);
            }
            else
            {
                // Get all media items and filter by date range
                var allItems = await _mediaItemRepository.ListAsync(ct);
                mediaItems = allItems
                    .Where(m => m.RecordedAtUtc >= adjustedFromDate && m.RecordedAtUtc <= adjustedToDate)
                    .ToList();
            }

            var mediaItemIds = mediaItems.Select(m => m.Id).ToList();

            if (!mediaItemIds.Any())
            {
                Console.WriteLine("No media items found for the specified criteria.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Exporting {mediaItemIds.Count} item(s)...");

            var outputDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoForensics",
                "Exports");

            var result = await _evidenceExportService.ExportEvidenceAsync(
                mediaItemIds,
                outputDir,
                caseReference,
                recipientDescription,
                passphrase,
                ct);

            Console.WriteLine("\n═══════════════════════════════════════════════════════════");
            if (result.Success)
            {
                Console.WriteLine("✓ Export completed successfully");
                Console.WriteLine($"Archive: {result.ArchivePath}");
                Console.WriteLine($"Hash: {result.ArchiveSha256Hash}");
                Console.WriteLine($"Items Included: {result.ItemsIncluded}");
                if (result.ItemsExcludedForFailedIntegrity.Any())
                {
                    Console.WriteLine($"Items Excluded (Integrity Failed): {result.ItemsExcludedForFailedIntegrity.Count}");
                }
            }
            else
            {
                Console.WriteLine("✗ Export failed");
                Console.WriteLine($"Error: {result.ErrorMessage}");
            }

            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey();
        }

        private async Task ShowValidateEvidenceMenu(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("VALIDATE EVIDENCE");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            Console.WriteLine("\nOptions:");
            Console.WriteLine("1. Check File Integrity");
            Console.WriteLine("2. Reconcile With Provider");
            Console.WriteLine("3. Back to Analysis Menu");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                case "integrity":
                    await CheckFileIntegrityAsync(ct);
                    break;
                case "2":
                case "reconcile":
                    await ReconcileWithProviderAsync(ct);
                    break;
                case "3":
                case "back":
                    return;
                default:
                    Console.WriteLine("Invalid selection.");
                    Console.ReadKey();
                    break;
            }
        }

        private async Task CheckFileIntegrityAsync(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("Check File Integrity");
            Console.WriteLine("───────────────────────────────────────────────────────────");

            Console.Write("Enter device ID (or press Enter for all): ");
            var deviceIdStr = Console.ReadLine();
            Guid? deviceId = null;

            if (!string.IsNullOrEmpty(deviceIdStr) && Guid.TryParse(deviceIdStr, out var parsedId))
            {
                deviceId = parsedId;
            }

            Console.WriteLine("Running integrity verification...");
            var results = await _evidenceValidationService.VerifyLocalIntegrityAsync(deviceId, ct);

            Console.WriteLine($"\nVerification Results: {results.Count} item(s)");
            Console.WriteLine("───────────────────────────────────────────────────────────");
            foreach (var result in results)
            {
                Console.WriteLine($"Status: {result.Status} | File: {result.FileName}");
                if (!string.IsNullOrEmpty(result.FailureReason))
                    Console.WriteLine($"  Reason: {result.FailureReason}");
            }

            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey();
        }

        private async Task ReconcileWithProviderAsync(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("Reconcile With Provider");
            Console.WriteLine("───────────────────────────────────────────────────────────");

            Console.Write("Enter device ID: ");
            if (!Guid.TryParse(Console.ReadLine(), out var deviceId))
            {
                Console.WriteLine("Invalid device ID.");
                Console.ReadKey();
                return;
            }

            // Look up the device to get the provider device ID
            var device = await _deviceRepository.GetAsync(deviceId, ct);
            if (device == null)
            {
                Console.WriteLine("Device not found.");
                Console.ReadKey();
                return;
            }

            Console.Write("Enter start date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var fromDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            Console.Write("Enter end date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var toDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            // Adjust dates to include full day boundaries: start at 00:00:01, end at 23:59:59
            var adjustedFromDate = fromDate.Date.AddSeconds(1).ToUniversalTime();
            var adjustedToDate = toDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToUniversalTime();

            Console.WriteLine("Running provider reconciliation...");
            var discrepancies = await _evidenceValidationService.ReconcileWithProviderAsync(
                deviceId, device.ProviderDeviceId, adjustedFromDate, adjustedToDate, ct);

            Console.WriteLine($"\nDiscrepancies Found: {discrepancies.Count}");
            Console.WriteLine("───────────────────────────────────────────────────────────");
            foreach (var disc in discrepancies)
            {
                Console.WriteLine($"Type: {disc.Type} | Event ID: {disc.ProviderEventId}");
                if (!string.IsNullOrEmpty(disc.FieldName))
                    Console.WriteLine($"  Field: {disc.FieldName}");
                if (!string.IsNullOrEmpty(disc.StoredValue) || !string.IsNullOrEmpty(disc.ProviderValue))
                {
                    Console.WriteLine($"  Stored: {disc.StoredValue ?? "(null)"}");
                    Console.WriteLine($"  Provider: {disc.ProviderValue ?? "(null)"}");
                }
            }

            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey();
        }

        private async Task ShowVideoDownloads(CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine("[bold cyan]Video Downloads[/]");

            while (true)
            {
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select download option")
                        .HighlightStyle("green")
                        .AddChoices(
                            "Download Videos",
                            "Download Snapshots",
                            "Back"
                        ));

                switch (choice)
                {
                    case "Download Videos":
                        await DownloadVideos(cancellationToken);
                        break;
                    case "Download Snapshots":
                        await DownloadSnapshots(cancellationToken);
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task ShowAnalyzeEvidenceMenu()
        {
            while (true)
            {
                AnsiConsole.MarkupLine("[bold cyan]Analyze Evidence[/]");
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select analysis type")
                        .HighlightStyle("green")
                        .AddChoices(
                            "Forensic Reports",
                            "Signal Anomalies",
                            "Access Control Monitoring",
                            "Chain of Custody",
                            "Validate Evidence",
                            "Back to Main Menu"
                        ));

                switch (choice)
                {
                    case "Forensic Reports":
                        await ShowForensicReports();
                        break;
                    case "Signal Anomalies":
                        await ShowSignalAnomalies();
                        break;
                    case "Access Control Monitoring":
                        await ShowAccessControl();
                        break;
                    case "Chain of Custody":
                        await ShowChainOfCustody();
                        break;
                    case "Validate Evidence":
                        await ShowValidateEvidenceMenu(CancellationToken.None);
                        break;
                    case "Back to Main Menu":
                        return;
                }

                AnsiConsole.MarkupLine("");
            }
        }

        private async Task ShowBrowseEventsMenu(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("BROWSE EVENTS");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var devices = await _deviceRepository.ListAsync(ct);
            if (!devices.Any())
            {
                Console.WriteLine("No devices found.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nSelect device:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name}");
            }

            if (!int.TryParse(Console.ReadLine(), out int deviceChoice) || deviceChoice < 1 || deviceChoice > devices.Count)
            {
                Console.WriteLine("Invalid selection.");
                Console.ReadKey();
                return;
            }

            var selectedDevice = devices[deviceChoice - 1];

            Console.Write("\nEnter start date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var fromDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            Console.Write("Enter end date (yyyy-MM-dd): ");
            if (!DateTime.TryParse(Console.ReadLine(), out var toDate))
            {
                Console.WriteLine("Invalid date format.");
                Console.ReadKey();
                return;
            }

            // Adjust dates to include full day boundaries: start at 00:00:01, end at 23:59:59
            var adjustedFromDate = fromDate.Date.AddSeconds(1).ToUniversalTime();
            var adjustedToDate = toDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToUniversalTime();

            var events = await _eventRepository.ListByDeviceAndDateRangeAsync(selectedDevice.Id, adjustedFromDate, adjustedToDate, ct);

            Console.WriteLine($"\n{events.Count} event(s) found:");
            Console.WriteLine("───────────────────────────────────────────────────────────");
            foreach (var evt in events)
            {
                Console.WriteLine($"Type: {evt.EventType} | Occurred: {evt.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Provider Event ID: {evt.ProviderEventId}");
                if (!string.IsNullOrEmpty(evt.SnapshotUrl))
                    Console.WriteLine($"  Snapshot: {evt.SnapshotUrl}");
                Console.WriteLine();
            }

            Console.WriteLine("Press any key to return...");
            Console.ReadKey();
        }

        private async Task ShowQueryApiMenu(CancellationToken ct)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("QUERY API");
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                AnsiConsole.MarkupLine("[yellow]Runs a live query against the provider API and saves the raw result as JSON.[/]");

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a query to run")
                        .HighlightStyle("green")
                        .AddChoices(
                            "Account Status",
                            "Locations",
                            "Devices",
                            "Device Events",
                            "Device Config",
                            "Back to Main Menu"
                        ));

                try
                {
                    switch (choice)
                    {
                        case "Account Status":
                            await QueryAccountStatusAsync(ct);
                            break;
                        case "Locations":
                            await QueryLocationsAsync(ct);
                            break;
                        case "Devices":
                            await QueryDevicesAsync(ct);
                            break;
                        case "Device Events":
                            await QueryDeviceEventsAsync(ct);
                            break;
                        case "Device Config":
                            await QueryDeviceConfigAsync(ct);
                            break;
                        case "Back to Main Menu":
                            return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Query API request failed");
                    AnsiConsole.MarkupLine("[red]✗ Query failed: {0}[/]", EscapeMarkup(ex.Message));
                }

                AnsiConsole.MarkupLine("");
                AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
                Console.ReadKey();
            }
        }

        private async Task QueryAccountStatusAsync(CancellationToken ct)
        {
            var isAuthenticated = await _authService.IsAuthenticatedAsync(ct);
            var result = new
            {
                AuthStatus = _authService.GetAuthStatus(),
                IsAuthenticated = isAuthenticated,
                ActiveProviderAccountId = _forensicsConfig.ActiveProviderAccountId
            };

            await WriteQueryResultAsync("account-status", result, ct);
        }

        private async Task QueryLocationsAsync(CancellationToken ct)
        {
            if (!await EnsureAuthenticatedAsync(ct))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var locations = await _deviceService.GetLocationsAsync(ct);
            AnsiConsole.MarkupLine("[green]✓ {0} location(s) retrieved[/]", locations.Count);
            await WriteQueryResultAsync("locations", locations, ct);
        }

        private async Task QueryDevicesAsync(CancellationToken ct)
        {
            if (!await EnsureAuthenticatedAsync(ct))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var locations = await _deviceService.GetLocationsAsync(ct);
            if (locations.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No locations found on the account.[/]");
                return;
            }

            Console.WriteLine("\nSelect location:");
            for (int i = 0; i < locations.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {locations[i].Name}");
            }

            Console.Write("\nEnter location number: ");
            if (!int.TryParse(Console.ReadLine(), out int locationChoice) || locationChoice < 1 || locationChoice > locations.Count)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }

            var selectedLocation = locations[locationChoice - 1];
            var devices = await _deviceService.GetDevicesAsync(selectedLocation.Id, ct);
            AnsiConsole.MarkupLine("[green]✓ {0} device(s) retrieved for {1}[/]", devices.Count, EscapeMarkup(selectedLocation.Name));
            await WriteQueryResultAsync($"devices_{selectedLocation.Name}", devices, ct);
        }

        private async Task QueryDeviceEventsAsync(CancellationToken ct)
        {
            if (!await EnsureAuthenticatedAsync(ct))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var devices = await _deviceRepository.ListAsync(ct);
            if (!devices.Any())
            {
                Console.WriteLine("No devices found.");
                return;
            }

            Console.WriteLine("\nSelect device:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name}");
            }

            Console.Write("\nEnter device number: ");
            if (!int.TryParse(Console.ReadLine(), out int deviceChoice) || deviceChoice < 1 || deviceChoice > devices.Count)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }

            var selectedDevice = devices[deviceChoice - 1];

            var defaultStartDate = DateTime.Now.AddDays(-1);
            var defaultEndDate = DateTime.Now;

            var fromDate = AskDateWithEditableDefault(
                "[yellow]Start date (M-d-yy, yyyy-MM-dd):[/]",
                defaultStartDate);

            var toDate = AskDateWithEditableDefault(
                "[yellow]End date (M-d-yy, yyyy-MM-dd):[/]",
                defaultEndDate);

            // Adjust dates to include full day boundaries: start at 00:00:01, end at 23:59:59
            var adjustedFromDate = fromDate.Date.AddSeconds(1).ToUniversalTime();
            var adjustedToDate = toDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59).ToUniversalTime();

            var events = await _eventAndConfigService.GetEventsAsync(selectedDevice.ProviderDeviceId, adjustedFromDate, adjustedToDate, cancellationToken: ct);
            AnsiConsole.MarkupLine("[green]✓ {0} event(s) retrieved for {1}[/]", events.Count, EscapeMarkup(selectedDevice.Name));
            await WriteQueryResultAsync($"device-events_{selectedDevice.Name}", events, ct);
        }

        private async Task QueryDeviceConfigAsync(CancellationToken ct)
        {
            if (!await EnsureAuthenticatedAsync(ct))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var devices = await _deviceRepository.ListAsync(ct);
            if (!devices.Any())
            {
                Console.WriteLine("No devices found.");
                return;
            }

            Console.WriteLine("\nSelect device:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name}");
            }

            Console.Write("\nEnter device number: ");
            if (!int.TryParse(Console.ReadLine(), out int deviceChoice) || deviceChoice < 1 || deviceChoice > devices.Count)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }

            var selectedDevice = devices[deviceChoice - 1];
            var config = await _eventAndConfigService.GetDeviceConfigAsync(selectedDevice.ProviderDeviceId, ct);
            if (config == null)
            {
                AnsiConsole.MarkupLine("[yellow]No configuration returned for {0}[/]", EscapeMarkup(selectedDevice.Name));
                return;
            }

            await WriteQueryResultAsync($"device-config_{selectedDevice.Name}", config, ct);
        }

        /// <summary>Serializes a query result to JSON and writes it under the query export location.</summary>
        private async Task WriteQueryResultAsync(string queryLabel, object result, CancellationToken ct)
        {
            var exportDirectory = await GetQueryExportLocation(ct);
            if (exportDirectory == null)
            {
                AnsiConsole.MarkupLine("[yellow]Query result not saved (no export location available).[/]");
                return;
            }

            var sanitizedLabel = new string(queryLabel.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            var fileName = $"{sanitizedLabel}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(exportDirectory, fileName);

            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json, ct);

            _logger.LogInformation("Query result saved: {FilePath}", filePath);
            AnsiConsole.MarkupLine("[green]✓ Saved to: {0}[/]", filePath);
        }

        /// <summary>Resolves the directory to write query results to, prompting for and persisting a choice if none is configured yet.</summary>
        private async Task<string?> GetQueryExportLocation(CancellationToken ct)
        {
            if (!string.IsNullOrEmpty(_forensicsConfig.QueryExportLocation))
            {
                try
                {
                    if (!Directory.Exists(_forensicsConfig.QueryExportLocation))
                        Directory.CreateDirectory(_forensicsConfig.QueryExportLocation);
                    return _forensicsConfig.QueryExportLocation;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Saved export location not accessible: {0}[/]", ex.Message);
                }
            }

            // Use OneDrive-aware default (Documents/VideoForensics)
            var defaultPath = PathUtilities.GetDefaultQueryExportLocation();
            AnsiConsole.MarkupLine("[cyan]Suggested export location:[/] {0}", defaultPath);

            if (AnsiConsole.Confirm("Use this location?", true))
            {
                try
                {
                    Directory.CreateDirectory(defaultPath);
                    _forensicsConfig.QueryExportLocation = defaultPath;
                    await SaveConfiguration(ct);
                    return defaultPath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create query export directory: {ExportPath}", defaultPath);
                    AnsiConsole.MarkupLine("[yellow]⚠ Could not use suggested location: {0}[/]", ex.Message);
                }
            }

            var newPath = AnsiConsole.Ask<string>("[yellow]Enter output directory for query results:[/]");
            if (string.IsNullOrEmpty(newPath))
                return null;

            newPath = newPath.Trim();
            var validationError = ValidateDownloadPath(newPath);
            if (validationError != null)
            {
                AnsiConsole.MarkupLine("[red]✗ Invalid path: {0}[/]", validationError);
                return null;
            }

            try
            {
                Directory.CreateDirectory(newPath);
                _forensicsConfig.QueryExportLocation = newPath;
                await SaveConfiguration(ct);
                return newPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create query export directory: {ExportPath}", newPath);
                AnsiConsole.MarkupLine("[red]✗ Failed to create directory: {0}[/]", ex.Message);
                return null;
            }
        }

        private async Task ShowDeviceConfigurationMenu(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("═══════════════════════════════════════════════════════════");
            Console.WriteLine("DEVICE CONFIGURATION");
            Console.WriteLine("═══════════════════════════════════════════════════════════");

            var devices = await _deviceRepository.ListAsync(ct);
            if (!devices.Any())
            {
                Console.WriteLine("No devices found.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\nSelect device:");
            for (int i = 0; i < devices.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {devices[i].Name}");
            }

            if (!int.TryParse(Console.ReadLine(), out int deviceChoice) || deviceChoice < 1 || deviceChoice > devices.Count)
            {
                Console.WriteLine("Invalid selection.");
                Console.ReadKey();
                return;
            }

            var selectedDevice = devices[deviceChoice - 1];
            var latestConfig = await _deviceConfigRepository.GetLatestAsync(selectedDevice.Id, ct);

            Console.WriteLine($"\nLatest Configuration for {selectedDevice.Name}:");
            Console.WriteLine("───────────────────────────────────────────────────────────");
            if (latestConfig != null)
            {
                Console.WriteLine($"Motion Detection: {latestConfig.MotionDetectionEnabled?.ToString() ?? "Unknown"}");
                Console.WriteLine($"Motion Sensitivity: {latestConfig.MotionSensitivity?.ToString() ?? "Unknown"}");
                Console.WriteLine($"Recording Mode: {latestConfig.RecordingMode ?? "Unknown"}");
                Console.WriteLine($"Captured At: {latestConfig.CapturedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"Source: {latestConfig.Source}");
            }
            else
            {
                Console.WriteLine("No configuration snapshot found.");
            }

            Console.WriteLine("\nPress any key to return...");
            Console.ReadKey();
        }

        private async Task RunFullForensicWorkflow(CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine("[bold cyan]Full Forensic Workflow[/]");
            AnsiConsole.MarkupLine("[dim]Guided walkthrough: collect → analyze → review[/]");
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[bold]Step 1 of 3: Collect Evidence[/]");
            var collectChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to download?")
                    .HighlightStyle("green")
                    .AddChoices("Videos", "Snapshots", "Both", "Skip"));

            // Download* prompts for authentication itself if needed, so accounts aren't
            // forced to log in when this step is skipped or when only analyzing existing evidence.
            if (collectChoice == "Videos" || collectChoice == "Both")
                await DownloadVideos(cancellationToken);
            if (collectChoice == "Snapshots" || collectChoice == "Both")
                await DownloadSnapshots(cancellationToken);
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[bold]Step 2 of 3: Analyze Evidence[/]");
            await ShowForensicReports();
            AnsiConsole.MarkupLine("");
            await ShowSignalAnomalies();
            AnsiConsole.MarkupLine("");
            await ShowAccessControl();
            AnsiConsole.MarkupLine("");
            await ShowChainOfCustody();
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[bold]Step 3 of 3: Review Evidence[/]");
            await ShowEvidence();
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[bold green]✓ Full forensic workflow complete[/]");
        }

        private const int MaxAuthenticationAttempts = 3;

        /// <summary>
        /// Ensures a Ring session is active before a download/collection flow proceeds. First
        /// enumerates known provider accounts (so the flow always knows what's configured before
        /// touching the network), then tries to resume the most recently used one from its saved
        /// refresh token, falling back to an interactive username/password/2FA prompt only if no
        /// saved session could be restored.
        /// </summary>
        private async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken = default)
        {
            if (await _authService.IsAuthenticatedAsync(cancellationToken))
            {
                return true;
            }

            var accounts = await _providerAccountRepository.ListAsync(cancellationToken);
            if (accounts.Count > 0)
            {
                var activeAccountId = _forensicsConfig.ActiveProviderAccountId;
                var active = accounts.FirstOrDefault(a => a.Id == activeAccountId) ?? accounts[0];
                AnsiConsole.MarkupLine($"[dim]Found {accounts.Count} saved account(s); resuming {active.ProviderName} session...[/]");

                if (await _authService.RestoreFromSavedCredentialsAsync(active.Id, cancellationToken))
                {
                    AnsiConsole.MarkupLine("[green]✓ Resumed saved session[/]");
                    if (_forensicsConfig.ActiveProviderAccountId == null)
                    {
                        _forensicsConfig.ActiveProviderAccountId = active.Id;
                        await SaveConfiguration(cancellationToken);
                    }
                    return true;
                }

                AnsiConsole.MarkupLine("[yellow]Saved session could not be resumed; re-authentication required[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No saved accounts found; authentication required[/]");
            }

            return await AuthenticateWithTwoFactorAsync(cancellationToken);
        }

        private async Task<bool> AuthenticateWithTwoFactorAsync(CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; attempt <= MaxAuthenticationAttempts; attempt++)
            {
                AnsiConsole.MarkupLine("[bold cyan]Ring Account Authentication[/]");
                var username = AnsiConsole.Ask<string>("[yellow]Enter email address:[/]");
                var password = AnsiConsole.Prompt(new TextPrompt<string>("[yellow]Enter password:[/]").Secret());

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    AnsiConsole.MarkupLine("[red]✗ Authentication cancelled[/]");
                    return false;
                }

                // Provide 2FA callback that prompts the user for the code
                Func<Task<string>> twoFactorCodeProvider = async () =>
                {
                    AnsiConsole.MarkupLine("[yellow]Two-factor authentication required[/]");
                    var code = AnsiConsole.Prompt(
                        new TextPrompt<string>("[yellow]Enter the 2FA code (SMS or authenticator app):[/]").Secret()
                    );
                    return code;
                };

                var result = await _authService.AuthenticateWithTwoFactorAsync(username, password, twoFactorCodeProvider, cancellationToken);
                if (result.Success)
                {
                    AnsiConsole.MarkupLine("[green]✓ Authentication successful[/]");

                    // Without this, the active account stays unset after a fresh login (only the
                    // "resume saved session" path used to set it), so device downloads later had
                    // nothing to attach to and fell back to creating a synthetic "default" account.
                    if (result.ProviderAccountId.HasValue && _forensicsConfig.ActiveProviderAccountId != result.ProviderAccountId)
                    {
                        _forensicsConfig.ActiveProviderAccountId = result.ProviderAccountId;
                        await SaveConfiguration(cancellationToken);
                    }

                    return true;
                }

                AnsiConsole.MarkupLine("[red]✗ Authentication failed{0}[/]",
                    !string.IsNullOrEmpty(result.ErrorMessage) ? $": {result.ErrorMessage}" : "");

                if (attempt < MaxAuthenticationAttempts)
                {
                    AnsiConsole.MarkupLine("[yellow]Attempt {0} of {1} — please try again[/]", attempt, MaxAuthenticationAttempts);
                }
            }

            AnsiConsole.MarkupLine("[red]✗ Maximum authentication attempts ({0}) exceeded[/]", MaxAuthenticationAttempts);
            return false;
        }

        private async Task DownloadVideos(CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine("[bold cyan]Download Ring Videos[/]");

            if (!await EnsureAuthenticatedAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var outputPath = await GetDownloadLocation(cancellationToken);
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            // Discover devices
            var devices = await DiscoverDevicesAsync();
            if (devices == null)
            {
                return;
            }

            // Check if any device has prior download history by querying the device repository
            var hasAnyPriorDownloads = false;
            var allDevices = await _deviceRepository.ListAsync(cancellationToken);
            if (allDevices != null)
            {
                hasAnyPriorDownloads = allDevices.Any(d => d.LastSuccessfulPullAtUtc.HasValue);
            }

            // Determine default start date: use account watermark if available, otherwise use 181 days ago
            var defaultStartDate = DateTime.Today.AddDays(-181);
            if (_forensicsConfig.ActiveProviderAccountId.HasValue)
            {
                try
                {
                    var accountWatermark = await _videoForensicsDataClient.GetAccountDownloadWatermarkAsync(_forensicsConfig.ActiveProviderAccountId.Value, cancellationToken);
                    defaultStartDate = accountWatermark;
                    _logger.LogInformation("Using account download watermark as default start date: {WatermarkDate:yyyy-MM-dd}", accountWatermark);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retrieve account download watermark; falling back to 181 days ago");
                }
            }

            // Override with saved configuration if available
            if (DateTime.TryParseExact(_forensicsConfig.DownloadStartDate, "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDefault))
            {
                defaultStartDate = parsedDefault;
            }
            var startDate = AskDateWithEditableDefault("[yellow]Start date to pull from (yyyy-MM-dd or M-d-yy):[/]", defaultStartDate);
            var startDateString = startDate.ToString("yyyy-MM-dd");
            if (startDateString != _forensicsConfig.DownloadStartDate)
            {
                _logger.LogInformation("Updating DownloadStartDate from {Old} to {New}", _forensicsConfig.DownloadStartDate, startDateString);
                _forensicsConfig.DownloadStartDate = startDateString;
                await SaveConfiguration(cancellationToken);
                _logger.LogInformation("DownloadStartDate saved: {Value}", _forensicsConfig.DownloadStartDate);
            }

            // Adjust dates to include full day boundaries: start at 00:00:01, end at 23:59:59
            var adjustedStartDate = startDate.Date.AddSeconds(1);
            var endDate = DateTime.Now;
            var adjustedEndDate = endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

            // If no prior downloads, force a full re-scan to fetch all data. If there are prior downloads,
            // ask whether to fetch incrementally (since last pull) or force a full re-scan.
            var force = false;
            if (!hasAnyPriorDownloads)
            {
                force = true;
                _logger.LogInformation("No prior downloads found; automatically forcing full re-scan to fetch all available data");
            }
            else
            {
                force = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Force re-scan the full window above, or only fetch what's new since the last successful pull?[/]")
                        .HighlightStyle("green")
                        .AddChoices("Only fetch new items (recommended)", "Force full re-scan")) == "Force full re-scan";
            }

            // Display devices, with an Items column that fills in live as the pre-scan (matched-event
            // count per device) runs — before any actual downloading starts.
            AnsiConsole.MarkupLine("[cyan]Devices to download from:[/]");
            var table = new Table();
            table.AddColumn("Device");
            table.AddColumn("Location");
            table.AddColumn("Items");
            foreach (var (id, name, location) in devices)
            {
                table.AddRow(name, location, "[dim]…[/]");
            }

            await AnsiConsole.Live(table).StartAsync(async ctx =>
            {
                ctx.Refresh();

                Task preScanTask;
                try
                {
                    preScanTask = _downloadService.PreScanAsync(outputPath, adjustedStartDate, adjustedEndDate, force, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pre-scan failed to start; Items column will stay blank until download starts");
                    return;
                }

                while (!preScanTask.IsCompleted)
                {
                    var counts = _downloadService.GetPreScanCounts();
                    for (var i = 0; i < devices.Count; i++)
                    {
                        if (counts.TryGetValue(devices[i].Id, out var count))
                        {
                            table.UpdateCell(i, 2, new Markup(count.ToString()));
                        }
                    }
                    ctx.Refresh();
                    await Task.Delay(200, cancellationToken);
                }

                try
                {
                    await preScanTask;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Pre-scan failed; Items column may be incomplete");
                }

                var finalCounts = _downloadService.GetPreScanCounts();
                for (var i = 0; i < devices.Count; i++)
                {
                    table.UpdateCell(i, 2, new Markup(finalCounts.TryGetValue(devices[i].Id, out var count) ? count.ToString() : "[dim]?[/]"));
                }
                ctx.Refresh();
            });
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[dim]Scanning for videos from {0} device(s)...[/]", devices.Count);
            AnsiConsole.MarkupLine("");

            // Build device ID to location name mapping and pass it to the adapter
            var deviceIdToLocationMapping = new Dictionary<string, string>();
            foreach (var (deviceId, deviceName, locationName) in devices)
            {
                deviceIdToLocationMapping[deviceId] = locationName;
            }

            if (_downloadService is VideoDownloadServiceAdapter adapter)
            {
                adapter.SetDeviceLocationMapping(deviceIdToLocationMapping);
            }

            while (true)
            {
                var result = await RunDownloadWithProgressAsync(
                    () => _downloadService.DownloadVideosAsync(outputPath, adjustedStartDate, adjustedEndDate, force),
                    "videos",
                    cancellationToken);

                var downloadedCount = Directory.Exists(outputPath)
                    ? Directory.GetFiles(outputPath, "*.mp4").Length
                    : 0;

                if (result)
                {
                    _forensicsConfig.DownloadLocation = outputPath;
                    // Ensure DownloadStartDate is persisted
                    _logger.LogInformation("Persisting configuration after download: DownloadStartDate={StartDate}", _forensicsConfig.DownloadStartDate);
                    await SaveConfiguration(cancellationToken);

                    // Display summary
                    AnsiConsole.MarkupLine("[bold cyan]Download Summary[/]");
                    AnsiConsole.MarkupLine("[yellow]Date Range:[/] {0:yyyy-MM-dd} to {1:yyyy-MM-dd} ({2} days)",
                        startDate, endDate, (endDate - startDate).Days);
                    AnsiConsole.MarkupLine("[yellow]Locations Scanned:[/] {0}",
                        string.Join(", ", devices.GroupBy(d => d.Location).Select(g => g.Key)));
                    AnsiConsole.MarkupLine("[yellow]Devices:[/]");

                    var summaryTable = new Table();
                    summaryTable.AddColumn("Device");
                    summaryTable.AddColumn("Location");
                    foreach (var (id, name, location) in devices)
                    {
                        summaryTable.AddRow(name, location);
                    }
                    AnsiConsole.Write(summaryTable);

                    AnsiConsole.MarkupLine("");
                    if (downloadedCount == 0)
                    {
                        AnsiConsole.MarkupLine("[yellow]⚠ No videos found in the specified date range[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[green]✓ Total Videos Downloaded: {0}[/]", downloadedCount);
                    }
                    AnsiConsole.MarkupLine("[green]✓ Saved to: {0}[/]", outputPath);

                    var remaining = _downloadService.GetRemainingCount();
                    if (remaining > 0)
                    {
                        var reason = _downloadService.GetRemainingReason();
                        var reasonSuffix = reason != null ? $" ({EscapeMarkup(reason)})" : " (reason unknown)";
                        AnsiConsole.MarkupLine("");
                        var choice = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title($"[yellow]{remaining} more video(s) matched but weren't downloaded{reasonSuffix}. Continue downloading the rest?[/]")
                                .HighlightStyle("green")
                                .AddChoices("Continue downloading", "Return to Main Menu"));

                        if (choice == "Continue downloading")
                        {
                            AnsiConsole.MarkupLine("");
                            continue;
                        }
                    }
                }
                else
                {
                    var error = _downloadService.GetLastError();
                    var safeError = EscapeMarkup(error);
                    AnsiConsole.MarkupLine("[red]✗ Download failed{0}[/]",
                        !string.IsNullOrEmpty(safeError) ? $": {safeError}" : "");
                }

                break;
            }
        }

        private async Task DownloadSnapshots(CancellationToken cancellationToken = default)
        {
            AnsiConsole.MarkupLine("[bold cyan]Download Ring Snapshots[/]");

            if (!await EnsureAuthenticatedAsync(cancellationToken))
            {
                AnsiConsole.MarkupLine("[red]✗ Cannot proceed without authentication[/]");
                return;
            }

            var outputPath = await GetDownloadLocation(cancellationToken);
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            // TODO(Phase1-followup): Once IWatermarkService is wired in via _dataClient (see https://github.com/path/to/plan),
            // add a force-rescan option here. For snapshots, this is less critical since they're always "latest", but the
            // database dedup still applies (see RingMediaDownloadService.DownloadSnapshotsAsync).
            // Ring only exposes each device's current/latest snapshot — there's no historical,
            // per-event snapshot API — so there's no date range to ask for here.
            var startDate = DateTime.Now;
            var endDate = DateTime.Now;

            // Discover devices
            var devices = await DiscoverDevicesAsync();
            if (devices == null)
            {
                return;
            }

            // Display devices
            AnsiConsole.MarkupLine("[cyan]Devices to capture a snapshot from:[/]");
            var table = new Table();
            table.AddColumn("Device");
            table.AddColumn("Location");
            foreach (var (id, name, location) in devices)
            {
                table.AddRow(name, location);
            }
            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine("");

            AnsiConsole.MarkupLine("[dim]Capturing latest snapshot from {0} device(s)...[/]", devices.Count);
            AnsiConsole.MarkupLine("");

            // Build device ID to location name mapping and pass it to the adapter
            var deviceIdToLocationMapping = new Dictionary<string, string>();
            foreach (var (deviceId, deviceName, locationName) in devices)
            {
                deviceIdToLocationMapping[deviceId] = locationName;
            }

            if (_downloadService is VideoDownloadServiceAdapter adapter)
            {
                adapter.SetDeviceLocationMapping(deviceIdToLocationMapping);
            }

            var result = await RunDownloadWithProgressAsync(
                () => _downloadService.DownloadSnapshotsAsync(outputPath, startDate, endDate),
                "snapshots",
                cancellationToken);

            var downloadedCount = Directory.Exists(outputPath)
                ? Directory.GetFiles(outputPath, "*.jpg").Length
                : 0;

            if (result)
            {
                _forensicsConfig.DownloadLocation = outputPath;
                await SaveConfiguration(cancellationToken);

                // Display summary
                AnsiConsole.MarkupLine("[bold cyan]Snapshot Summary[/]");
                AnsiConsole.MarkupLine("[yellow]Captured:[/] latest available snapshot per device (as of {0:yyyy-MM-dd HH:mm})", DateTime.Now);
                AnsiConsole.MarkupLine("[yellow]Locations Scanned:[/] {0}",
                    string.Join(", ", devices.GroupBy(d => d.Location).Select(g => g.Key)));
                AnsiConsole.MarkupLine("[yellow]Devices:[/]");

                var summaryTable = new Table();
                summaryTable.AddColumn("Device");
                summaryTable.AddColumn("Location");
                foreach (var (id, name, location) in devices)
                {
                    summaryTable.AddRow(name, location);
                }
                AnsiConsole.Write(summaryTable);

                AnsiConsole.MarkupLine("");
                if (downloadedCount == 0)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ No snapshots could be captured[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓ Total Snapshots Downloaded: {0}[/]", downloadedCount);
                }
                AnsiConsole.MarkupLine("[green]✓ Saved to: {0}[/]", outputPath);
            }
            else
            {
                var error = _downloadService.GetLastError();
                var safeError = EscapeMarkup(error);
                AnsiConsole.MarkupLine("[red]✗ Download failed{0}[/]",
                    !string.IsNullOrEmpty(safeError) ? $": {safeError}" : "");
            }
        }

        private async Task<string?> GetDownloadLocation(CancellationToken cancellationToken = default)
        {
            // Use saved location if it exists and is accessible
            if (!string.IsNullOrEmpty(_forensicsConfig.DownloadLocation))
            {
                try
                {
                    if (!Directory.Exists(_forensicsConfig.DownloadLocation))
                    {
                        _logger.LogInformation("Creating download directory: {DownloadPath}", _forensicsConfig.DownloadLocation);
                        Directory.CreateDirectory(_forensicsConfig.DownloadLocation);
                    }
                    _logger.LogInformation("Using download location: {DownloadPath}", _forensicsConfig.DownloadLocation);
                    AnsiConsole.MarkupLine("[cyan]Download location:[/] {0}", _forensicsConfig.DownloadLocation);
                    return _forensicsConfig.DownloadLocation;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine("[yellow]⚠ Saved location not accessible: {0}[/]", ex.Message);
                }
            }

            // Use OneDrive-aware default location
            var defaultPath = PathUtilities.GetDefaultDownloadLocation();
            AnsiConsole.MarkupLine("[cyan]Suggested location:[/] {0}", defaultPath);

            if (AnsiConsole.Confirm("Use this location?", true))
            {
                try
                {
                    if (!Directory.Exists(defaultPath))
                    {
                        _logger.LogInformation("Creating download directory: {DownloadPath}", defaultPath);
                        Directory.CreateDirectory(defaultPath);
                    }
                    _forensicsConfig.DownloadLocation = defaultPath;
                    _logger.LogInformation("Download location configured: {DownloadPath}", defaultPath);
                    await SaveConfiguration(cancellationToken);
                    AnsiConsole.MarkupLine("[green]✓ Location saved and configured[/]");
                    return defaultPath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create directory: {DownloadPath}", defaultPath);
                    AnsiConsole.MarkupLine("[yellow]⚠ Could not use suggested location: {0}[/]", ex.Message);
                }
            }

            // Ask for custom location if user declined default
            var newPath = AnsiConsole.Ask<string>("[yellow]Enter output directory for downloads:[/]");

            if (!string.IsNullOrEmpty(newPath))
            {
                newPath = newPath.Trim();
                var validationError = ValidateDownloadPath(newPath);
                if (validationError != null)
                {
                    AnsiConsole.MarkupLine("[red]✗ Invalid path: {0}[/]", validationError);
                    return await GetDownloadLocation(cancellationToken);
                }

                try
                {
                    _logger.LogInformation("Creating new download directory: {DownloadPath}", newPath);
                    Directory.CreateDirectory(newPath);
                    _forensicsConfig.DownloadLocation = newPath;
                    _logger.LogInformation("Download location configured: {DownloadPath}", newPath);
                    await SaveConfiguration(cancellationToken);
                    AnsiConsole.MarkupLine("[green]✓ Location saved and configured[/]");
                    return newPath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create directory: {DownloadPath}", newPath);
                    AnsiConsole.MarkupLine("[red]✗ Failed to create directory: {0}[/]", ex.Message);
                    return null;
                }
            }

            return null;
        }

        private async Task ShowConfigurationMenu(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                AnsiConsole.MarkupLine("[bold cyan]⚙️  Configuration Menu[/]");
                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure settings")
                        .HighlightStyle("green")
                        .AddChoices(
                            "Report Generation Settings",
                            "PII Redaction Settings",
                            "Key Storage Configuration",
                            "Retention Policy",
                            "Download Location",
                            "Query Export Location",
                            "Max Concurrent Downloads",
                            "Logging Level",
                            "Factory Reset (Destructive)",
                            "Back to Main Menu"
                        ));

                switch (choice)
                {
                    case "Report Generation Settings":
                        await ConfigureReportGeneration();
                        break;
                    case "PII Redaction Settings":
                        ConfigurePiiRedaction();
                        break;
                    case "Key Storage Configuration":
                        ConfigureKeyStorage();
                        break;
                    case "Retention Policy":
                        await ConfigureRetentionPolicy();
                        break;
                    case "Download Location":
                        await ConfigureDownloadLocation();
                        break;
                    case "Query Export Location":
                        await ConfigureQueryExportLocation();
                        break;
                    case "Max Concurrent Downloads":
                        await ConfigureMaxConcurrentDownloads();
                        break;
                    case "Logging Level":
                        ConfigureLoggingLevel();
                        break;
                    case "Factory Reset (Destructive)":
                        await PerformFactoryReset();
                        return;
                    case "Back to Main Menu":
                        await SaveConfiguration(cancellationToken);
                        return;
                }

                AnsiConsole.MarkupLine("");
            }
        }

        private async Task ConfigureReportGeneration()
        {
            AnsiConsole.MarkupLine("[bold cyan]Report Generation Settings[/]");

            while (true)
            {
                var reportChoices = new[]
                {
                    EscapeMarkup($"Forensic Analysis Reports [{(_forensicsConfig.EnableForensicAnalysisReports ? "ON" : "OFF")}]"),
                    EscapeMarkup($"Signal Anomaly Reports [{(_forensicsConfig.EnableSignalAnomalyReports ? "ON" : "OFF")}]"),
                    EscapeMarkup($"Chain of Custody Reports [{(_forensicsConfig.EnableChainOfCustodyReports ? "ON" : "OFF")}]"),
                    EscapeMarkup($"Evidence Validation Reports [{(_forensicsConfig.EnableEvidenceValidationReports ? "ON" : "OFF")}]"),
                    "Set Reports Output Directory",
                    "Set Report Format",
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a report type to toggle")
                        .HighlightStyle("green")
                        .AddChoices(reportChoices)
                );

                switch (choice)
                {
                    case var c when c.StartsWith("Forensic Analysis"):
                        _forensicsConfig.EnableForensicAnalysisReports = !_forensicsConfig.EnableForensicAnalysisReports;
                        break;
                    case var c when c.StartsWith("Signal Anomaly"):
                        _forensicsConfig.EnableSignalAnomalyReports = !_forensicsConfig.EnableSignalAnomalyReports;
                        break;
                    case var c when c.StartsWith("Chain of Custody"):
                        _forensicsConfig.EnableChainOfCustodyReports = !_forensicsConfig.EnableChainOfCustodyReports;
                        break;
                    case var c when c.StartsWith("Evidence Validation"):
                        _forensicsConfig.EnableEvidenceValidationReports = !_forensicsConfig.EnableEvidenceValidationReports;
                        break;
                    case "Set Reports Output Directory":
                        await ConfigureQueryExportLocation();
                        break;
                    case "Set Report Format":
                        var format = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select report format")
                                .AddChoices("json", "xml", "csv"));
                        _forensicsConfig.ReportOutputFormat = format;
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private void ConfigurePiiRedaction()
        {
            AnsiConsole.MarkupLine("[bold cyan]PII Redaction Settings[/]");

            while (true)
            {
                var piiChoices = new[]
                {
                    EscapeMarkup($"Enable PII Redaction [{(_forensicsConfig.EnablePiiRedaction ? "ON" : "OFF")}]"),
                    EscapeMarkup($"Redaction Level: {_forensicsConfig.RedactionLevel}"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure PII redaction")
                        .HighlightStyle("green")
                        .AddChoices(piiChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Enable"):
                        _forensicsConfig.EnablePiiRedaction = !_forensicsConfig.EnablePiiRedaction;
                        break;
                    case var c when c.StartsWith("Redaction Level"):
                        var level = AnsiConsole.Prompt(
                            new SelectionPrompt<VideoForensics.Client.Common.RedactionLevel>()
                                .Title("Select redaction level")
                                .AddChoices(
                                    VideoForensics.Client.Common.RedactionLevel.None,
                                    VideoForensics.Client.Common.RedactionLevel.Light,
                                    VideoForensics.Client.Common.RedactionLevel.Medium,
                                    VideoForensics.Client.Common.RedactionLevel.Heavy));
                        _forensicsConfig.RedactionLevel = level;
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private void ConfigureKeyStorage()
        {
            AnsiConsole.MarkupLine("[bold cyan]Key Storage Configuration[/]");

            while (true)
            {
                var storageChoices = new[]
                {
                    EscapeMarkup($"Storage Provider [{_forensicsConfig.KeyStorageProvider}]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure key storage")
                        .HighlightStyle("green")
                        .AddChoices(storageChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Storage Provider"):
                        var provider = AnsiConsole.Prompt(
                            new SelectionPrompt<KeyStorageProvider>()
                                .Title("Select key storage provider")
                                .HighlightStyle("green")
                                .AddChoices(
                                    KeyStorageProvider.Auto,
                                    KeyStorageProvider.Tpm,
                                    KeyStorageProvider.Dpapi,
                                    KeyStorageProvider.FileBased));

                        _forensicsConfig.KeyStorageProvider = provider;

                        var description = provider switch
                        {
                            KeyStorageProvider.Auto => "Automatic selection (TPM → DPAPI → File-based)",
                            KeyStorageProvider.Tpm => "Hardware TPM 2.0 (most secure)",
                            KeyStorageProvider.Dpapi => "Windows DPAPI (Windows only)",
                            KeyStorageProvider.FileBased => "AES-256-GCM encrypted file storage (cross-platform)",
                            _ => "Unknown"
                        };

                        AnsiConsole.MarkupLine("[dim]{0}[/]", description);
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task ConfigureRetentionPolicy()
        {
            AnsiConsole.MarkupLine("[bold cyan]Retention Policy[/]");

            while (true)
            {
                var retentionChoices = new[]
                {
                    EscapeMarkup($"Retention Period [{_forensicsConfig.RetentionDaysDefault} days]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure retention")
                        .HighlightStyle("green")
                        .AddChoices(retentionChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Retention Period"):
                        var days = AnsiConsole.Ask<int>("[yellow]Enter retention period (days):[/]");
                        var (success, message) = await _configToolsOrchestrator.SetRetentionDaysAsync(_forensicsConfig, days);
                        if (success)
                        {
                            AnsiConsole.MarkupLine("[green]{0}[/]", message);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]{0}[/]", message);
                        }
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task ConfigureMaxConcurrentDownloads()
        {
            AnsiConsole.MarkupLine("[bold cyan]⇅ Max Concurrent Downloads[/]");

            while (true)
            {
                var concurrencyChoices = new[]
                {
                    EscapeMarkup($"Concurrent Downloads [{_forensicsConfig.MaxConcurrentDownloads}]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure concurrent downloads")
                        .HighlightStyle("green")
                        .AddChoices(concurrencyChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Concurrent Downloads"):
                        var count = AnsiConsole.Ask<int>("[yellow]Enter max concurrent downloads (per device):[/]");
                        var (success, message) = await _configToolsOrchestrator.SetMaxConcurrentDownloadsAsync(_forensicsConfig, count);
                        if (success)
                        {
                            AnsiConsole.MarkupLine("[green]{0}[/]", message);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]{0}[/]", message);
                        }
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task ConfigureDownloadLocation()
        {
            AnsiConsole.MarkupLine("[bold cyan]Download Location[/]");

            while (true)
            {
                var downloadChoices = new[]
                {
                    EscapeMarkup($"Download Directory [{(_forensicsConfig.DownloadLocation ?? "Not set")}]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure download location")
                        .HighlightStyle("green")
                        .AddChoices(downloadChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Download Directory"):
                        if (!string.IsNullOrEmpty(_forensicsConfig.DownloadLocation))
                        {
                            AnsiConsole.MarkupLine("[yellow]Current location:[/] {0}", _forensicsConfig.DownloadLocation);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[yellow]No location set[/]");
                        }

                        var newPath = AnsiConsole.Ask<string>("[yellow]Enter new download directory (or press Enter to keep current):[/]", _forensicsConfig.DownloadLocation ?? "");

                        if (!string.IsNullOrEmpty(newPath))
                        {
                            try
                            {
                                Directory.CreateDirectory(newPath);
                                _forensicsConfig.DownloadLocation = newPath;
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine("[red]✗ Failed to set location: {0}[/]", ex.Message);
                            }
                        }
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task ConfigureQueryExportLocation()
        {
            AnsiConsole.MarkupLine("[bold cyan]Query Export Location[/]");

            while (true)
            {
                var exportChoices = new[]
                {
                    EscapeMarkup($"Export Directory [{(_forensicsConfig.QueryExportLocation ?? "Not set - defaults to " + PathUtilities.GetDefaultQueryExportLocation())}]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure query export location")
                        .HighlightStyle("green")
                        .AddChoices(exportChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Export Directory"):
                        var newPath = AnsiConsole.Ask<string>("[yellow]Enter new export directory (or press Enter to keep current):[/]", _forensicsConfig.QueryExportLocation ?? "");

                        if (!string.IsNullOrEmpty(newPath))
                        {
                            var validationError = ValidateDownloadPath(newPath);
                            if (validationError != null)
                            {
                                AnsiConsole.MarkupLine("[red]✗ Invalid path: {0}[/]", validationError);
                                break;
                            }

                            try
                            {
                                Directory.CreateDirectory(newPath);
                                _forensicsConfig.QueryExportLocation = newPath;
                                await SaveConfiguration();
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine("[red]✗ Failed to set location: {0}[/]", ex.Message);
                            }
                        }
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private void ConfigureLoggingLevel()
        {
            AnsiConsole.MarkupLine("[bold cyan]Logging Level[/]");

            while (true)
            {
                var loggingChoices = new[]
                {
                    EscapeMarkup($"Logging Level [{_forensicsConfig.LogLevel}]"),
                    "Back"
                };

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Configure logging")
                        .HighlightStyle("green")
                        .AddChoices(loggingChoices));

                switch (choice)
                {
                    case var c when c.StartsWith("Logging Level"):
                        var level = AnsiConsole.Prompt(
                            new SelectionPrompt<string>()
                                .Title("Select logging level")
                                .HighlightStyle("green")
                                .AddChoices(
                                    "Debug",
                                    "Information",
                                    "Warning",
                                    "Error",
                                    "Critical"));

                        _forensicsConfig.LogLevel = level;
                        break;
                    case "Back":
                        return;
                }
            }
        }

        private async Task PerformFactoryReset()
        {
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[bold red]⚠️  FACTORY RESET - DESTRUCTIVE OPERATION[/]");
            AnsiConsole.MarkupLine("[red]This will:[/]");
            AnsiConsole.MarkupLine("[red]  • Delete ALL downloaded video files[/]");
            AnsiConsole.MarkupLine("[red]  • Delete the entire database (all data and settings)[/]");
            AnsiConsole.MarkupLine("[red]  • Exit the application[/]");
            AnsiConsole.MarkupLine("");

            var confirm1 = AnsiConsole.Confirm("[bold yellow]Are you absolutely sure? (yes/no)[/]");
            if (!confirm1)
            {
                AnsiConsole.MarkupLine("[yellow]Factory reset cancelled.[/]");
                return;
            }

            var confirm2 = AnsiConsole.Confirm("[bold red]This action CANNOT be undone. Type 'yes' to confirm:[/]");
            if (!confirm2)
            {
                AnsiConsole.MarkupLine("[yellow]Factory reset cancelled.[/]");
                return;
            }

            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("[yellow]Performing factory reset...[/]");

            try
            {
                // Use orchestrator for the core reset logic
                var (success, message) = await _configToolsOrchestrator.FactoryResetAsync();

                if (success)
                {
                    AnsiConsole.MarkupLine("[green]✓ {0}[/]", message);
                    AnsiConsole.MarkupLine("[bold green]✓ Factory reset complete. Exiting...[/]");
                    AnsiConsole.MarkupLine("");
                    await Task.Delay(250);
                    Environment.Exit(0);
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗ {0}[/]", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during factory reset");
                AnsiConsole.MarkupLine("[red]✗ Factory reset failed: {0}[/]", ex.Message);
            }
        }

        private async Task SaveConfiguration(CancellationToken cancellationToken = default)
        {
            try
            {
                await _configService.SaveConfigurationAsync(_forensicsConfig, cancellationToken);
                AnsiConsole.MarkupLine("[green]✓ Configuration saved[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine("[red]✗ Failed to save configuration: {0}[/]", ex.Message);
            }
        }

        private string? ValidateDownloadPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "Path cannot be empty";

            try
            {
                // Reject relative paths with .. traversal attempts
                if (path.Contains(".."))
                    return "Path traversal (..) not allowed";

                // Reject paths with null characters
                if (path.Contains('\0'))
                    return "Path contains invalid characters";

                // Convert to full path to normalize and validate
                var fullPath = Path.GetFullPath(path);

                // Ensure path is not a system-critical directory
                var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

                if (fullPath.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(programFiles, StringComparison.OrdinalIgnoreCase) ||
                    fullPath.StartsWith(windows, StringComparison.OrdinalIgnoreCase))
                    return "Cannot use system directories";

                return null; // Valid
            }
            catch (Exception ex)
            {
                return $"Invalid path format: {ex.Message}";
            }
        }

        private static string EscapeMarkup(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("[", "[[")
                .Replace("]", "]]");
        }

        /// <summary>
        /// Discovers devices across all locations, printing progress/error/empty-result messages
        /// as it goes. Returns null (with a message already shown) if discovery failed or found
        /// nothing; callers should just check for null and return.
        /// </summary>
        private async Task<List<(string Id, string Name, string Location)>?> DiscoverDevicesAsync()
        {
            AnsiConsole.MarkupLine("[dim]Discovering devices...[/]");
            var deviceDict = new Dictionary<string, (string Id, string Name, string Location)>();

            try
            {
                var locations = await _deviceService.GetLocationsAsync();
                if (locations != null && locations.Count > 0)
                {
                    foreach (var location in locations)
                    {
                        var locationDevices = await _deviceService.GetDevicesAsync(location.Id.ToString());
                        if (locationDevices != null)
                        {
                            foreach (var device in locationDevices)
                            {
                                if (!deviceDict.ContainsKey(device.Id))
                                {
                                    deviceDict[device.Id] = (device.Id, device.Name, location.Name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗ Device discovery failed: {EscapeMarkup(ex.Message)}[/]");
                return null;
            }

            var devices = deviceDict.Values.ToList();
            if (devices.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No devices found on this account[/]");
                return null;
            }

            return devices;
        }

        /// <summary>
        /// Runs a download with three live-updating progress bars: current device, total aggregate,
        /// and speed/connections info. Plus a scrolling feed of per-file outcomes.
        /// </summary>
        private async Task<bool> RunDownloadWithProgressAsync(Func<Task<bool>> startDownload, string mediaLabel, CancellationToken cancellationToken)
        {
            var result = false;
            var globalStartTime = DateTime.Now;
            var deviceStartTime = DateTime.Now;
            var lastDeviceIndex = 0;

            await AnsiConsole.Progress()
                .AutoClear(true)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn())
                .StartAsync(async ctx =>
                {
                    var deviceTask = ctx.AddTask("[cyan]Current Device[/]", maxValue: 1);
                    var totalTask = ctx.AddTask("[cyan]Total Progress[/]", maxValue: 1);
                    var speedTask = ctx.AddTask("[cyan]Speed & Connections[/]", maxValue: 1);

                    deviceTask.IsIndeterminate = true;
                    totalTask.IsIndeterminate = true;
                    speedTask.IsIndeterminate = true;

                    var downloadTask = startDownload();

                    while (!downloadTask.IsCompleted)
                    {
                        var progress = _downloadService.GetProgress();
                        var (deviceIndex, deviceTotal, deviceName) = _downloadService.GetCurrentDevice();

                        // Reset per-device timer when moving to a new device
                        if (deviceIndex != lastDeviceIndex)
                        {
                            deviceStartTime = DateTime.Now;
                            lastDeviceIndex = deviceIndex;
                        }

                        // Bar 1: Current Device
                        if (progress.FilesTotal > 0)
                        {
                            deviceTask.IsIndeterminate = false;
                            deviceTask.MaxValue = progress.FilesTotal;
                            deviceTask.Value = progress.FilesCompleted;
                            var deviceElapsed = DateTime.Now - deviceStartTime;
                            var deviceTimeStr = $"{deviceElapsed.Hours:D2}:{deviceElapsed.Minutes:D2}:{deviceElapsed.Seconds:D2}";
                            deviceTask.Description = $"[cyan]Device {deviceIndex}/{deviceTotal}[/] {EscapeMarkup(deviceName)}: {progress.FilesCompleted}/{progress.FilesTotal} ({FormatBytes(progress.BytesDownloaded)}) {deviceTimeStr}";
                        }
                        else if (progress.IsDownloading)
                        {
                            deviceTask.Description = deviceTotal > 0
                                ? $"[cyan]Device {deviceIndex}/{deviceTotal}[/] [yellow]Fetching history...[/]"
                                : "[cyan]Discovering devices...[/]";
                        }

                        // Bar 2: Total Aggregate Progress
                        // TotalFilesMatched is pre-scanned across every device before the first
                        // device's download starts (see VideoDownloadServiceAdapter.DownloadVideosAsync),
                        // so this reflects the true grand total from the start instead of only knowing
                        // about whichever device is currently in flight.
                        var aggregateTotal = progress.TotalFilesMatched;
                        if (aggregateTotal > 0)
                        {
                            totalTask.IsIndeterminate = false;
                            totalTask.MaxValue = aggregateTotal;
                            totalTask.Value = progress.TotalFilesCompleted + progress.FilesCompleted;
                            var aggregateCompleted = progress.TotalFilesCompleted + progress.FilesCompleted;
                            var globalElapsed = DateTime.Now - globalStartTime;
                            var globalTimeStr = $"{globalElapsed.Hours:D2}:{globalElapsed.Minutes:D2}:{globalElapsed.Seconds:D2}";
                            totalTask.Description = $"[cyan]Across All Devices[/]: {aggregateCompleted}/{aggregateTotal} ({FormatBytes(progress.TotalBytesDownloaded + progress.BytesDownloaded)}) {globalTimeStr}";
                        }

                        // Bar 3: Speed & Connections — percent reflects how saturated the configured
                        // concurrency is (active connections out of the configured max), not a fixed 100%.
                        var maxConnections = Math.Max(1, _forensicsConfig.MaxConcurrentDownloads);
                        speedTask.IsIndeterminate = false;
                        speedTask.MaxValue = maxConnections;
                        speedTask.Value = Math.Min(progress.ActiveConnections, maxConnections);
                        var speedInfo = $"{progress.CurrentSpeedMbps:F1} Mbps";
                        var connInfo = $"{progress.ActiveConnections}/{maxConnections} connection{(progress.ActiveConnections != 1 ? "s" : "")}";
                        speedTask.Description = $"[cyan]Transfer Rate[/]: {speedInfo} — {connInfo}";

                        foreach (var line in _downloadService.DrainActivityLog())
                        {
                            AnsiConsole.MarkupLine(line);
                        }

                        await Task.Delay(400, cancellationToken);
                    }

                    result = await downloadTask;

                    foreach (var line in _downloadService.DrainActivityLog())
                    {
                        AnsiConsole.MarkupLine(line);
                    }

                    deviceTask.StopTask();
                    totalTask.StopTask();
                    speedTask.StopTask();
                });

            return result;
        }

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024;
            const double mb = kb * 1024;
            if (bytes >= mb)
                return $"{bytes / mb:F1} MB";
            if (bytes >= kb)
                return $"{bytes / kb:F1} KB";
            return $"{bytes} bytes";
        }

        private async Task ShowManageAccountsMenu(CancellationToken ct)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("═══════════════════════════════════════════════════════════");
                Console.WriteLine("MANAGE ACCOUNTS");
                Console.WriteLine("═══════════════════════════════════════════════════════════");

                var accounts = await _providerAccountRepository.ListAsync(ct);
                var activeAccountId = _forensicsConfig.ActiveProviderAccountId;

                if (accounts.Count == 0)
                {
                    Console.WriteLine("\nNo provider accounts configured.");
                }
                else
                {
                    Console.WriteLine("\nConfigured Accounts:");
                    for (int i = 0; i < accounts.Count; i++)
                    {
                        var account = accounts[i];
                        var label = await FormatAccountLabelAsync(account, ct);
                        var isActive = account.Id == activeAccountId ? " [ACTIVE]" : "";
                        var lastAuth = account.LastSuccessfulAuthUtc.HasValue
                            ? account.LastSuccessfulAuthUtc.Value.ToString("yyyy-MM-dd HH:mm:ss")
                            : "Never";
                        Console.WriteLine($"  {i + 1}. {label} ({account.ProviderName}) - Linked: {account.LinkedUtc:yyyy-MM-dd HH:mm:ss}, Last Auth: {lastAuth}{isActive}");
                    }
                }

                Console.WriteLine("\nOptions:");
                Console.WriteLine("1. Select Active Account");
                Console.WriteLine("2. Add Account");
                Console.WriteLine("3. Remove Account");
                Console.WriteLine("4. Back to Main Menu");

                var choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                    case "select":
                        if (accounts.Count > 0)
                            await SelectActiveAccountAsync(accounts, ct);
                        else
                            Console.WriteLine("No accounts available to select.");
                        Console.ReadKey();
                        break;
                    case "2":
                    case "add":
                        await AddNewAccountAsync(ct);
                        break;
                    case "3":
                    case "remove":
                        if (accounts.Count > 0)
                            await RemoveAccountAsync(accounts, ct);
                        else
                            Console.WriteLine("No accounts available to remove.");
                        Console.ReadKey();
                        break;
                    case "4":
                    case "back":
                        return;
                    default:
                        Console.WriteLine("Invalid selection.");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private async Task SelectActiveAccountAsync(IReadOnlyList<ProviderAccount> accounts, CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("Select Active Account");
            Console.WriteLine("───────────────────────────────────────────────────────────");

            for (int i = 0; i < accounts.Count; i++)
            {
                var label = await FormatAccountLabelAsync(accounts[i], ct);
                Console.WriteLine($"{i + 1}. {label}");
            }

            Console.Write("Enter account number (or 0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out var selection) && selection > 0 && selection <= accounts.Count)
            {
                var selected = accounts[selection - 1];
                var selectedLabel = await FormatAccountLabelAsync(selected, ct);
                _forensicsConfig.ActiveProviderAccountId = selected.Id;
                await SaveConfiguration(ct);

                // Authenticate with the selected account
                if (await _authService.RestoreFromSavedCredentialsAsync(selected.Id, ct))
                {
                    AnsiConsole.MarkupLine($"[green]✓ Active account set to {selectedLabel} and authenticated[/]");
                    _logger.LogInformation("Active account changed to {AccountId} ({ProviderName}) with successful authentication", selected.Id, selected.ProviderName);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]✓ Active account set to {selectedLabel}, but authentication failed[/]");
                    AnsiConsole.MarkupLine("[yellow]Run the Download Videos menu option to re-authenticate with username and password[/]");
                    _logger.LogWarning("Active account changed to {AccountId} ({ProviderName}) but authentication restore failed", selected.Id, selected.ProviderName);
                }
            }
        }

        private async Task RemoveAccountAsync(IReadOnlyList<ProviderAccount> accounts, CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("Remove Account");
            Console.WriteLine("───────────────────────────────────────────────────────────");

            for (int i = 0; i < accounts.Count; i++)
            {
                var label = await FormatAccountLabelAsync(accounts[i], ct);
                Console.WriteLine($"{i + 1}. {label}");
            }

            Console.Write("Enter account number to remove (or 0 to cancel): ");
            if (int.TryParse(Console.ReadLine(), out var selection) && selection > 0 && selection <= accounts.Count)
            {
                var selected = accounts[selection - 1];
                var selectedLabel = await FormatAccountLabelAsync(selected, ct);
                if (AnsiConsole.Confirm($"Remove {selectedLabel} account?", false))
                {
                    await _providerAccountRepository.DeleteAsync(selected.Id, ct);

                    // If this was the active account, clear it
                    if (_forensicsConfig.ActiveProviderAccountId == selected.Id)
                    {
                        _forensicsConfig.ActiveProviderAccountId = null;
                        await SaveConfiguration(ct);
                    }

                    AnsiConsole.MarkupLine($"[green]✓ Account removed[/]");
                    _logger.LogInformation("Account removed: {AccountId} ({ProviderName})", selected.Id, selected.ProviderName);
                }
            }
        }

        private async Task AddNewAccountAsync(CancellationToken ct)
        {
            Console.Clear();
            Console.WriteLine("Add New Account");
            Console.WriteLine("───────────────────────────────────────────────────────────");

            Console.Write("Enter provider name (Ring/Wyze): ");
            var provider = Console.ReadLine();

            if (string.IsNullOrEmpty(provider))
            {
                Console.WriteLine("Provider name required.");
                Console.ReadKey();
                return;
            }

            // Re-use existing 2FA auth flow from AuthenticateWithTwoFactorAsync
            Console.WriteLine("Initiating authentication flow...");
            var result = await AuthenticateWithTwoFactorAsync(ct);

            if (result)
            {
                Console.WriteLine("Account added successfully.");
            }
            else
            {
                Console.WriteLine("Failed to add account.");
            }
            Console.ReadKey();
        }

        /// <summary>
        /// Prompts for a string with the default value pre-filled and editable in the input line
        /// itself (cursor positioned before it), rather than shown as a "(7):" hint the way
        /// Spectre.Console's TextPrompt/Ask do. The first accepted character typed clears the
        /// pre-filled default entirely (as if it were pre-selected) rather than inserting before
        /// it; Backspace/Delete/arrow keys before that fall back to normal in-place editing. Enter
        /// with no changes keeps the default. Only characters accepted by <paramref name="isAllowedChar"/>
        /// are inserted; all other keys (aside from the navigation/editing keys above) are ignored.
        /// </summary>
        private static string AskStringWithEditableDefault(string label, string defaultValue, Func<char, bool> isAllowedChar)
        {
            AnsiConsole.Markup(label + " ");
            Console.Out.Flush();

            var buffer = new System.Text.StringBuilder(defaultValue);
            var cursor = 0;
            var defaultConsumed = false;

            // Tracks where the terminal's cursor actually is (offset from the start of the field),
            // independent of the logical `cursor` value — the two only match right after Redraw()
            // repositions the terminal. Redraw() must rewind from this, not from the new logical
            // cursor, or it backspaces too far and eats into the label text.
            var termCursor = 0;

            void Redraw()
            {
                // Move to start of buffer, from wherever the terminal actually is
                if (termCursor > 0)
                    Console.Write(new string('\b', termCursor));

                // Clear from here to end of line
                Console.Write("\x1b[K");

                // Write the buffer
                Console.Write(buffer.ToString());

                // Position cursor at the correct spot within the buffer
                var trailing = buffer.Length - cursor;
                if (trailing > 0)
                    Console.Write(new string('\b', trailing));

                termCursor = cursor;
            }

            Console.Write(buffer.ToString());
            Console.Write(new string('\b', buffer.Length));

            while (true)
            {
                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    defaultConsumed = true;
                    if (cursor > 0)
                    {
                        buffer.Remove(cursor - 1, 1);
                        cursor--;
                        Redraw();
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.Delete)
                {
                    defaultConsumed = true;
                    if (cursor < buffer.Length)
                    {
                        buffer.Remove(cursor, 1);
                        Redraw();
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.LeftArrow)
                {
                    defaultConsumed = true;
                    if (cursor > 0)
                    {
                        cursor--;
                        termCursor--;
                        Console.Write("\b");
                    }
                    continue;
                }

                if (key.Key == ConsoleKey.RightArrow)
                {
                    defaultConsumed = true;
                    if (cursor < buffer.Length)
                    {
                        Console.Write(buffer[cursor]);
                        cursor++;
                        termCursor++;
                    }
                    continue;
                }

                if (isAllowedChar(key.KeyChar))
                {
                    if (!defaultConsumed)
                    {
                        buffer.Clear();
                        cursor = 0;
                        defaultConsumed = true;
                    }
                    buffer.Insert(cursor, key.KeyChar);
                    cursor++;
                    Redraw();
                }
            }

            return buffer.Length == 0 ? defaultValue : buffer.ToString();
        }

        private static int AskIntWithEditableDefault(string label, int defaultValue)
        {
            var result = AskStringWithEditableDefault(label, defaultValue.ToString(), char.IsDigit);
            return int.TryParse(result, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// Accepted date input formats, dash-separated: ISO year-first (yyyy-M-d, any digit
        /// padding) or US month-first (M-d-y, 2- or 4-digit year).
        private static readonly string[] DateInputFormats =
        {
            "yyyy-MM-dd", "yyyy-M-d", "yyyy-MM-d", "yyyy-M-dd",
            "M-d-yyyy", "MM-dd-yyyy", "M-dd-yyyy", "MM-d-yyyy",
            "M-d-yy", "MM-dd-yy", "M-dd-yy", "MM-d-yy"
        };

        /// <summary>
        /// Prompts for a date with the default pre-filled and editable, re-prompting until the
        /// input parses. Digits and '-' are the only characters accepted; see
        /// <see cref="DateInputFormats"/> for the accepted layouts.
        /// </summary>
        private static DateTime AskDateWithEditableDefault(string label, DateTime defaultValue)
        {
            while (true)
            {
                var input = AskStringWithEditableDefault(label, defaultValue.ToString("yyyy-MM-dd"), c => char.IsDigit(c) || c == '-');
                if (DateTime.TryParseExact(
                        input,
                        DateInputFormats,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var parsed))
                {
                    return parsed;
                }

                AnsiConsole.MarkupLine("[red]Invalid date - use yyyy-MM-dd or M-d-yy.[/]");
            }
        }
    }
}
