using Microsoft.AspNetCore.Identity;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.DbRepair.Contracts;
using Xunit;

namespace VideoForensics.DbRepair.Tests;

public class SuperAdminRecoveryTests
{
    private readonly Mock<IOperatorRepository> _operatorRepo = new();
    private readonly Mock<IAppSettingRepository> _appSettingRepo = new();
    private readonly Mock<IPasswordPrompt> _passwordPrompt = new();
    private readonly Mock<ISecurityAuditLogRepository> _auditRepo = new();
    private readonly SuperAdminRecovery _recovery;

    public SuperAdminRecoveryTests()
    {
        _recovery = new SuperAdminRecovery(_operatorRepo.Object, _appSettingRepo.Object, _passwordPrompt.Object, _auditRepo.Object);
    }

    private static Operator CreateTestOperator(string username = "test", OperatorRole role = OperatorRole.SuperAdmin, bool isPrimary = false)
    {
        return new Operator
        {
            Id = Guid.NewGuid(),
            Username = username,
            DisplayName = username,
            FirstName = username,
            LastName = "User",
            Email = $"{username}@localhost.invalid",
            Role = role,
            IsPrimarySuperAdmin = isPrimary
        };
    }

    [Fact]
    public async Task SelectTarget_WithUsername_ReturnsOperatorIfExists()
    {
        // Arrange
        var target = CreateTestOperator("admin");
        _operatorRepo.Setup(r => r.GetByUsernameAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        // Act
        var result = await _recovery.SelectTargetAsync("admin", CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Id, result.Id);
    }

    [Fact]
    public async Task SelectTarget_WithUsername_ThrowsIfOperatorNotExists()
    {
        // Arrange
        _operatorRepo.Setup(r => r.GetByUsernameAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Operator?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recovery.SelectTargetAsync("admin", CancellationToken.None));
    }

    [Fact]
    public async Task SelectTarget_WithUsername_ThrowsIfNotSuperAdmin()
    {
        // Arrange
        var op = CreateTestOperator("user", OperatorRole.Admin);
        _operatorRepo.Setup(r => r.GetByUsernameAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(op);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recovery.SelectTargetAsync("user", CancellationToken.None));
    }

    [Fact]
    public async Task SelectTarget_WithoutUsername_ReturnsPrimarySuperAdmin()
    {
        // Arrange
        var primary = CreateTestOperator("primary", isPrimary: true);
        _operatorRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { primary });

        // Act
        var result = await _recovery.SelectTargetAsync(null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(primary.Id, result.Id);
    }

    [Fact]
    public async Task SelectTarget_WithoutUsername_ReturnsSingleSuperAdminIfNoPrimary()
    {
        // Arrange
        var superAdmin = CreateTestOperator("super");
        _operatorRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { superAdmin });

        // Act
        var result = await _recovery.SelectTargetAsync(null, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(superAdmin.Id, result.Id);
    }

    [Fact]
    public async Task SelectTarget_WithoutUsername_ThrowsIfMultipleSuperAdmins()
    {
        // Arrange
        var super1 = CreateTestOperator("super1");
        var super2 = CreateTestOperator("super2");
        _operatorRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { super1, super2 });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recovery.SelectTargetAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task SelectTarget_WithoutUsername_ThrowsIfNoSuperAdmin()
    {
        // Arrange
        var op = CreateTestOperator("user", OperatorRole.Admin);
        _operatorRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { op });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recovery.SelectTargetAsync(null, CancellationToken.None));
    }

    [Fact]
    public void ValidatePassword_TooShort_Throws()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _recovery.ValidatePassword("short"));
    }

    [Fact]
    public void ValidatePassword_ValidLength_Succeeds()
    {
        // Act - should not throw
        _recovery.ValidatePassword("ValidPassword123!");
    }

    [Fact]
    public async Task PromptPassword_Successful()
    {
        // Arrange
        _passwordPrompt.Setup(p => p.PromptPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.PromptConfirmationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");

        // Act
        var pwd = await _recovery.PromptPasswordAsync(CancellationToken.None);

        // Assert
        Assert.Equal("NewPassword123!", pwd);
    }

    [Fact]
    public async Task PromptPassword_MismatchedConfirmation_Throws()
    {
        // Arrange
        _passwordPrompt.Setup(p => p.PromptPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Password123!");
        _passwordPrompt.Setup(p => p.PromptConfirmationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("DifferentPassword!");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _recovery.PromptPasswordAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ResetPassword_HashesWithPasswordHasher()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var op = CreateTestOperator();
        op.Id = operatorId;
        string newPassword = "NewPassword123!";

        // Act
        await _recovery.ResetPasswordAsync(op, newPassword, CancellationToken.None);

        // Assert
        // Verify SetPasswordAsync was called with a hash and mustChangePassword=false
        _operatorRepo.Verify(r => r.SetPasswordAsync(
            operatorId,
            It.IsAny<string>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ClearsLockout()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var op = CreateTestOperator();
        op.Id = operatorId;
        op.FailedLoginAttemptCount = 5;
        op.LockedOutUntilUtc = DateTime.UtcNow.AddHours(1);

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _operatorRepo.Verify(r => r.UnlockAsync(operatorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_EnablesAuthPasswordIfKeyExists()
    {
        // Arrange
        var op = CreateTestOperator();
        _appSettingRepo.Setup(r => r.GetAsync("AuthPasswordEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync("False");

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _appSettingRepo.Verify(r => r.SetAsync("AuthPasswordEnabled", "True", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_DoesNotWriteSettingWhenKeyAbsent()
    {
        // Arrange
        var op = CreateTestOperator();
        _appSettingRepo.Setup(r => r.GetAsync("AuthPasswordEnabled", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _appSettingRepo.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_LogsAuditEvent()
    {
        // Arrange
        var op = CreateTestOperator();
        var operatorId = op.Id;

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _auditRepo.Verify(r => r.AppendAsync(
            It.Is<SecurityAuditLogEntry>(e =>
                e.EventType == SecurityAuditEventTypes.SuperAdminPasswordReset &&
                e.OperatorId == operatorId &&
                e.SourceIp == "local-console" &&
                e.IsUrgent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_ReactivatesDeactivatedAccount()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var op = CreateTestOperator();
        op.Id = operatorId;
        op.Active = false;  // Account is deactivated

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _operatorRepo.Verify(r => r.ReactivateAsync(operatorId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPassword_DoesNotReactivateIfAlreadyActive()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var op = CreateTestOperator();
        op.Id = operatorId;
        op.Active = true;  // Already active

        // Act
        await _recovery.ResetPasswordAsync(op, "NewPassword123!", CancellationToken.None);

        // Assert
        _operatorRepo.Verify(r => r.ReactivateAsync(operatorId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_FullFlow_WithConfirmation()
    {
        // Arrange
        var target = CreateTestOperator("admin");
        _operatorRepo.Setup(r => r.GetByUsernameAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _passwordPrompt.Setup(p => p.PromptPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.PromptConfirmationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.AskConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _recovery.ExecuteAsync("admin", skipConfirmation: false, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(target.Username, result.Username);
        _operatorRepo.Verify(r => r.SetPasswordAsync(It.IsAny<Guid>(), It.IsAny<string>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_SkipConfirmation()
    {
        // Arrange
        var target = CreateTestOperator("admin");
        _operatorRepo.Setup(r => r.GetByUsernameAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _passwordPrompt.Setup(p => p.PromptPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.PromptConfirmationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");

        // Act
        var result = await _recovery.ExecuteAsync("admin", skipConfirmation: true, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _passwordPrompt.Verify(p => p.AskConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_DeclinedConfirmation_CancelsOperation()
    {
        // Arrange
        var target = CreateTestOperator("admin");
        _operatorRepo.Setup(r => r.GetByUsernameAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);

        _passwordPrompt.Setup(p => p.PromptPasswordAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.PromptConfirmationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NewPassword123!");
        _passwordPrompt.Setup(p => p.AskConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _recovery.ExecuteAsync("admin", skipConfirmation: false, CancellationToken.None));
    }
}
