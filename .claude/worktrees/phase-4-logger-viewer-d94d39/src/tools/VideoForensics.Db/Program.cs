using VideoForensics.Db.Commands;

// VideoForensics.Db: unified tool for database operations.
// Supports subcommands: setup, repair.
//
// Usage:
//   VideoForensics.Db <setup|repair> [options]
//
// Examples:
//   VideoForensics.Db setup --db-path /path/to/db.sqlite
//   VideoForensics.Db repair --apply
//   VideoForensics.Db setup --help
//   VideoForensics.Db repair --help

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

string command = args[0].ToLowerInvariant();
string[] remainingArgs = args.Skip(1).ToArray();

int result = command switch
{
    "setup" => await SetupCommand.ExecuteAsync(remainingArgs),
    "repair" => await RepairCommand.ExecuteAsync(remainingArgs),
    "help" or "-h" or "--help" => PrintHelpAndReturn(),
    _ => PrintUnknownCommandAndReturn(command)
};

return result;

static int PrintHelpAndReturn()
{
    PrintUsage();
    return 0;
}

static int PrintUnknownCommandAndReturn(string command)
{
    Console.Error.WriteLine($"Unknown command: '{command}'");
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        VideoForensics.Db - database management tool.

        Usage:
          VideoForensics.Db <command> [options]

        Commands:
          setup       Create/migrate the VideoForensics database without starting the server
          repair      Repair the VideoForensics database by removing duplicates and orphaned records
          help        Show this message

        Examples:
          VideoForensics.Db setup
          VideoForensics.Db setup --db-path /path/to/videoforensics.db
          VideoForensics.Db setup --data-root /path/to/data
          VideoForensics.Db repair --apply
          VideoForensics.Db repair

        For command-specific help:
          VideoForensics.Db <command> --help
        """);
}
