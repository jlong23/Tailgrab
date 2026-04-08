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
            if (connector == null)
            {
                connector = new XSSocket.XSSocket("tailgrab");
                connector.ConnectAsync().ConfigureAwait(false);
                while (connector.State == WebSocketState.Connecting)
                {
                    logger.Info("Connecting to OverlayManager...");
                    await Task.Delay(1000); // wait 1 second
                }
                if (connector.State == WebSocketState.Open)
                {
                    logger.Info("Connected to OverlayManager!");
                }
                else
                {
                    logger.Error($"Failed to connect to OverlayManager. State: {connector.State}");
                    connector.Dispose();
                }
            }
            else if (connector.State == WebSocketState.Open)
            {
                logger.Warn("Already connected to OverlayManager.");

            }
        }

        public void Dispose()
        {
            if (connector != null)
            {
                connector.Dispose();
                logger.Info("Disconnected from OverlayManager.");
            }
        }

        public async Task SendNotification(AlertTypeEnum alertType, string message )
        {
            AlertTypeEnum xsOverlayLevel = 
                CommonConst.AlertTypeEnumFromString( ConfigStore.GetStoredKeyString(CommonConst.Registry_XSOverlay_Level) ?? CommonConst.XSOverlay_Level_None);

            if( xsOverlayLevel == AlertTypeEnum.None)
            {
                logger.Debug("XSOverlay notifications are disabled. Skipping sending notification.");
                return;
            } 
            else if (alertType < xsOverlayLevel)
            {
                logger.Debug($"Alert type {alertType} is below the configured XSOverlay level {xsOverlayLevel}. Skipping sending notification.");
                return;
            }

            if ( connector == null || connector.State != WebSocketState.Open)
            {
                logger.Warn("Cannot send notification. Not connected to OverlayManager.");
                return;
            }

            XSNotificationObject notificationObject = new()
            {
                title = $"{alertType.ToString()} Notifcation",
                content = message,
                timeout = 5,
                height = 174,
                sourceApp = "Tailgrab",
                icon = "warning"

            };
            await connector.SendNotification(notificationObject);
        }
    }
}
