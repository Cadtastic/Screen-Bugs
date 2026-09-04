using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using IconGen;
using ScreenBugs.Tray;

// Regenerates the checked-in branding assets from the app's own ant glyph.
// Run: dotnet run --project tools/IconGen

// The file icon cannot follow the system theme the way the tray glyph does, so it is always the
// dark-theme red: legible on both light and dark backgrounds, where near-black disappears.
var color = Color.FromArgb(216, 50, 31);
int[] sizes = [16, 24, 32, 48, 64, 128, 256];

string assets = Path.Combine(FindRepositoryRoot(), "assets");
Directory.CreateDirectory(assets);

var images = sizes.Select(size => AntGlyph.Draw(size, color)).ToList();
try
{
    IcoWriter.Write(Path.Combine(assets, "ScreenBugs.ico"), images);
}
finally
{
    foreach (var image in images)
    {
        image.Dispose();
    }
}

using (var side = WizardBitmaps.Side(164, 314))
{
    side.Save(Path.Combine(assets, "wizard-side.bmp"), ImageFormat.Bmp);
}

using (var header = WizardBitmaps.Header(150, 57))
{
    header.Save(Path.Combine(assets, "wizard-header.bmp"), ImageFormat.Bmp);
}

Console.WriteLine($"Wrote ScreenBugs.ico ({sizes.Length} sizes), wizard-side.bmp and wizard-header.bmp to {assets}");

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScreenBugs.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName
        ?? throw new InvalidOperationException("Could not find ScreenBugs.slnx in any parent directory.");
}
