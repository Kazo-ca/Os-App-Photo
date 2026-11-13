using System.Text.Json;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Photo.Documents;

/// <summary>
/// Reads the documents PhotoCraft has open from its <c>session.inspect</c> answer (doc/en/plan.md,
/// decision 8): <c>{ active, documents[{ index, name, path, width, height, dirty, revision }] }</c>.
/// </summary>
/// <remarks>
/// <para>
/// The module's <see cref="OpenDocument.ListFrom(JsonElement)"/> reads WordCraft's and GridCraft's
/// shapes, which carry an <c>id</c>; PhotoCraft's list has none, only the tab's place
/// (<c>index</c>), which is what <c>document.activate { document }</c> takes. So
/// <see cref="OpenDocument.Id"/> is that index here: it holds until a tab opens or closes, which is
/// why the window reads the list again before each use.
/// </para>
/// <para>
/// <see cref="OpenDocument.Name"/> is the document's <c>path</c>: the name PhotoCraft saves it under,
/// which is the file's name for a file the window opened (fork, H2), the name the last save chose
/// otherwise, and <see langword="null"/> for a document never saved. <see cref="OpenDocument.Title"/>
/// is the name on the tab. The answer comes from the iframe: anything malformed is skipped.
/// </para>
/// </remarks>
public static class PhotoDocumentList
{
    /// <summary>Reads the documents of a <c>session.inspect</c> answer.</summary>
    /// <param name="inspect">PhotoCraft's answer.</param>
    /// <returns>One entry per tab, in PhotoCraft's order; empty when the answer has no document list.</returns>
    public static IReadOnlyList<OpenDocument> ListFrom(JsonElement inspect)
    {
        if (inspect.ValueKind != JsonValueKind.Object
            || !inspect.TryGetProperty("documents", out JsonElement documents)
            || documents.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        long? active = inspect.TryGetProperty("active", out JsonElement current)
            && current.ValueKind == JsonValueKind.Number
            && current.TryGetInt64(out long index)
                ? index
                : null;

        List<OpenDocument> list = [];
        foreach (JsonElement document in documents.EnumerateArray())
        {
            if (document.ValueKind != JsonValueKind.Object
                || !document.TryGetProperty("index", out JsonElement place)
                || place.ValueKind != JsonValueKind.Number
                || !place.TryGetInt64(out long tab)
                || tab < 0
                || list.Any(listed => listed.Id == tab))
            {
                continue;
            }

            string? path = Text(document, "path");
            list.Add(new OpenDocument(
                tab,
                string.IsNullOrEmpty(path) ? null : path,
                Text(document, "name") ?? string.Empty,
                Flag(document, "dirty"),
                tab == active));
        }

        return list;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
