using System.Globalization;
using Kazo.Os.AppSdk.Craft;
using Kazo.Photo.Hosting;

namespace Kazo.Photo.Tests;

/// <summary>What the window gives PhotoCraft: the page's query, the language, the theme and the chords.</summary>
public sealed class HostingTests
{
    [Fact]
    public void The_page_always_asks_PhotoCraft_for_its_command_API()
    {
        // PhotoCraft installs window.photocraft only with ?control (fork, H7); the module's proxy needs it.
        Assert.Equal(new Dictionary<string, string> { ["control"] = string.Empty }, PhotoCraftParameters.For(webGl: false));
    }

    [Fact]
    public void The_webgl_argument_forces_WebGL_2()
    {
        IReadOnlyDictionary<string, string> parameters = PhotoCraftParameters.For(webGl: true);

        Assert.Equal(string.Empty, parameters["webgl"]);
        Assert.Equal(string.Empty, parameters["control"]);
    }

    [Theory]
    [InlineData("fr-CA", "fr")]
    [InlineData("de-DE", "de")]
    [InlineData("pt-PT", "pt-br")]
    [InlineData("zh-TW", "zh-hant")]
    [InlineData("zh-CN", "zh-hans")]
    [InlineData("sr-Latn-RS", "sr-latn")]
    [InlineData("nb-NO", "nb")]
    [InlineData("en-US", "en")]
    [InlineData("eu-ES", "en")]
    public void PhotoCraft_gets_one_of_its_35_languages_or_English(string culture, string expected)
    {
        Assert.Equal(expected, PhotoCraftLanguage.Of(CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void PhotoCraft_has_35_languages_besides_English()
    {
        Assert.Equal(35, PhotoCraftLanguage.Available.Count);
        Assert.Equal(PhotoCraftLanguage.Available.Count, PhotoCraftLanguage.Available.Distinct().Count());
    }

    [Theory]
    [InlineData("light", "proLight")]
    [InlineData("LIGHT", "proLight")]
    [InlineData("dark", "pro")]
    [InlineData(null, "pro")]
    [InlineData("high-contrast", "pro")]
    public void PhotoCraft_follows_the_OS_scheme_not_the_browser(string? scheme, string expected)
    {
        Assert.Equal(expected, PhotoCraftTheme.Of(scheme));
    }

    [Fact]
    public void Ctrl_N_stays_PhotoCraft_s_New_document()
    {
        // Decision 10: the window reserves the OS chords only.
        string window = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Kazo.Photo", "PhotoWindow.razor.cs"));
        Assert.Contains("ReservedKeys = ReservedKeys.Os,", window);
        Assert.DoesNotContain(ReservedKeys.NewWindow, ReservedKeys.Os);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kazo.Photo.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kazo.Photo.slnx not found above the test binaries.");
    }
}
