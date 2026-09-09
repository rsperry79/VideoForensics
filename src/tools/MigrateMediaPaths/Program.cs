using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddVideoForensicsSqlite();

var sp = services.BuildServiceProvider();
var dbFactory = sp.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
var logger = sp.GetRequiredService<ILogger<Program>>();

using var db = dbFactory.CreateDbContext();

var oldPrefix = "C:\\Users\\richa\\OneDrive\\Videos\\VideoForensics";
var newPrefix = "C:\\ProgramData\\VideoForensics\\media";

// Find media items with old paths
var itemsToUpdate = await db.MediaItems
    .Where(m => m.FilePath.StartsWith(oldPrefix))
    .ToListAsync();

logger.LogInformation("Found {Count} media items to update", itemsToUpdate.Count);

int updated = 0;
foreach (var item in itemsToUpdate)
{
    var oldPath = item.FilePath;
    var relativePath = oldPath.Substring(oldPrefix.Length).TrimStart('\\');
    item.FilePath = System.IO.Path.Combine(newPrefix, relativePath);
    updated++;

    if (updated % 1000 == 0)
    {
        logger.LogInformation("Updated {Count} items...", updated);
    }
}

if (itemsToUpdate.Count > 0)
{
    await db.SaveChangesAsync();
    logger.LogInformation("Successfully updated {Count} media item paths", itemsToUpdate.Count);
}
else
{
    logger.LogInformation("No media items with OneDrive paths found");
}
