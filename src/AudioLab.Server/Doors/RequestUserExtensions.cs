using AudioLab.Core.Model;

namespace AudioLab.Server.Doors;

/// <summary>Carries the user a door resolved from the filter to the route that handles the request.</summary>
public static class RequestUserExtensions
{
    private const string ItemKey = "AudioLab.User";

    /// <summary>Records the user of the request; only <see cref="DoorFilter"/> calls this.</summary>
    public static void SetUser(this HttpContext context, UserId user)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Items[ItemKey] = user;
    }

    /// <summary>The user the door resolved for this request.</summary>
    /// <exception cref="InvalidOperationException">The route is not behind the door filter, so the request has no user.</exception>
    public static UserId RequireUser(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue(ItemKey, out object? value) && value is UserId user
            ? user
            : throw new InvalidOperationException("The request has no user: the route is not behind the door filter.");
    }
}
