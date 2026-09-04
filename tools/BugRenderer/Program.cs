using System.IO;
using ScreenBugs.Core.Simulation;
using ScreenBugs.Rendering;

namespace BugRenderer;

/// <summary>
/// Regenerates the specimen images the README uses.
/// Run: dotnet run --project tools/BugRenderer
/// </summary>
public static class Program
{
    // WPF's rendering objects have thread affinity and require a single-threaded apartment,
    // which a console app does not get by default.
    [STAThread]
    public static void Main()
    {
        string images = Path.Combine(FindRepositoryRoot(), "docs", "images", "bugs");
        var registry = new BugPainterRegistry();

        foreach (var species in SpeciesCatalog.All)
        {
            string path = Path.Combine(images, $"{species.Id}.png");
            SpecimenRenderer.Write(path, species.Id, registry);
            Console.WriteLine($"{species.Id,-20} {new FileInfo(path).Length,7:N0} bytes");
        }

        Console.WriteLine($"\nWrote {SpeciesCatalog.All.Count} specimens to {images}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ScreenBugs.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find ScreenBugs.slnx in any parent directory.");
    }
}
