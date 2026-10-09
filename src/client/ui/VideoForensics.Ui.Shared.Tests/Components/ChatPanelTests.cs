namespace VideoForensics.Ui.Shared.Tests.Components;

using System.Runtime.CompilerServices;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Ui.Shared.Components;
using VideoForensics.Ui.Shared.Resources;
using Xunit;

public class ChatPanelTests : BunitContext
{
    /// <summary>Fake chat service whose stream is scripted per call and which records each call's arguments.</summary>
    private sealed class FakeChatService : IChatService
    {
        public Queue<Func<IAsyncEnumerable<ChatStreamEvent>>> Scripts { get; } = new();
        public List<(List<ChatTurn> History, string Message)> Calls { get; } = [];

        public Task<ChatTurnResult> SendMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
            => throw new NotSupportedException();

        public IAsyncEnumerable<ChatStreamEvent> StreamMessageAsync(IReadOnlyList<ChatTurn> history, string message, CancellationToken ct)
        {
            Calls.Add((history.Select(t => new ChatTurn { Role = t.Role, Content = t.Content }).ToList(), message));
            return Scripts.Dequeue()();
        }
    }

    private readonly FakeChatService _chat = new();
    private readonly IStringLocalizer<SharedResources> _localizer = TestLocalizer.Create();

    public ChatPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton<IChatService>(_chat);
        Services.AddSingleton(_localizer);
    }

    private static async IAsyncEnumerable<ChatStreamEvent> Stream(
        Task? gateAfterFirst, string[] deltas, ChatTurnResult? result, [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < deltas.Length; i++)
        {
            yield return new ChatTextDelta(deltas[i]);
            if (i == 0 && gateAfterFirst is not null)
            {
                await gateAfterFirst;
            }
        }

        if (result is not null)
        {
            yield return new ChatTurnCompleted(result);
        }

        await Task.CompletedTask;
    }

    /// <summary>Types the text and presses Enter; returns the (possibly still running) send task.</summary>
    private Task Send(IRenderedComponent<ChatPanel> cut, string text)
    {
        cut.Find("textarea").Input(text);
        return cut.Find("textarea").TriggerEventAsync("onkeydown", new KeyboardEventArgs { Key = "Enter" });
    }

    [Fact]
    public async Task ChatPanel_StreamingDeltas_RenderWhileStreamOpen()
    {
        var gate = new TaskCompletionSource();
        _chat.Scripts.Enqueue(() => Stream(gate.Task, ["Hel", "lo"], new ChatTurnResult { Reply = "Hello" }));
        var cut = Render<ChatPanel>();

        var send = Send(cut, "Hi");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Hel", cut.Markup);
            Assert.DoesNotContain("Hello", cut.Markup);
            Assert.DoesNotContain(_localizer["ChatThinking"].Value, cut.Markup);
        });

        gate.SetResult();
        await send;

        cut.WaitForAssertion(() =>
        {
            var assistant = cut.FindAll(".assistant-message .chat-message-content");
            Assert.Single(assistant);
            Assert.Equal("Hello", assistant[0].TextContent.Trim());
        });
    }

    [Fact]
    public async Task ChatPanel_Completion_FinalReplyReplacesStreamedText()
    {
        _chat.Scripts.Enqueue(() => Stream(null, ["par", "tial"], new ChatTurnResult { Reply = "Final answer" }));
        var cut = Render<ChatPanel>();

        await Send(cut, "Hi");

        cut.WaitForAssertion(() =>
        {
            var assistant = cut.FindAll(".assistant-message .chat-message-content");
            Assert.Single(assistant);
            Assert.Contains("Final answer", assistant[0].TextContent);
            Assert.DoesNotContain("partial", cut.Markup);
            Assert.DoesNotContain(_localizer["ChatThinking"].Value, cut.Markup);
        });
    }

    [Fact]
    public async Task ChatPanel_SecondSend_DoesNotIncludeNewUserMessageInHistory()
    {
        _chat.Scripts.Enqueue(() => Stream(null, ["x"], new ChatTurnResult { Reply = "First reply" }));
        _chat.Scripts.Enqueue(() => Stream(null, ["y"], new ChatTurnResult { Reply = "Second reply" }));
        var cut = Render<ChatPanel>();

        await Send(cut, "Earlier");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".assistant-message")));
        await Send(cut, "Hi");

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".assistant-message").Count));

        Assert.Equal(2, _chat.Calls.Count);
        var (history, message) = _chat.Calls[1];
        Assert.Equal("Hi", message);
        Assert.Equal(["Earlier", "First reply"], history.Select(t => t.Content).ToArray());
        Assert.DoesNotContain(history, t => t.Content == "Hi");

        var userBubbles = cut.FindAll(".user-message .chat-message-content");
        Assert.Equal(1, userBubbles.Count(b => b.TextContent.Trim() == "Hi"));
    }
}
