using System.Text.Json;
using System.Xml.Linq;

namespace Kazo.Photo.Tests;

/// <summary>
/// The licenses travel with the app (work item 3, THIRD-PARTY-NOTICES.md): the project links the
/// repository's license and notices into the package's web root, PhotoCraft's own notices are in
/// its build folder, the files they refer to are copied next to them, and the notices name the
/// PhotoCraft build actually bundled.
/// </summary>
public sealed class LegalFilesTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string PhotoCraftFolder = Path.Combine(Root, "src", "Kazo.Photo", "wwwroot", "photocraft");

    [Theory]
    [InlineData("LICENSE", "wwwroot/legal/LICENSE.txt")]
    [InlineData("THIRD-PARTY-NOTICES.md", "wwwroot/legal/THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md", "wwwroot/legal/THIRD-PARTY-NOTICES-fr.md")]
    public void The_project_puts_the_repository_legal_files_in_the_package(string file, string link)
    {
        XDocument project = XDocument.Load(Path.Combine(Root, "src", "Kazo.Photo", "Kazo.Photo.csproj"));

        // A Content item linked under wwwroot/ is a static web asset: KazoOsPack packs it.
        XElement? item = project.Descendants("Content")
            .FirstOrDefault(element => (string?)element.Attribute("Link") == link);
        Assert.NotNull(item);
        Assert.Equal($"../../{file}", (string?)item.Attribute("Include"));
        Assert.True(File.Exists(Path.Combine(Root, file)), $"{file} is missing at the repository root");
    }

    [Theory]
    [InlineData("LICENSE-MIT")]
    [InlineData("LICENSE-APACHE")]
    [InlineData("NOTICE")]
    [InlineData("ATTRIBUTION.md")]
    [InlineData("OFL-Inter.txt")]
    [InlineData("OFL-JetBrainsMono.txt")]
    public void PhotoCraft_ships_its_own_notices(string file)
    {
        Assert.True(File.Exists(Path.Combine(PhotoCraftFolder, file)), $"photocraft/{file} is missing");
    }

    [Theory]
    [InlineData("LICENSE-lucide.txt")]
    [InlineData("LICENSE-noun-magnetic-lasso.txt")]
    [InlineData("LICENSE-symmetry-axes.txt")]
    [InlineData("app-icon-LICENSE.txt")]
    [InlineData("LICENSE-solarized.txt")]
    [InlineData("LICENSE-SCOWL.txt")]
    [InlineData("LICENSE-libfacedetection.txt")]
    public void The_license_files_PhotoCraft_refers_to_ship_in_the_package(string file)
    {
        // PhotoCraft's web build names these files in its NOTICE and ATTRIBUTION.md but does not
        // carry them: they are copied from the fork at the bundled commit, and listed in
        // THIRD-PARTY-NOTICES.md.
        string path = Path.Combine(Root, "src", "Kazo.Photo", "wwwroot", "legal", "photocraft", file);
        Assert.True(File.Exists(path), $"legal/photocraft/{file} is missing");
        Assert.True(new FileInfo(path).Length > 0, $"legal/photocraft/{file} is empty");
        Assert.Contains($"`{file}`", File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md")), StringComparison.Ordinal);
        Assert.Contains($"`{file}`", File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES-fr.md")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_bundled_PhotoCraft_commit_and_version(string file)
    {
        // The license files above were copied at this commit: moving to another build without
        // copying them again and updating the notices fails here.
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(PhotoCraftFolder, "photocraft.json")));
        string commit = provenance.RootElement.GetProperty("source").GetProperty("commit").GetString()!;
        string version = provenance.RootElement.GetProperty("version").GetString()!;

        string notices = File.ReadAllText(Path.Combine(Root, file));
        Assert.Contains(commit, notices, StringComparison.Ordinal);
        Assert.Contains($"| {version}, ", notices, StringComparison.Ordinal);
        Assert.Contains($"kazo/v{version}", notices, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_craft_fonts_commit_and_every_served_family(string file)
    {
        string notices = File.ReadAllText(Path.Combine(Root, file));
        string revision = File.ReadAllText(Path.Combine(Root, "tools", "craft-fonts.rev")).Trim();
        Assert.Contains(revision, notices, StringComparison.Ordinal);

        string fonts = Path.Combine(Root, "src", "Kazo.Photo", "wwwroot", "host", "fonts");
        foreach (string family in Directory.GetDirectories(fonts))
        {
            string name = Path.GetFileName(family);
            Assert.True(File.Exists(Path.Combine(family, "OFL.txt")), $"fonts/{name}/OFL.txt is missing");
            Assert.Contains($"`fonts/{name}/OFL.txt`", notices, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("description.en.md", "PhotoCraft")]
    [InlineData("description.fr.md", "PhotoCraft")]
    [InlineData("description.en.md", "ArtCraft")]
    [InlineData("description.fr.md", "ArtCraft")]
    [InlineData("description.en.md", "Open Font License")]
    [InlineData("description.fr.md", "Open Font License")]
    public void The_store_listing_credits_PhotoCraft_and_the_fonts(string file, string name)
    {
        Assert.Contains(name, File.ReadAllText(Path.Combine(Root, "store", file)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_store_listing_names_the_minimum_Kazo_OS_version_of_the_manifest()
    {
        // The listing tells users which Kazo OS they need: it must follow the manifest.
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "src", "Kazo.Photo", "manifest.json")));
        string minimum = manifest.RootElement.GetProperty("sdkCompatibility").GetProperty("minimumVersion").GetString()!;
        Assert.Contains($"Kazo OS {minimum}", File.ReadAllText(Path.Combine(Root, "store", "description.en.md")), StringComparison.Ordinal);
        Assert.Contains($"Kazo OS {minimum}", File.ReadAllText(Path.Combine(Root, "store", "description.fr.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_store_tags_are_ones_the_catalog_accepts()
    {
        // Os-AppMarketPlace refuses any other tag (lower case, digits and '-', 30 characters at most):
        // Kazo Word 0.1.0 had "word processor" and its catalog folder had to be corrected by hand.
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "store", "store.json")));
        foreach (JsonElement tag in store.RootElement.GetProperty("tags").EnumerateArray())
        {
            Assert.Matches("^[a-z0-9][a-z0-9-]{0,29}$", tag.GetString());
        }
    }

    private static string FindRepositoryRoot()
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
