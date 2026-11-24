using AudioLab.Web.Components;
using Microsoft.AspNetCore.Components;

namespace AudioLab.Web.Tests;

/// <summary>The rules of the menu and of a disabled control that the markup cannot show: the order of commands and the click guard.</summary>
public sealed class MenuTests
{
    private static readonly MenuItem _rename = new("rename", "Rename", "F2");

    private static readonly MenuItem _delete = new("delete", "Delete", "Del", Destructive: true);

    private static readonly MenuItem _duplicate = new("duplicate", "Duplicate", "Ctrl+D");

    private static readonly MenuItem _remove = new("remove", "Remove", "Shift+Del", Destructive: true);

    /// <summary>Destructive commands come last, whatever order the caller listed them in.</summary>
    [Fact]
    public void DestructiveCommandsComeLast()
    {
        string[] ids = [.. Menu.Order([_delete, _rename, _remove, _duplicate]).Select(item => item.Id)];

        Assert.Equal(["rename", "duplicate", "delete", "remove"], ids);
    }

    /// <summary>The caller's order holds inside each group, so a menu reads as it was written.</summary>
    [Fact]
    public void EachGroupKeepsTheCallersOrder()
    {
        string[] ids = [.. Menu.Order([_duplicate, _rename]).Select(item => item.Id)];

        Assert.Equal(["duplicate", "rename"], ids);
    }

    /// <summary>A menu with no destructive command is left as it is.</summary>
    [Fact]
    public void AMenuWithoutDestructiveCommandsIsUnchanged()
    {
        Assert.Equal(["rename", "duplicate"], Menu.Order([_rename, _duplicate]).Select(item => item.Id));
    }

    /// <summary>A disabled control raises nothing.</summary>
    [Fact]
    public async Task ADisabledControlRaisesNothing()
    {
        int raised = 0;
        EventCallback callback = EventCallback.Factory.Create(this, () => raised++);

        await Activation.RunAsync(true, callback);

        Assert.Equal(0, raised);
    }

    /// <summary>An enabled control raises its callback once.</summary>
    [Fact]
    public async Task AnEnabledControlRaisesItsCallbackOnce()
    {
        int raised = 0;
        EventCallback callback = EventCallback.Factory.Create(this, () => raised++);

        await Activation.RunAsync(false, callback);

        Assert.Equal(1, raised);
    }
}
