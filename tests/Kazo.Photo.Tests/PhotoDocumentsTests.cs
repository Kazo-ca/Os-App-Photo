using System.Text.Json;
using Kazo.Os.AppSdk.Craft;
using Kazo.Photo.Documents;

namespace Kazo.Photo.Tests;

/// <summary>
/// The tabs of a Kazo Photo window and their files (doc/en/plan.md, work item 2, decisions 6 to 8,
/// Benoit's Q2 = A): what PhotoCraft lists, where a file opens, where each save goes, the title.
/// </summary>
public sealed class PhotoDocumentsTests
{
    private const string Untitled = "Untitled";

    [Fact]
    public void PhotoCraft_s_session_inspect_is_read_by_tab_index()
    {
        IReadOnlyList<OpenDocument> tabs = PhotoDocumentList.ListFrom(Inspect(
            """
            { "active": 1, "documents": [
                { "index": 0, "name": "beach", "path": "beach.png", "width": 640, "height": 400, "dirty": false, "revision": 3 },
                { "index": 1, "name": "Untitled-1", "path": null, "dirty": true },
                { "index": 2, "name": "poster", "path": "", "dirty": false } ] }
            """));

        Assert.Equal(
            [
                new OpenDocument(0, "beach.png", "beach", false, false),
                new OpenDocument(1, null, "Untitled-1", true, true),
                new OpenDocument(2, null, "poster", false, false),
            ],
            tabs);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("""{ "documents": 3 }""")]
    [InlineData("""{ "documents": [ 1, { "name": "no index" }, { "index": -1 }, { "index": "0" } ] }""")]
    public void A_malformed_answer_from_the_iframe_lists_nothing(string json)
    {
        Assert.Empty(PhotoDocumentList.ListFrom(Inspect(json)));
    }

    [Fact]
    public void A_tab_listed_twice_is_kept_once()
    {
        IReadOnlyList<OpenDocument> tabs = PhotoDocumentList.ListFrom(Inspect(
            """{ "active": 0, "documents": [ { "index": 0, "path": "a.psd" }, { "index": 0, "path": "b.psd" } ] }"""));

        OpenDocument tab = Assert.Single(tabs);
        Assert.Equal("a.psd", tab.Name);
    }

    [Fact]
    public void An_opened_layered_file_is_rewritten_by_Save()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);

        SaveDecision decision = documents.Decide("poster.psd", saveAs: false);

        Assert.Equal(SaveAction.Rewrite, decision.Action);
        Assert.Equal("/home/ana/Pictures/poster.psd", decision.Path);
    }

    [Fact]
    public void Save_as_always_asks_in_the_folder_of_the_file()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);

        SaveDecision decision = documents.Decide("poster.psd", saveAs: true);

        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Equal("/home/ana/Pictures", decision.Folder);
    }

    [Fact]
    public void A_PNG_that_gained_layers_asks_for_a_PSD_next_to_the_PNG()
    {
        // H3: the format can no longer hold the document, PhotoCraft proposes beach.psd (saveAs).
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/beach.png");
        documents.Update([Tab(0, "beach.png", active: true)]);

        SaveDecision decision = documents.Decide("beach.psd", saveAs: true);

        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Equal("beach.psd", decision.Name);
        Assert.Equal("/home/ana/Pictures", decision.Folder);
    }

    [Fact]
    public void After_Save_as_the_next_Save_writes_the_file_chosen()
    {
        // PhotoCraft has no rename command: its tab keeps the name it suggested, which is bound
        // to the file the user chose.
        PhotoDocuments documents = new();
        documents.Update([Tab(0, "Untitled-1.psd", active: true)]);
        SaveDecision first = documents.Decide("Untitled-1.psd", saveAs: true);
        documents.Saved(first, "/home/ana/Pictures/holiday.psd");

        SaveDecision next = documents.Decide("Untitled-1.psd", saveAs: false);

        Assert.Equal(SaveAction.Rewrite, next.Action);
        Assert.Equal("/home/ana/Pictures/holiday.psd", next.Path);
        Assert.Equal("holiday.psd — Kazo Photo", documents.Title(Untitled));
    }

    [Fact]
    public void A_cancelled_or_failed_save_asks_again_and_keeps_the_window_modified()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);
        SaveDecision decision = documents.Decide("poster.psd", saveAs: false);

        documents.SaveFailed(decision);

        Assert.True(documents.IsDirty);
        Assert.Equal(SaveAction.AskAndBind, documents.Decide("poster.psd", saveAs: false).Action);
        Assert.Single(documents.WithUnsavedWork());
    }

    [Fact]
    public void An_export_is_always_a_copy_in_the_folder_of_the_image_in_front()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);

        SaveDecision decision = documents.DecideCopy("poster.png");

        Assert.Equal(SaveAction.AskForCopy, decision.Action);
        Assert.Equal("/home/ana/Pictures", decision.Folder);
        Assert.False(decision.SavesTheDocument);
    }

    [Fact]
    public void A_new_file_opens_here()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);

        Assert.Equal(OpenPlace.Here, documents.PlaceOf("/home/ana/Pictures/beach.png"));
    }

    [Fact]
    public void A_file_already_open_brings_its_tab_to_the_front()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "beach.png", active: true), Tab(1, "poster.psd")]);

        Assert.Equal(OpenPlace.AlreadyOpen, documents.PlaceOf("/home/ana/Pictures/poster.psd"));
        Assert.Equal(1, documents.TabOf("/home/ana/Pictures/poster.psd")?.Id);
    }

    [Fact]
    public void Two_files_with_one_name_never_share_a_window()
    {
        // Decision 7: PhotoCraft would take the two tabs for one file.
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true)]);

        Assert.Equal(OpenPlace.NewWindow, documents.PlaceOf("/home/ana/Work/poster.psd"));
    }

    [Fact]
    public void A_name_a_new_image_already_saves_under_sends_the_file_to_a_new_window()
    {
        // A tab saved once under poster.psd holds the name, wherever its file is.
        PhotoDocuments documents = new();
        documents.Update([Tab(0, "poster.psd", active: true)]);

        Assert.Equal(OpenPlace.NewWindow, documents.PlaceOf("/home/ana/Pictures/poster.psd"));
    }

    [Fact]
    public void A_file_saved_by_Save_as_is_found_under_the_name_PhotoCraft_kept()
    {
        // The tab Untitled-1.psd writes holiday.psd: opening holiday.psd brings that tab back.
        PhotoDocuments documents = new();
        documents.Update([Tab(0, "Untitled-1.psd", active: true)]);
        documents.Saved(documents.Decide("Untitled-1.psd", saveAs: true), "/home/ana/Pictures/holiday.psd");

        Assert.Equal(OpenPlace.AlreadyOpen, documents.PlaceOf("/home/ana/Pictures/holiday.psd"));
        Assert.Equal(0, documents.TabOf("/home/ana/Pictures/holiday.psd")?.Id);
    }

    [Fact]
    public void A_file_named_like_the_file_of_a_tab_saved_by_Save_as_opens_in_a_new_window()
    {
        PhotoDocuments documents = new();
        documents.Update([Tab(0, "Untitled-1.psd", active: true)]);
        documents.Saved(documents.Decide("Untitled-1.psd", saveAs: true), "/home/ana/Pictures/holiday.psd");

        Assert.Equal(OpenPlace.NewWindow, documents.PlaceOf("/home/ana/Work/holiday.psd"));
    }

    [Theory]
    [InlineData("/home/ana/brushes.abr")]
    [InlineData("/home/ana/Gradients.GRD")]
    [InlineData("/home/ana/styles.asl")]
    [InlineData("/home/ana/swatches.aco")]
    [InlineData("/home/ana/table.act")]
    [InlineData("/home/ana/swatches.ase")]
    public void A_preset_file_goes_to_PhotoCraft_s_libraries_and_is_never_bound(string path)
    {
        PhotoDocuments documents = new();

        Assert.Equal(OpenPlace.Preset, documents.PlaceOf(path));
    }

    [Fact]
    public void The_title_names_the_file_of_the_tab_in_front_and_marks_unsaved_work()
    {
        PhotoDocuments documents = new();
        documents.Opened("/home/ana/Pictures/poster.psd");
        documents.Update([Tab(0, "poster.psd", active: true), Tab(1, null, title: "Untitled-1", dirty: true)]);

        Assert.Equal("*poster.psd — Kazo Photo", documents.Title(Untitled));

        documents.Update([Tab(0, "poster.psd"), Tab(1, null, title: "Untitled-1", active: true)]);
        Assert.Equal("Untitled-1 — Kazo Photo", documents.Title(Untitled));
    }

    [Fact]
    public void The_title_is_the_app_s_name_on_the_home_screen_and_before_PhotoCraft_runs()
    {
        PhotoDocuments documents = new();

        Assert.Equal("Kazo Photo", documents.Title(Untitled));
        Assert.Equal("Kazo Photo", documents.Title(Untitled, running: false));
    }

    [Fact]
    public void The_dirty_event_alone_marks_the_window_modified()
    {
        PhotoDocuments documents = new();
        documents.SetFrameDirty(true);

        Assert.True(documents.IsDirty);
        Assert.Equal("*Kazo Photo", documents.Title(Untitled));
    }

    [Fact]
    public void The_close_guard_lists_every_modified_tab_in_PhotoCraft_s_order()
    {
        PhotoDocuments documents = new();
        documents.Update([Tab(0, "a.psd", dirty: true), Tab(1, "b.psd"), Tab(2, null, title: "Untitled-2", dirty: true, active: true)]);

        Assert.Equal([0L, 2L], documents.WithUnsavedWork().Select(tab => tab.Id));
    }

    private static OpenDocument Tab(long index, string? path, string? title = null, bool dirty = false, bool active = false) =>
        new(index, path, title ?? Path.GetFileNameWithoutExtension(path ?? "Untitled"), dirty, active);

    private static JsonElement Inspect(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
