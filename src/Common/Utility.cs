using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tailgrab.Common
{
    public static class Utility
    {
        public static string GetCurrentDateTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static string I18NString(string key) {

            var resources = System.Windows.Application.Current.Resources;
            // Implementation for retrieving the internationalized string based on the key
            return (string)resources[key];
        }

        public static string GetRtfImageTag(Image img)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // Save the image as a JPEG (or PNG/BMP)
                img.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);

                // Convert byte array to a continuous hexadecimal string
                string hexData = BitConverter.ToString(ms.ToArray()).Replace("-", "").ToLower();

                // Target dimensions in twips (1 inch = 1440 twips)
                // picw/pich = original size, picwgoal/pichgoal = target display size
                int widthTwips = (int)(img.Width * 1440 / img.HorizontalResolution);
                int heightTwips = (int)(img.Height * 1440 / img.VerticalResolution);

                // Construct the RTF image tag string
                return $@"{{\pict\jpegblip\picw{img.Width}\pich{img.Height}\picwgoal{widthTwips}\pichgoal{heightTwips} {hexData}}}";
            }
        }

        public static Image ScaleImage(Image image, int maxWidth, int maxHeight)
        {
            // Calculate the highest scaling ratio possible for width and height
            double ratioX = (double)maxWidth / image.Width;
            double ratioY = (double)maxHeight / image.Height;

            // Use the smaller ratio to guarantee the image fits inside both boundaries
            double ratio = Math.Min(ratioX, ratioY);

            // Calculate target dimensions
            int newWidth = (int)(image.Width * ratio);
            int newHeight = (int)(image.Height * ratio);

            // Create a blank bitmap canvas with calculated dimensions
            Bitmap newImage = new Bitmap(newWidth, newHeight);

            // Render the original image onto the new canvas using high-quality settings
            using (Graphics graphics = Graphics.FromImage(newImage))
            {
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(image, 0, 0, newWidth, newHeight);
            }

            return newImage;
        }

        public static string ConvertImageToBase64(Image image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                // XSOverlay accepts standard formats like PNG or JPEG.
                // PNG is recommended to preserve transparency.
                image.Save(ms, ImageFormat.Png);

                byte[] imageBytes = ms.ToArray();

                // Convert byte array to Base64 string
                return Convert.ToBase64String(imageBytes);
            }
        }

        public static Image ConvertGeometryToGdiImage(Geometry geometry, int width, int height)
        {
            // 1. Create a WPF DrawingVisual and draw the geometry
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawGeometry(System.Windows.Media.Brushes.Blue,
                    new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 2), geometry);
            }

            // 2. Render the visual to a RenderTargetBitmap
            RenderTargetBitmap renderBitmap = new RenderTargetBitmap(
                width, height, 96d, 96d, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);

            // 3. Create a System.Drawing Bitmap with matching pixel formatting
            Bitmap gdiBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            // 4. Safely copy the pixel byte data into GDI memory
            BitmapData bitmapData = gdiBitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                gdiBitmap.PixelFormat);

            try
            {
                int stride = bitmapData.Stride;
                renderBitmap.CopyPixels(Int32Rect.Empty, bitmapData.Scan0, stride * height, stride);
            }
            finally
            {
                gdiBitmap.UnlockBits(bitmapData);
            }

            return gdiBitmap; // Returns as System.Drawing.Image
        }
    }
}