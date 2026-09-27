using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tailgrab.MCP;

/// <summary>
/// Manages MCP subscriptions and broadcasts notifications to subscribers.
/// </summary>
public class McpSubscriptionManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private Dictionary<string, HashSet<string>> _subscriptions = new(); // subscriptionType -> subscriptionIds
    private Dictionary<string, (string SubscriptionId, string SubscriptionType, object? Context)> _subscriptionMetadata = new();

    /// <summary>
    /// Raised when a subscription is created.
    /// </summary>
    public event EventHandler<SubscriptionEventArgs>? SubscriptionCreated;

    /// <summary>
    /// Raised when a subscription is terminated.
    /// </summary>
    public event EventHandler<SubscriptionEventArgs>? SubscriptionTerminated;

    /// <summary>
    /// Raised when an event is published for a subscription.
    /// </summary>
    public event EventHandler<SubscriptionNotificationEventArgs>? NotificationPublished;

    /// <summary>
    /// Creates a new subscription and returns its unique ID.
    /// </summary>
    public string CreateSubscription(string subscriptionType, object? context = null)
    {
        string subscriptionId = Guid.NewGuid().ToString();

        if (!_subscriptions.ContainsKey(subscriptionType))
        {
            _subscriptions[subscriptionType] = new();
        }

        _subscriptions[subscriptionType].Add(subscriptionId);
        _subscriptionMetadata[subscriptionId] = (subscriptionId, subscriptionType, context);

        SubscriptionCreated?.Invoke(this, new SubscriptionEventArgs(subscriptionId, subscriptionType));
        Logger.Info($"Subscription created: {subscriptionId} for type: {subscriptionType}");

        return subscriptionId;
    }

    /// <summary>
    /// Terminates a subscription.
    /// </summary>
    public bool TerminateSubscription(string subscriptionId)
    {
        if (!_subscriptionMetadata.TryGetValue(subscriptionId, out var metadata))
        {
            return false;
        }

        _subscriptions[metadata.SubscriptionType].Remove(subscriptionId);
        _subscriptionMetadata.Remove(subscriptionId);

        SubscriptionTerminated?.Invoke(this, new SubscriptionEventArgs(subscriptionId, metadata.SubscriptionType));
        Logger.Info($"Subscription terminated: {subscriptionId}");

        return true;
    }

    /// <summary>
    /// Gets all subscription IDs for a given type.
    /// </summary>
    public IEnumerable<string> GetSubscriptionsOfType(string subscriptionType)
    {
        return _subscriptions.TryGetValue(subscriptionType, out var subs) ? subs : Enumerable.Empty<string>();
    }

    /// <summary>
    /// Publishes a notification to all subscribers of a given type.
    /// </summary>
    public void PublishNotification(string subscriptionType, object notificationData)
    {
        var subscribers = GetSubscriptionsOfType(subscriptionType).ToList();

        foreach (var subscriptionId in subscribers)
        {
            NotificationPublished?.Invoke(this, new SubscriptionNotificationEventArgs(subscriptionId, subscriptionType, notificationData));
            Logger.Debug($"Notification published to subscription: {subscriptionId} (type: {subscriptionType})");
        }
    }

    /// <summary>
    /// Checks if a subscription exists and is active.
    /// </summary>
    public bool IsSubscriptionActive(string subscriptionId)
    {
        return _subscriptionMetadata.ContainsKey(subscriptionId);
    }

    /// <summary>
    /// Gets metadata for a subscription.
    /// </summary>
    public (string SubscriptionId, string SubscriptionType, object? Context)? GetSubscriptionMetadata(string subscriptionId)
    {
        return _subscriptionMetadata.TryGetValue(subscriptionId, out var metadata) ? metadata : null;
    }
}

public class SubscriptionEventArgs(string subscriptionId, string subscriptionType) : EventArgs
{
    public string SubscriptionId { get; } = subscriptionId;
    public string SubscriptionType { get; } = subscriptionType;
}

public class SubscriptionNotificationEventArgs(string subscriptionId, string subscriptionType, object notificationData) : EventArgs
{
    public string SubscriptionId { get; } = subscriptionId;
    public string SubscriptionType { get; } = subscriptionType;
    public object NotificationData { get; } = notificationData;
}
