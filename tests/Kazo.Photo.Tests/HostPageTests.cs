using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Kazo.Photo.Tests;

/// <summary>The iframe page (decision 5) and the window's texts.</summary>
public sealed partial class HostPageTests
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly string App = Path.Combine(Root, "src", "Kazo.Photo");
    private static readonly string Page = File.ReadAllText(Path.Combine(App, "wwwroot", "host", "index.html"));

    [Fact]
    public void The_page_describes_PhotoCraft_for_the_module_proxy()
    {
        Assert.Matches(
            """<canvas id="photocraft_canvas" data-host="page"\s+data-craft-app="photocraft" data-craft-build="../photocraft/photocraft.json">""",
            Page);
        Assert.Contains("""<script type="module" src="../../Kazo.Os.AppSdk.Craft/craft-proxy.js"></script>""", Page);
        Assert.Contains("""<link rel="stylesheet" href="../../Kazo.Os.AppSdk.Craft/craft-page.css">""", Page);
        Assert.Contains("""<link rel="stylesheet" href="photocraft-page.css">""", Page);
        Assert.Contains("""id="photocraft_loading" data-craft-loading""", Page);
    }

    [Fact]
    public void The_page_keeps_the_message_PhotoCraft_reveals_when_its_module_stops()
    {
        // apps/photocraft-web/src/web.rs looks these ids up and removes "hidden" (doc/en/plan.md).
        Assert.Contains("""<div id="photocraft_stopped" hidden role="alert">""", Page);
        Assert.Contains("""<p id="photocraft_stopped_detail"></p>""", Page);
        Assert.True(File.Exists(Path.Combine(App, "wwwroot", "host", "photocraft-page.css")));
    }

    [Fact]
    public void The_build_the_page_names_is_in_the_package()
    {
        Assert.True(File.Exists(Path.Combine(App, "wwwroot", "photocraft", "photocraft.json")));
    }

    [Fact]
    public void The_page_has_no_inline_script_or_style()
    {
        Assert.DoesNotMatch("<script(?![^>]*\\ssrc=)[^>]*>", Page);
        Assert.DoesNotContain("<style", Page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" style=", Page, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void English_and_French_texts_have_the_same_keys()
    {
        Assert.Equal(KeysOf("Strings.resx"), KeysOf("Strings.fr.resx"));
    }

    [Fact]
    public void Every_text_the_window_reads_exists()
    {
        HashSet<string> keys = KeysOf("Strings.resx");
        IEnumerable<string> sources = Directory.EnumerateFiles(App, "PhotoWindow*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
            .Select(File.ReadAllText);
        string[] used = [.. sources.SelectMany(text => TextKey().Matches(text).Select(match => match.Groups[1].Value)).Distinct()];

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, keys));
    }

    private static HashSet<string> KeysOf(string file) =>
        [.. XDocument.Load(Path.Combine(App, "Resources", file)).Root!.Elements("data").Select(data => (string)data.Attribute("name")!)];

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kazo.Photo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kazo.Photo.slnx not found above the test binaries.");
    }

    // L["Key"] and L["Key", ...] in the window, and the keys picked in a ternary (L[x ? "A" : "B"]).
    [GeneratedRegex("""(?:L\[|\? |: )"([A-Za-z]+)"(?=[\],])""")]
    private static partial Regex TextKey();
}
