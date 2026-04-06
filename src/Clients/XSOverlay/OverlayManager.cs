using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using XSSocket.Models;
using NLog;

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

        public async Task SendNotification(string title, string message )
        {
            if ( connector == null || connector.State != WebSocketState.Open)
            {
                logger.Warn("Cannot send notification. Not connected to OverlayManager.");
                return;
            }

            XSNotificationObject notificationObject = new()
            {
                title = title,
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
