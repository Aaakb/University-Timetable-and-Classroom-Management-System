using System.Drawing;

namespace University_Timetable_and_Classroom_Management_System
{
    internal static class BrandAssets
    {
        private const string AssetsFolder = "Assets";
        private const string LogoFileName = "AppLogo.png";
        private const string IconFileName = "AppIcon.ico";

        public static Image? LoadLogoImage()
        {
            string? path = FindAssetPath(LogoFileName);

            if (path is null)
            {
                return null;
            }

            using var image = Image.FromFile(path);
            return new Bitmap(image);
        }

        public static Icon? LoadIcon()
        {
            string? path = FindAssetPath(IconFileName);

            if (path is null)
            {
                return null;
            }

            using var icon = new Icon(path);
            return (Icon)icon.Clone();
        }

        private static string? FindAssetPath(string fileName)
        {
            var paths = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, AssetsFolder, fileName),
                Path.Combine(AppContext.BaseDirectory, fileName)
            };

            DirectoryInfo? current = new(AppContext.BaseDirectory);

            for (int i = 0; i < 8 && current is not null; i++)
            {
                paths.Add(Path.Combine(current.FullName, AssetsFolder, fileName));
                paths.Add(Path.Combine(
                    current.FullName,
                    "University Timetable and Classroom Management System",
                    AssetsFolder,
                    fileName));

                current = current.Parent;
            }

            return paths.FirstOrDefault(File.Exists);
        }
    }
}
