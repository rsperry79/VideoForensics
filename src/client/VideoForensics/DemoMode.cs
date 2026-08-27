using Spectre.Console;

namespace VideoForensics;

public static class DemoMode
{
    public static void RunDemo()
    {
        AnsiConsole.MarkupLine("[bold green]═══════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold green]    VideoForensics - Evidence Analysis[/]");
        AnsiConsole.MarkupLine("[bold green]  DV Victim Protection & Tamper Detection[/]");
        AnsiConsole.MarkupLine("[bold green]═══════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("");

        ShowEvidence();
        AnsiConsole.MarkupLine("");
        ShowReports();
        AnsiConsole.MarkupLine("");
        ShowSignalAnomalies();
        AnsiConsole.MarkupLine("");
        ShowAccessControl();
        AnsiConsole.MarkupLine("");
        ShowRingVideosIntegration();
    }

    static void ShowEvidence()
    {
        AnsiConsole.MarkupLine("[bold cyan]Forensic Evidence[/]");
        var table = new Table();
        table.AddColumn("Evidence ID");
        table.AddColumn("Device ID");
        table.AddColumn("Format");
        table.AddColumn("Status");
        table.Border = TableBorder.Rounded;

        table.AddRow("e0b11ac2", "a1b2c3d4", "MP4", "[green]✓ Verified[/]");
        table.AddRow("cec6d6ed", "e5f6g7h8", "MP4", "[yellow]⚠ Not verified[/]");
        table.AddRow("7310f57a", "i9j0k1l2", "JPEG", "[red]Integrity failed[/]");

        AnsiConsole.Write(table);
    }

    static void ShowReports()
    {
        AnsiConsole.MarkupLine("[bold cyan]GENERATED REPORTS[/]");

        var panel1 = new Panel("[green]✓[/] [bold]Forensic Analysis Report[/]\nRPT-2026-001 - RF Jamming Detected\n[yellow]Status: CRITICAL[/]")
        {
            Header = new PanelHeader("[bold green]Analysis Report[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel1);

        var panel2 = new Panel("[green]✓[/] [bold]Chain of Custody Report[/]\nRPT-2026-002 - Integrity Verified\n[green]Status: VERIFIED[/]")
        {
            Header = new PanelHeader("[bold green]Custody Report[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel2);

        var panel3 = new Panel("[red]✗[/] [bold]Access Control Alert[/]\n3 suspicious access attempts\n[red]Status: CRITICAL[/]")
        {
            Header = new PanelHeader("[bold red]Access Report[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel3);
    }

    static void ShowSignalAnomalies()
    {
        AnsiConsole.MarkupLine("[bold cyan]Signal Strength Analysis[/]");
        AnsiConsole.MarkupLine("[green]████████████[/] 65% - Normal range");
        AnsiConsole.MarkupLine("[orange3]██████[/] 32% - Degraded signal");
        AnsiConsole.MarkupLine("[red]███[/] 15% - Critical (Jamming detected)");

        AnsiConsole.MarkupLine("\n[yellow]Anomalies Detected:[/]");
        AnsiConsole.MarkupLine("• [red]RF Jamming Incident[/] - Front door camera");
        AnsiConsole.MarkupLine("• [yellow]Signal Drop[/] - Backyard camera (3 min duration)");
        AnsiConsole.MarkupLine("• [orange3]Sustained Degradation[/] - Side camera (25% loss)");
    }

    static void ShowRingVideosIntegration()
    {
        AnsiConsole.MarkupLine("[bold cyan]RING.VIDEOS INTEGRATION[/]");
        var panel = new Panel("[green]✓ Chain of Custody[/] - Managed by Ring.Videos\n[green]✓ Video Downloads[/] - Ring device management\n[green]✓ Device Authentication[/] - Secure access\n[green]✓ Evidence Storage[/] - Forensic preservation")
        {
            Header = new PanelHeader("[bold green]Linked Systems[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("\n[yellow]Launch Ring.Videos from VideoForensics menu to manage:[/]");
        AnsiConsole.MarkupLine("  • Chain of custody tracking with cryptographic verification");
        AnsiConsole.MarkupLine("  • Evidence video downloads and processing");
        AnsiConsole.MarkupLine("  • Device authentication and authorization");
    }

    static void ShowAccessControl()
    {
        AnsiConsole.MarkupLine("[bold cyan]Evidence Access Monitoring[/]");
        AnsiConsole.MarkupLine("[red]High-Risk Alerts: 2[/]");

        var table = new Table();
        table.AddColumn("Actor");
        table.AddColumn("Action");
        table.AddColumn("Entity Type");
        table.AddColumn("Timestamp");
        table.Border = TableBorder.Rounded;

        table.AddRow("Officer Smith", "View", "Evidence", "2026-08-20 10:15");
        table.AddRow("Detective Johnson", "Export", "Evidence", "2026-08-20 12:30");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[yellow]Recommendation:[/] Review access logs for potential evidence tampering");
    }
}
