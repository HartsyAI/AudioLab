using AudioLab.Web.Components;

namespace AudioLab.Web.Tests;

/// <summary>What a slider sends: keys pressed while a send is on its way make one more send, and a held value goes once it is drawn or after a moment.</summary>
public sealed class ValueSenderTests
{
    /// <summary>Keys pressed while a send is on its way fold into one send after it, so a held key sends one value per answer.</summary>
    [Fact]
    public async Task KeysPressedWhileASendIsOnItsWayFoldIntoOneMoreSend()
    {
        TaskCompletionSource answer = new();
        List<double> sent = [];
        ValueSender sender = new(
            async value =>
            {
                sent.Add(value);
                if (sent.Count == 1)
                {
                    await answer.Task;
                }
            },
            () => { });

        Task first = sender.SendAsync(0.2);
        Task second = sender.SendAsync(0.3);
        Task third = sender.SendAsync(0.4);
        answer.SetResult();
        await Task.WhenAll(first, second, third);

        Assert.Equal(new[] { 0.2, 0.4 }, sent);
        sender.Dispose();
    }

    /// <summary>A held value is shown until it is drawn, and it goes after a moment when nothing has come back.</summary>
    [Fact]
    public async Task AHeldValueGoesAfterAMomentWhenNothingComesBack()
    {
        int changes = 0;
        ValueSender sender = new(_ => Task.CompletedTask, () => changes++);

        Task sending = sender.SendAsync(0.5);

        Assert.Equal(0.5, sender.Held);
        await sending;
        Assert.Null(sender.Held);
        Assert.Equal(2, changes);
        sender.Dispose();
    }

    /// <summary>The held value goes as soon as the drawn value is the one it holds, and not before.</summary>
    [Fact]
    public async Task TheHeldValueGoesWhenTheDrawnValueIsTheOneItHolds()
    {
        ValueSender sender = new(_ => Task.CompletedTask, () => { });

        Task sending = sender.SendAsync(0.5);
        sender.Arrived(0.25);
        Assert.Equal(0.5, sender.Held);
        sender.Arrived(0.5);
        Assert.Null(sender.Held);
        await sending;
        sender.Dispose();
    }
}
