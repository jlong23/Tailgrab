using NLog;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Tailgrab.Common;
using XSSocket.Models;

namespace Tailgrab.Clients.XSOverlay
{
    public class OverlayManager
    {
        private static XSSocket.XSSocket? connector;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public async Task Initialize()
        {
            lock(this)
            {
                logger.Info("Initializing XSOverlay...");
                if (connector == null || connector.State != WebSocketState.Open)
                {
                    logger.Info("Not connected XSOverlay...");
                    connector = new XSSocket.XSSocket("tailgrab");
                    Task.Run(() => connector.ConnectAsync().ConfigureAwait(false));
                    while (connector.State == WebSocketState.Connecting)
                    {
                        logger.Info("Connecting to XSOverlay WS...");
                        Task.Run(() => Task.Delay(1000)); // wait 1 second
                    }
                    if (connector.State == WebSocketState.Open)
                    {
                        logger.Info("Connected to XSOverlay WS!");
                    }
                    else
                    {
                        logger.Error($"Failed to connect to XSOverlay WS; State: {connector.State}");
                        connector.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            if (connector != null)
            {
                connector.Dispose();
                connector = null;
                logger.Info("Disconnected from XSOverlay WS.");
            }
        }

        public async Task SendNotification(AlertTypeEnum alertType, string? title, string message, Image? icon )
        {
            AlertTypeEnum xsOverlayLevel =
                CommonConst.AlertTypeEnumFromString(ConfigStore.GetStoredKeyString(CommonConst.Registry_XSOverlay_Level) ?? CommonConst.XSOverlay_Level_None);

            // Fail Fast if XSOverlay notifications are disabled
            if (xsOverlayLevel == AlertTypeEnum.None)
            {
                logger.Debug("XSOverlay notifications are disabled. Skipping sending notification.");
                return;
            }

            // Check if the alert type is below the configured XSOverlay level
            if (alertType < xsOverlayLevel)
            {
                logger.Debug($"Alert type {alertType} is below the configured XSOverlay level {xsOverlayLevel}. Skipping sending notification.");
                return;
            }

            // Ensure there is a connection to XSOverlay before sending the notification
            if (connector == null || connector.State != WebSocketState.Open)
            {
                await Initialize();
                if( connector == null || connector.State != WebSocketState.Open)
                {
                    logger.Warn("Cannot send notification. Not connected to XSOverlay WS.");
                    return;
                }
            }

            // XSOverlay notification payload
            XSNotificationObject notificationObject = new()
            {
                title = $"{alertType.ToString()} Notification",
                content = message,
                timeout = 5,
                height = 174,
                sourceApp = "Tailgrab",
                icon = "warning",
                opacity = 0.75f

            };

            // Override title if provided
            if ( !string.IsNullOrEmpty(title))
            {
                notificationObject.title = title;
            }

            // Convert icon to Base64 if provided
            if (icon != null)
            {
                notificationObject.icon = Utility.ConvertImageToBase64(icon);
                notificationObject.useBase64Icon = true;
            }

            // Send the notification to XSOverlay
            await connector.SendNotification(notificationObject);
        }
    }
}
