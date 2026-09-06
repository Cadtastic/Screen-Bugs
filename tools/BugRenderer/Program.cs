using System.IO;
using ScreenBugs.Core.Simulation;
using ScreenBugs.Rendering;

namespace BugRenderer;

/// <summary>
/// Regenerates the specimen images the README uses, or, with <c>--filmstrip [dir]</c>, renders
/// each species across a stride for review.
/// Run: dotnet run --project tools/BugRenderer
/// Run: dotnet run --project tools/BugRenderer -- --filmstrip
/// </summary>
public static class Program
{
    // WPF's rendering objects have thread affinity and require a single-threaded apartment,
    // which a console app does not get by default.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] != "--filmstrip")
        {
            Console.Error.WriteLine($"Unknown argument '{args[0]}'. Usage: BugRenderer [--filmstrip [dir]]");
            Environment.Exit(2);
        }

        string root = FindRepositoryRoot();
        var registry = new BugPainterRegistry();

        if (args.Length > 0 && args[0] == "--filmstrip")
        {
            // artifacts/ is gitignored: filmstrips are looked at, never committed.
            string filmstrips = args.Length > 1
                ? Path.GetFullPath(args[1])
                : Path.Combine(root, "artifacts", "filmstrips");
            WriteAll(filmstrips, registry, FilmstripRenderer.Write, "filmstrips");
            return;
        }

        WriteAll(Path.Combine(root, "docs", "images", "bugs"), registry, SpecimenRenderer.Write, "specimens");
    }

    private static void WriteAll(
        string directory, BugPainterRegistry registry, Action<string, SpeciesId, BugPainterRegistry> write, string label)
    {
        foreach (var species in SpeciesCatalog.All)
        {
            string path = Path.Combine(directory, $"{species.Id}.png");
            write(path, species.Id, registry);
            Console.WriteLine($"{species.Id,-20} {new FileInfo(path).Length,7:N0} bytes");
        }

        Console.WriteLine($"\nWrote {SpeciesCatalog.All.Count} {label} to {directory}");
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
