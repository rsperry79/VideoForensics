using System.Data.Common;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>The append path reads the chain tail on every write, so that query must be an index lookup.</summary>
    public class ActionLogTailTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task Latest_PlansAsIndexLookup_NotATableScanWithSort()
        {
            await using var db = _fixture.Factory.CreateDbContext();
            // ToQueryString prefixes a ".param set" line and emits LIMIT @p; keep the statement and bind the limit to a literal.
            string generated = ActionLogTail.Latest(db).Take(1).ToQueryString();
            string sql = generated[generated.IndexOf("SELECT", StringComparison.Ordinal)..].Replace("LIMIT @p", "LIMIT 1", StringComparison.Ordinal);

            DbConnection connection = db.Database.GetDbConnection();
            await connection.OpenAsync();
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = "EXPLAIN QUERY PLAN " + sql;
            var plan = new List<string>();
            await using DbDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(3));
            }

            string text = string.Join(Environment.NewLine, plan);
            Assert.Contains("IX_ActionLogEntries_TimestampUtc", text, StringComparison.Ordinal);
            Assert.DoesNotContain("TEMP B-TREE", text, StringComparison.Ordinal);
        }
    }
}
