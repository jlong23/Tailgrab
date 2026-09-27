# MCP Subscription System Implementation

## Overview
A comprehensive subscription system has been added to the MCP server that allows the LLM to monitor changes to `PlayerManager.SelectedPlayer` and receive notifications in real-time.

## Components Created

### 1. **McpSubscriptionManager** (`src/MCP/McpSubscriptionManager.cs`)
   - Located in: `Tailgrab.MCP` namespace
   - Manages all active subscriptions
   - Maintains subscription metadata
   - Publishes notifications to subscribers
   - Features:
	 - `CreateSubscription()` - Creates a new subscription and returns a unique ID
	 - `TerminateSubscription()` - Removes an active subscription
	 - `GetSubscriptionsOfType()` - Gets all subscriptions for a type
	 - `PublishNotification()` - Broadcasts updates to all subscribers
	 - `IsSubscriptionActive()` - Checks subscription status
	 - `GetSubscriptionMetadata()` - Retrieves subscription details
   - Events:
	 - `SubscriptionCreated` - Fired when a subscription is created
	 - `SubscriptionTerminated` - Fired when a subscription ends
	 - `NotificationPublished` - Fired when notifications are sent

### 2. **MCP Tools** (Located in `Tailgrab.MCP.Tools` namespace)

#### a. **SubscribeToSelectedPlayerChanges** (`src/MCP/Tools/SubscribeToSelectedPlayerChanges.cs`)
   - Tool Name: `subscribe_to_selected_player_changes`
   - Creates a subscription to monitor selected player changes
   - Returns a unique `subscriptionId` for monitoring updates
   - No input parameters required
   - Response includes subscription ID and type

#### b. **UnsubscribeFromSelectedPlayerChanges** (`src/MCP/Tools/UnsubscribeFromSelectedPlayerChanges.cs`)
   - Tool Name: `unsubscribe_from_selected_player_changes`
   - Terminates an active subscription
   - Input: `subscriptionId` (string)
   - Confirms successful termination

#### c. **GetSelectedPlayerSubscriptionStatus** (`src/MCP/Tools/GetSelectedPlayerSubscriptionStatus.cs`)
   - Tool Name: `get_selected_player_subscription_status`
   - Retrieves the current status and value of an active subscription
   - Input: `subscriptionId` (string)
   - Returns: Current selected player details, subscription status, and timestamp

### 3. **PlayerManager Modification** (`src/PlayerManagement/PlayerManagement.cs`)
   - Converted `SelectedPlayer` property to a full property with backing field
   - Automatically detects when `SelectedPlayer` changes
   - Publishes notifications via `McpSubscriptionManager` when selection changes
   - Notification data includes:
	 - Selected player details (displayName, userId, isFriend, isWatched)
	 - Timestamp of change

### 4. **ServiceRegistry Integration** (`src/ServiceRegistry.cs`)
   - Added `McpSubscriptionManager` field
   - Added `GetSubscriptionManager()` method to access the subscription manager
   - Imported `Tailgrab.MCP` namespace

## Usage Flow

### Subscribe to Changes
1. LLM calls `subscribe_to_selected_player_changes`
2. Receives `subscriptionId` in response
3. Stores subscription ID for later use

### Monitor Changes
1. LLM periodically calls `get_selected_player_subscription_status` with the `subscriptionId`
2. Receives the current `SelectedPlayer` data and timestamp
3. Can detect changes by monitoring the timestamp or player details

### Unsubscribe
1. LLM calls `unsubscribe_from_selected_player_changes` with the `subscriptionId`
2. Subscription is terminated and cleaned up

## Notification Details
When `PlayerManager.SelectedPlayer` changes, the following notification is published:
```json
{
  "selectedPlayer": {
	"displayName": "PlayerName",
	"userId": "usr_xxxxx",
	"isFriend": true,
	"isWatched": false
  },
  "timestamp": "2024-01-15T10:30:45.1234567Z"
}
```

## Error Handling
- All subscription operations include try-catch blocks
- Invalid subscription IDs are handled gracefully
- Errors are logged via NLog
- Tool results indicate success/failure status

## Thread Safety
- Subscriptions are managed in a thread-safe manner suitable for multi-threaded environments
- PlayerManager notifications are wrapped in exception handlers

## Integration Points
- Automatically discovered by MCP tools discovery mechanism in `McpServer`
- Works with existing JSON-RPC infrastructure
- Compatible with existing MCP server endpoints
