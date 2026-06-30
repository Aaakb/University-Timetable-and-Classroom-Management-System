using System.Drawing;
using System.Drawing.Drawing2D;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class PasswordToggleIcon
    {
        private static readonly Color IconColor = Color.FromArgb(37, 99, 235);

        public static void Apply(Guna.UI2.WinForms.Guna2Button button, bool passwordVisible)
        {
            Image? previousImage = button.Image;

            button.Text = string.Empty;
            button.Image = Create(passwordVisible);
            button.ImageAlign = HorizontalAlignment.Center;
            button.ImageSize = new Size(22, 22);
            button.AccessibleName = passwordVisible ? "Hide password" : "Show password";

            previousImage?.Dispose();
        }

        private static Bitmap Create(bool passwordVisible)
        {
            const int size = 28;
            var bitmap = new Bitmap(size, size);

            using var graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            using var pen = new Pen(IconColor, 2.2F)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };

            using var pupilBrush = new SolidBrush(IconColor);
            var eyeBounds = new RectangleF(4, 8, 20, 12);
            graphics.DrawArc(pen, eyeBounds, 200, 140);
            graphics.DrawArc(pen, eyeBounds, 20, 140);
            graphics.FillEllipse(pupilBrush, 11, 11, 6, 6);

            if (!passwordVisible)
            {
                graphics.DrawLine(pen, 6, 22, 22, 6);
            }

            return bitmap;
        }
    }
}
