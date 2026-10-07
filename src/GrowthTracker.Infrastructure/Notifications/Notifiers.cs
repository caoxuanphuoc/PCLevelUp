using GrowthTracker.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Tmds.DBus.Protocol;

namespace GrowthTracker.Infrastructure.Notifications;

/// <summary>
/// Linux notifications through org.freedesktop.Notifications on the session bus (GNOME, KDE, XFCE…).
/// Without a session bus (e.g. a bare CI box) it only logs, as TDD §7.2 allows.
/// </summary>
public sealed class LinuxDbusNotifier(ILogger<LinuxDbusNotifier> logger) : INotifier
{
    private const string Service = "org.freedesktop.Notifications";
    private const string Path = "/org/freedesktop/Notifications";

    public async Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = DBusConnection.Session;
            if (connection is null)
            {
                logger.LogWarning("No D-Bus session bus; notification not shown");
                return;
            }

            await connection.CallMethodAsync(CreateNotifyMessage(connection, title, message)).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "D-Bus notification failed");
        }
    }

    /// <summary>Notify(app_name s, replaces_id u, app_icon s, summary s, body s, actions as, hints a{sv}, expire_timeout i).</summary>
    private static MessageBuffer CreateNotifyMessage(DBusConnection connection, string title, string message)
    {
        using var writer = connection.GetMessageWriter();
        writer.WriteMethodCallHeader(destination: Service, path: Path, @interface: Service, member: "Notify",
                                     signature: "susssasa{sv}i");
        writer.WriteString("Growth Tracker");
        writer.WriteUInt32(0);
        writer.WriteString("appointment-soon");
        writer.WriteString(title);
        writer.WriteString(message);
        writer.WriteArray(Array.Empty<string>());
        var hints = writer.WriteDictionaryStart();
        writer.WriteDictionaryEnd(hints);
        writer.WriteInt32(-1);
        return writer.CreateMessage();
    }
}

/// <summary>Fallback when the platform has no notifier: the reminder is only logged.</summary>
public sealed class LogOnlyNotifier(ILogger<LogOnlyNotifier> logger) : INotifier
{
    public Task NotifyAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notification (not shown on this platform)");
        return Task.CompletedTask;
    }
}
