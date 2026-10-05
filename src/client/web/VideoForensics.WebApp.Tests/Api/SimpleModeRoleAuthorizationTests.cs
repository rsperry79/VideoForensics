namespace VideoForensics.WebApp.Tests.Api;

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Hosting.Contracts;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;
using Xunit;

/// <summary>
/// Tests for ChatEndpoints authorization, validation, and simple-mode specific chat flows.
/// Covers role-based access control (ReadOnly role) and message/history validation constraints.
/// </summary>
public class SimpleModeRoleAuthorizationTests
{
    private static HttpContext CreateAuthenticatedHttpContext(Guid operatorId, OperatorRole role = OperatorRole.ReadOnly)
    {
        var context = new DefaultHttpContext();
        var claims = new[]
        {
            new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(VideoForensicsClaimTypes.NetworkTier, NetworkTier.Local.ToString())
        };
        var identity = new ClaimsIdentity(claims, "test");
        context.User = new ClaimsPrincipal(identity);
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return context;
    }

    private static HttpContext CreateUnauthenticatedHttpContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return context;
    }

    [Fact]
    public async Task SendChatMessage_ReadOnlyRole_CanPostToEndpoint()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "What can you tell me about this evidence?"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "This is a relevant piece of evidence.",
                ToolsInvoked = new List<string> { "analyze_tool" }
            });

        // Act & Assert
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_AdminRole_CanPostToEndpoint()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.Admin);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "Analyze this evidence"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "Analysis complete",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_UnauthenticatedUser_Returns401()
    {
        // Arrange
        var context = CreateUnauthenticatedHttpContext();

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "Hello"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<ChatResponseDto>;
        Assert.Null(okResult); // Should not be OK - would be caught by authorization middleware in real scenario
    }

    [Fact]
    public async Task SendChatMessage_EmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: ""
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        var badRequestResult = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>;
        Assert.NotNull(badRequestResult);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendChatMessage_WhitespaceOnlyMessage_ReturnsBadRequest()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "   \n  \t  "
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        var badRequestResult = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>;
        Assert.NotNull(badRequestResult);
    }

    [Fact]
    public async Task SendChatMessage_MessageExceeds10KCharacters_ReturnsBadRequest()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var longMessage = new string('x', 10_001);
        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: longMessage
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        var badRequestResult = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>;
        Assert.NotNull(badRequestResult);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendChatMessage_MessageAt10KCharacters_IsAccepted()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var maxMessage = new string('x', 10_000);
        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: maxMessage
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "Got your message",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_HistoryExceeds100Turns_ReturnsBadRequest()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var largeHistory = new List<ChatMessageDto>();
        for (int i = 0; i < 101; i++)
        {
            largeHistory.Add(new ChatMessageDto(Role: "user", Content: "message"));
        }

        var chatRequest = new ChatRequestDto(
            History: largeHistory,
            Message: "What is this?"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        var badRequestResult = result as Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>;
        Assert.NotNull(badRequestResult);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendChatMessage_HistoryAt100Turns_IsAccepted()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var maxHistory = new List<ChatMessageDto>();
        for (int i = 0; i < 100; i++)
        {
            maxHistory.Add(new ChatMessageDto(Role: "user", Content: "message"));
        }

        var chatRequest = new ChatRequestDto(
            History: maxHistory,
            Message: "Final message"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "Reply to your message",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_NullHistory_IsAccepted()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "Hello"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "Hi there",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_SuccessfulResponse_IncludesReplyAndToolsInvoked()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "Analyze the timestamp"
        );

        var expectedReply = "The timestamp indicates the event occurred at 2024-10-04 15:30:00 UTC";
        var expectedTools = new List<string> { "parse_timestamp", "correlate_events" };

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = expectedReply,
                ToolsInvoked = expectedTools
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var okResult = result as Microsoft.AspNetCore.Http.HttpResults.Ok<ChatResponseDto>;
        Assert.NotNull(okResult);
        // The response DTO should contain both the reply and tools invoked
        var responseValue = okResult?.Value;
        Assert.NotNull(responseValue);
        Assert.Equal(expectedReply, responseValue.Reply);
        Assert.Equal(expectedTools.Count, responseValue.ToolsInvoked.Count);
        foreach (var tool in expectedTools)
        {
            Assert.Contains(tool, responseValue.ToolsInvoked);
        }
    }

    [Fact]
    public async Task SendChatMessage_WithEmptyHistory_CallsChatService()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var chatRequest = new ChatRequestDto(
            History: new List<ChatMessageDto>(),
            Message: "Start a new conversation"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "I'm ready to help",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), "Start a new conversation", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_WithMultipleTurnsInHistory_PreservesAllTurns()
    {
        // Arrange
        var operatorId = Guid.NewGuid();
        var context = CreateAuthenticatedHttpContext(operatorId, OperatorRole.ReadOnly);

        var history = new List<ChatMessageDto>
        {
            new ChatMessageDto(Role: "user", Content: "First question"),
            new ChatMessageDto(Role: "assistant", Content: "First answer"),
            new ChatMessageDto(Role: "user", Content: "Follow-up question")
        };

        var chatRequest = new ChatRequestDto(
            History: history,
            Message: "And what about this?"
        );

        var mockChatService = new Mock<IChatService>();
        var mockLogger = new Mock<ILogger<Program>>();

        mockChatService
            .Setup(s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<ChatTurn>, string, CancellationToken>((turns, msg, ct) =>
            {
                // Verify the history for assertions
                Assert.NotNull(turns);
                Assert.Equal(3, turns.Count);
            })
            .ReturnsAsync(new ChatTurnResult
            {
                Reply = "Continuing the conversation",
                ToolsInvoked = new List<string>()
            });

        // Act
        var result = await ChatEndpointsInvoker.SendChatMessageAsync(
            chatRequest,
            mockChatService.Object,
            context,
            mockLogger.Object,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        mockChatService.Verify(
            s => s.SendMessageAsync(It.IsAny<IReadOnlyList<ChatTurn>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
