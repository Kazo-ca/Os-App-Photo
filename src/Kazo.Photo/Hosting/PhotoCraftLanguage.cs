using System.Globalization;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Photo.Hosting;

/// <summary>
/// The language given to PhotoCraft: its preference <c>interface.language</c> (<c>prefs.set</c>) and
/// the module's <c>language</c> option (decision 9).
/// </summary>
/// <remarks>
/// PhotoCraft has a catalog for English and the 35 codes of <see cref="Available"/> (doc/en/plan.md,
/// "Reading PhotoCraft"), named like WordCraft's, so the module's <see cref="CraftLanguage.Pick"/>
/// maps a Kazo culture to them (<c>pt-br</c>, <c>zh-hans</c>, <c>zh-hant</c>, <c>sr-latn</c>).
/// </remarks>
public static class PhotoCraftLanguage
{
    /// <summary>The languages PhotoCraft 0.23.1 has a catalog for, besides English.</summary>
    public static IReadOnlyList<string> Available { get; } =
    [
        "ar", "az", "bn", "bs", "cs", "da", "de", "el", "es", "fi", "fr", "he", "hi", "hu", "id", "it",
        "ja", "ka", "ko", "nb", "nl", "pl", "pt-br", "ro", "ru", "sr-latn", "sv", "ta", "th", "tr", "uk",
        "ur", "vi", "zh-hans", "zh-hant",
    ];

    /// <summary>The code for a culture: one of <see cref="Available"/>, or English.</summary>
    /// <param name="culture">The Kazo user's culture.</param>
    /// <returns>A code PhotoCraft accepts.</returns>
    public static string Of(CultureInfo culture) => CraftLanguage.Pick(culture, (IReadOnlyCollection<string>)Available);
}
