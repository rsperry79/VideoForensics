using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for User entities.</summary>
    public class UserRepository : IUserRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<UserRepository> _logger;

        /// <summary>Initializes a new instance of the UserRepository.</summary>
        public UserRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<UserRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a user by ID.</summary>
        public async Task<User?> GetAsync(Guid userId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        }

        /// <summary>Gets a user by provider key.</summary>
        public async Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Users.FirstOrDefaultAsync(u => u.ProviderUserKey == providerUserKey, ct);
        }

        /// <summary>Lists all users.</summary>
        public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Users.ToListAsync(ct);
        }

        /// <summary>Adds a new user.</summary>
        public async Task AddAsync(User user, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Users.Add(user);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("User added: {UserId} ({DisplayName})", user.Id, MaskForLog(SanitizeForLog(user.DisplayName)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user: {DisplayName}", MaskForLog(SanitizeForLog(user.DisplayName)));
                throw;
            }
        }

        /// <summary>Updates an existing user.</summary>
        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Users.Update(user);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("User updated: {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user: {UserId}", user.Id);
                throw;
            }
        }

        /// <summary>Deletes a user.</summary>
        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                if (user != null)
                {
                    _ = db.Users.Remove(user);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("User deleted: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId}", userId);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');

        /// <summary>Masks a string for logging to prevent exposure of sensitive information like email addresses.</summary>
        private static string MaskForLog(string value) =>
            string.IsNullOrEmpty(value) || value.Length <= 2 ? "***" : $"{value[0]}***{value[^1]}";
    }
}
