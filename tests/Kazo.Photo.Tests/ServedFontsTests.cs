using System.Security.Cryptography;

namespace Kazo.Photo.Tests;

/// <summary>
/// The fonts PhotoCraft serves in Kazo Photo (doc/en/plan.md, work item 2, Benoit's Q4 = A):
/// craft-fonts' Latin catalog in <c>src/Kazo.Photo/wwwroot/host/fonts/</c>, as tools/vendor-fonts.sh
/// copies it.
/// </summary>
public sealed class ServedFontsTests
{
    private static readonly string FontsFolder = Path.Combine(RepositoryRoot(), "src", "Kazo.Photo", "wwwroot", "host", "fonts");

    private static readonly IReadOnlyList<string[]> Lines = ReadManifest();

    [Fact]
    public void The_manifest_sits_where_PhotoCraft_looks_for_it()
    {
        // apps/photocraft-web/src/web.rs: FONTS_MANIFEST = "fonts/manifest.txt", relative to the page
        // (wwwroot/host/index.html).
        Assert.True(File.Exists(Path.Combine(FontsFolder, "manifest.txt")));
        Assert.True(File.Exists(Path.Combine(FontsFolder, "..", "index.html")));
        Assert.NotEmpty(Lines);
    }

    [Fact]
    public void Every_font_is_there_unchanged_with_its_OFL_licence()
    {
        foreach (string[] line in Lines)
        {
            Assert.True(line.Length >= 8, string.Join(" | ", line));
            string file = Path.Combine(FontsFolder, "..", line[2]);
            Assert.True(File.Exists(file), line[2]);
            Assert.Equal(line[6], Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))));
            Assert.Equal("OFL-1.1", line[4]);
            Assert.True(File.Exists(Path.Combine(FontsFolder, "..", line[5])), line[5]);
        }
    }

    [Fact]
    public void Paths_stay_inside_the_fonts_folder()
    {
        foreach (string[] line in Lines)
        {
            Assert.StartsWith("fonts/", line[2], StringComparison.Ordinal);
            Assert.DoesNotContain("..", line[2], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Only_Latin_fonts_are_served_and_Inter_stays_PhotoCraft_s_own()
    {
        // Q4 = A: no CJK font in the first version. Inter is already in PhotoCraft's .wasm.
        foreach (string[] line in Lines)
        {
            Assert.Equal("Latn", line[3]);
            Assert.NotEqual("Inter", line[0]);
        }
    }

    [Fact]
    public void Every_file_of_the_folder_is_in_the_manifest()
    {
        HashSet<string> listed = [.. Lines.SelectMany(line => new[] { line[2], line[5] }), "fonts/manifest.txt"];
        string hostFolder = Path.GetFullPath(Path.Combine(FontsFolder, ".."));
        foreach (string file in Directory.EnumerateFiles(FontsFolder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(hostFolder, file).Replace('\\', '/');
            Assert.Contains(relative, listed);
        }
    }

    [Fact]
    public void The_fonts_stay_near_the_size_Benoit_chose()
    {
        // Q4 = A: about 25 MB. A craft-fonts update that grows them a lot is a new decision.
        long bytes = Directory.EnumerateFiles(FontsFolder, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
        Assert.InRange(bytes, 1, 30L * 1000 * 1000);
    }

    private static List<string[]> ReadManifest() =>
        [.. File.ReadAllLines(Path.Combine(FontsFolder, "manifest.txt"))
            .Where(line => line.Trim().Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(" | ").Select(field => field.Trim()).ToArray())];

    private static string RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kazo.Photo.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Kazo.Photo.slnx not found above " + AppContext.BaseDirectory);
    }
}
