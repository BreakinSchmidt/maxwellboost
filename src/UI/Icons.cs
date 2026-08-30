using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MaxwellBoost.UI
{
    public static class Icons
    {
        public static Icon CreateMicrophoneIcon(bool isConnected, bool hasError = false)
        {
            const int size = 32;
            using var bitmap = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // Colors
                var bodyColor = isConnected ? Color.FromArgb(245, 245, 245) : Color.FromArgb(130, 130, 130);
                var ringColor = isConnected ? Color.FromArgb(210, 210, 210) : Color.FromArgb(100, 100, 100);
                var statusColor = isConnected ? Color.FromArgb(46, 204, 113) : (hasError ? Color.FromArgb(231, 76, 60) : Color.FromArgb(149, 165, 166));

                // 1. Microphone Capsule Body (Rounded Capsule)
                using (var brush = new SolidBrush(bodyColor))
                {
                    g.FillEllipse(brush, 11, 4, 10, 8);
                    g.FillRectangle(brush, 11, 8, 10, 7);
                    g.FillEllipse(brush, 11, 11, 10, 8);
                }

                // 2. Microphone U-bracket
                using (var pen = new Pen(ringColor, 2f))
                {
                    g.DrawArc(pen, 8, 7, 16, 15, 0, 180);
                    g.DrawLine(pen, 16, 22, 16, 26);
                    g.DrawLine(pen, 11, 26, 21, 26);
                }

                // 3. Status Badge (Bottom-Right Circle)
                using (var badgeBrush = new SolidBrush(statusColor))
                using (var badgeBorder = new Pen(Color.FromArgb(25, 25, 25), 1.5f))
                {
                    const int badgeX = 18;
                    const int badgeY = 18;
                    const int badgeSize = 12;

                    g.FillEllipse(badgeBrush, badgeX, badgeY, badgeSize, badgeSize);
                    g.DrawEllipse(badgeBorder, badgeX, badgeY, badgeSize, badgeSize);
                }
            }

            return ConvertBitmapToManagedIcon(bitmap);
        }

        private static Icon ConvertBitmapToManagedIcon(Bitmap bitmap)
        {
            using var pngStream = new MemoryStream();
            bitmap.Save(pngStream, ImageFormat.Png);
            var pngBytes = pngStream.ToArray();

            using var icoStream = new MemoryStream();
            using var writer = new BinaryWriter(icoStream);

            // ICONDIR Header (6 bytes)
            writer.Write((ushort)0); // Reserved
            writer.Write((ushort)1); // Image Type 1 = Icon
            writer.Write((ushort)1); // Image Count = 1

            // ICONDIRENTRY (16 bytes)
            writer.Write((byte)bitmap.Width);  // Width
            writer.Write((byte)bitmap.Height); // Height
            writer.Write((byte)0);              // Colors in palette
            writer.Write((byte)0);              // Reserved
            writer.Write((ushort)1);            // Color planes
            writer.Write((ushort)32);           // Bits per pixel
            writer.Write((uint)pngBytes.Length);// Image size in bytes
            writer.Write((uint)22);             // Offset of image data (6 + 16 = 22)

            // PNG Data
            writer.Write(pngBytes);
            writer.Flush();

            icoStream.Position = 0;
            return new Icon(icoStream);
        }
    }
}
