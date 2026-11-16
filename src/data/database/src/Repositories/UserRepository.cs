using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Repository implementation for User entities. Reads run through <see cref="IUnitOfWork.ReadAsync{T}"/>; writes run
    /// through <see cref="IUnitOfWork.ExecuteAsync{T}"/>, so inside an open unit of work they join its transaction.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UserRepository> _logger;

        /// <summary>Initializes a new instance of the UserRepository.</summary>
        public UserRepository(IUnitOfWork unitOfWork, ILogger<UserRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a user by ID.</summary>
        public Task<User?> GetAsync(Guid userId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().Users.FirstOrDefaultAsync(u => u.Id == userId, ct), ct);
        }

        /// <summary>Gets a user by provider key.</summary>
        public Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().Users.FirstOrDefaultAsync(u => u.ProviderUserKey == providerUserKey, ct), ct);
        }

        /// <summary>Lists all users.</summary>
        public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Users.ToListAsync(ct), ct);
        }

        /// <summary>Adds a new user.</summary>
        public async Task AddAsync(User user, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.Users.Add(user);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("User added: {DisplayName}", MaskForLog(SanitizeForLog(user.DisplayName)));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding user: {DisplayName}: {Message} ({ErrorType})", MaskForLog(SanitizeForLog(user.DisplayName)), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing user.</summary>
        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.Users.Update(user);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("User updated: {DisplayName}", MaskForLog(SanitizeForLog(user.DisplayName)));
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating user: {DisplayName}: {Message} ({ErrorType})", MaskForLog(SanitizeForLog(user.DisplayName)), ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes a user.</summary>
        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            try
            {
                bool deleted = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    User? user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                    if (user == null)
                    {
                        return false;
                    }

                    _ = db.Users.Remove(user);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted)
                {
                    _logger.LogDebug("User deleted: {UserId}", userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting user: {UserId}: {Message} ({ErrorType})", userId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }

        /// <summary>Masks a string for logging to prevent exposure of sensitive information like email addresses.</summary>
        private static string MaskForLog(string value)
        {
            return string.IsNullOrEmpty(value) || value.Length <= 2 ? "***" : $"{value[0]}***{value[^1]}";
        }
    }
}
