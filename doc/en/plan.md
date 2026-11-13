> [Version française](../fr/plan.md)

# Plan: the Kazo Photo app for Kazo OS

Step 0 of the Kazo OS [Craft app port guide](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/craft-app-port-guide.md),
written on 2026-11-13 (`TODO` issue [#1](https://github.com/Kazo-ca/Os-App-Photo/issues/1)).
This document sets the goal, what reading PhotoCraft showed, the decisions, the questions asked to
Benoit and the work items. Each work item is written so that it can be handed as is to an
implementation thread. The closest references are Kazo Fig
([plan](https://github.com/Kazo-ca/Os-App-Fig/blob/main/doc/en/plan.md), several documents in
tabs) and Kazo Pdf ([plan](https://github.com/Kazo-ca/Os-App-PDF/blob/pdf/base/doc/en/plan.md),
exports received as copies).

## Goal

Kazo Photo is **the image editor of Kazo OS**: layers, masks, adjustments, filters, type, `.psd`
files. It wraps the web build of [PhotoCraft](https://github.com/storytold/photocraft) (ArtCraft
team, `MIT OR Apache-2.0`, Rust and egui, alpha) and connects it to the files, dialogs and
associations of Kazo OS. Kazo Paint, the base app, stays the simple tool; Kazo Photo is the full
one.

**What sets this port apart**: PhotoCraft has **neither host mode (`craft-web-host`) nor a command
API (`craft-web-api`)**. WordCraft, GridCraft, PdfCraft and FigCraft had them; here they have to
be written, in our fork [`Kazo-ca/photocraft`](https://github.com/Kazo-ca/photocraft), before the
Kazo window can open or save anything. Kazo Photo is therefore a **modified** build compiled from
the fork from its first version; there is no "official zip" stage.

The common Kazo code (iframe, proxy, file picker, shortcuts, settings, `DocumentSession`, exports)
comes from the `Kazo.Os.AppSdk.Craft` module of Kazo OS
([ADR 0259](https://github.com/Kazo-ca/Os/blob/main/doc/en/wasm/dev/adr/0259-shared-craft-app-host-module.md),
[ADR 0264](https://github.com/Kazo-ca/Os/blob/craft-exports/base/doc/en/wasm/dev/adr/0264-craft-app-exports-reach-the-window.md)).

## Reading PhotoCraft (step 0, 2026-11-13)

Read on `main` of the fork (commit `e052eab`, 2026-11-13, version 0.23.1) and compiled from that
commit. The upstream tag `v0.23.1` is commit `45066c48` of the `release` branch, which the fork
does not have yet.

| Point | Finding |
| --- | --- |
| Web build | `apps/photocraft-web` (Trunk 0.21.14, `public_url = "./"`, Cargo profile `wasm-release`, feature `heif`), eframe on **wgpu**: WebGPU, WebGL 2 fallback (`?webgl`), `?cpu` for the canvas without GPU. Rust 1.95 at least. `craft-i18n` languages embedded in the `.wasm` (revision `e02939e`, file `craft-i18n.rev`). |
| Host mode | **Missing.** No dependency on `craft-web-host`; `apps/photocraft-web/src/web.rs` only knows the browser: Open = browser picker (`rfd`), Save = download. |
| Command API | **Missing** in the web build (`window.photocraft` does not exist), but everything it needs is there: `PhotocraftApp::with_control` takes a queue of `ControlRequest`, the one the desktop TCP server feeds (`docs/control-protocol.md`). Only the `craft-web-api` wiring is missing, like `apps/gridcraft-web/src/api.rs` (150 lines). |
| Canvas | `photocraft_canvas`, hard-coded: one instance per HTML document, so one iframe per window. The page also holds `#photocraft_loading` and `#photocraft_stopped` (the message shown when the module stops, for example out of memory). |
| Official zip | `photocraft-web-<version>.zip` (`packaging/web/package.sh`): the `.wasm` is gzipped and cut into 20 MiB parts, started by `photocraft-web-boot`. Of no use here: no host mode, and the Kazo module expects one `.js` and one `.wasm`. We take Trunk's direct output (`dist/web`). |
| Size | `.wasm` of **37.6 MiB** after `wasm-opt`, `.js` glue of 179 KiB (measured, see "Trial"). Under the Kazo OS limits (128 MiB per file; package of 64 MiB compressed, 128 MiB extracted) and under GitHub's 50 MB warning. |
| Saving | Everything goes through `Services::write(path, bytes)`: the document (Save, Save As), but also copies and exports (Save a Copy, Export As, Quick Export as PNG, Save for Web, Layers to Files) and preset files (swatches, actions, shortcuts, contours, colour tables…), about fifteen call sites. The function does not know whether it writes the document or a copy; the app does (`write_document(…, copy)`). |
| Name on save | A file opened in the browser has no path (`path = None`). Save then always proposes `name.psd`, even for a one-layer JPEG. With a path (`menus/invoke.rs`, `file.save`): a layered file that holds the whole document (`.pcraft`, `.psd`, `.psb`, `.tif`) is rewritten in place; for a flat image, Save goes back through the name request and proposes its own file (`.png` for a JPEG that became transparent); when the format can no longer hold the document, it proposes `.psd` and the old format becomes a copy, like Photoshop. |
| "Modified" | On the web, `write_unconfirmed = true`: after a download the document **stays modified** (the browser confirms nothing) and the leave-page warning stays armed. |
| Documents | Several documents in **tabs** in one page (File › New, Open with several files, dragging a layer from one tab to another). Command `session.inspect`: `{ active, documents[{ index, name, path, width, height, dirty, revision }] }`. No stable identifier in that list (only the tab's position). `ui.inspect` does not list the documents. |
| Two tabs, one file | `detach_others_from_file`: when a tab saves under the path of another tab, the other one loses its file and becomes "modified". In the browser paths are plain names: two files of the same name from two folders would be taken for the same file. |
| Commands | `engine.execute { command, params }` (no dialog), `ui.menu.invoke` (like a menu click), `engine.commands`, `ui.inspect`, `ui.set`, `ui.screenshot`, `ui.dialog.*`. File: `file.new`, `file.open`, `file.save`, `file.saveAs`, `file.saveACopy`, `file.close`, `file.closeAll`, `file.revert`, `file.export.*`, `file.print`. Documents: `session.inspect`, `document.inspect`, `document.activate`. `app.open` and `app.save` need file access the web does not have. |
| Theme | `ui.set { theme }`: `pro` (dark, default), `proMedium`, `proLightGray`, `proLight`, `studio`, `studioLight`, `classic`, `adwaita`, `adwaitaDark`, `solarizedDark`, `breezeLight`, `breezeDark`, `softDark`, `softLight`. |
| Language | Preference `interface.language` (`prefs.set`), `auto` by default (the browser's languages). 35 languages have a PhotoCraft catalog: `ar az bn bs cs da de el es fi fr he hi hu id it ja ka ko nb nl pl pt-br ro ru sr-latn sv ta th tr uk ur vi zh-hans zh-hant`. |
| Browser picker (`rfd`) | About ten uses: open (one or several files), place embedded, files of a stack, LUTs and presets (`.abr` brushes, `.grd` gradients, `.asl` styles, swatches), actions, a smart object's SVG. All go through the same `input type=file`: the module's picker replaces them. **Folder** requests already answer "nothing" on the web. |
| Formats opened | `pcraft psd psb psdt xcf xcfgz ora kra krz pdn af afphoto afdesign afpub` (layered documents), `png jpg jpeg gif bmp webp tif tiff tga ico qoi exr hdr pbm pgm ppm pam pfm dds fits fit fts dpx cin dcm dicom dc3 iff lbm ilbm heic heif hif svg svgz` (images), `dng cr2 cr3 nef nrw arw pef orf rw2 raf` (RAW), and the presets `abr grd asl aco act ase`, which go to the libraries instead of opening a document. |
| Formats saved | Document: `.pcraft` (native), `.psd`, `.psb`, `.tif`; `.ora` when it holds the whole document; a flat format (`png jpg webp gif bmp tga qoi exr hdr…`) only for a flat document. |
| Settings | `localStorage`, key `photocraft.preferences`: the module keeps them per Kazo user. **Presets** (brushes…) are in IndexedDB: shared by every Kazo user of the same browser (the same limit as Kazo Word's added fonts). |
| Fonts | The web build only has **Inter and JetBrains Mono**. More fonts can be "served": a `fonts/manifest.txt` file **relative to the page** (the `craft-fonts` format), each family downloaded on first use. All of `craft-fonts` weighs 82 MB (55 MB of it CJK fonts). Question Q4. |
| Memory | wasm32: 4 GiB at most per page, so per window. PhotoCraft checks the room before Place, Image Size and every save, warns within 512 MiB of the ceiling, and shows "PhotoCraft stopped" if the module stops. |
| Leaving the page | `beforeunload` warning while work is unsaved; the Kazo window's close guard runs first. |
| Screen eyedropper, clipboard, pen | Browser APIs (`EyeDropper`, eframe's clipboard, Pointer Events): they work in the iframe, which the module opens with `clipboard-read` and `clipboard-write`. |
| Window size | Desktop: 1440 × 900, minimum 760 × 480. |
| Brand | The ArtCraft name and logos are trademarks (`docs/brand/LICENSE-brand.txt`): a modified build must remove them and must not present itself as an ArtCraft product; "based on PhotoCraft by the ArtCraft team" in plain text is allowed. The "ArtCraft" link of the About window carries the name; the home screen carries three links to ArtCraft and an icon to check (see "Trial"). |
| Upstream repository rules | `AGENTS.md`: Rust only, no `unwrap`/`panic` outside tests, everything is a command, never break wasm (`cargo xtask wasm`), no CI change in a feature pull request, no agent attribution in a pull request, an issue before a pull request. |

### Trial (2026-11-13)

Run on the fork (`main`, e052eab, v0.23.1), in this session's container (4 cores, Linux), with no change to the code.

| Point | Result |
| --- | --- |
| Tools | Rust stable 1.100.0, target `wasm32-unknown-unknown`, Trunk 0.21.14, `craft-i18n` at the pinned revision (`craft-i18n.rev`, `CRAFT_I18N_REQUIRED=1`). `craft-fonts` was not provided. |
| Command | `trunk build --release` in `apps/photocraft-web` (profile `wasm-release`, feature `heif`). |
| Duration | 13 minutes from cold: 9 min of `cargo`, then 3 min 30 of `wasm-opt -Oz --converge`. |
| Output | `dist/web`: `index.html`, a `.js` of 179 KiB, a `.wasm` of 37.6 MiB (39.4 MB; 51.8 MB before `wasm-opt`; 16.0 MB gzipped), a `snippets/` folder. |
| Start | Chromium with no graphics card (WebGL2 through SwiftShader, `?webgl`): the app starts in under a second from a local server and shows its home screen. It falls back to CPU rendering by itself ("CPU Rendering" in the status bar). |
| Open | The "Open…" button opens the browser file picker. A 640 × 400 PNG opens in a tab. |
| Save | Ctrl+S on that PNG: `sample.psd` is downloaded, with no prompt, and the tab takes that name. This is what H2 fixes: the opened file has no path. |
| Page API | `typeof window.photocraft` is `undefined`. |
| Storage | One `photocraft.preferences` key in `localStorage`. |
| Console | One 404 request (`fonts/manifest.txt`, served fonts absent), harmless. |

Two things seen on screen: the home screen carries ArtCraft's "Website", "Discord" and "GitHub" links, the PhotoCraft name and an icon at the top left (H9, in the fork); the 1280 × 820 window is well filled, which confirms a large starting size.

Screenshots: `photo/trials/` in the project files (outside the repository).

## What has to be added to PhotoCraft (fork)

The contract is `craft-web-host`'s (`storytold/craft-libs`, version "1"), the same as the other
Craft apps, with the `photocraft:` prefix. The code lives in `apps/photocraft-web`, on the model of
`apps/gridcraft-web/src/{api,host,hostmode}.rs`; two points also need a small opening in
`crates/ui-egui` (marked ★), because the web shell cannot see what it needs today.

| # | Behaviour in host mode (`data-host="page"` or `?host=parent`) | Why |
| --- | --- | --- |
| H1 | `photocraft:ready { version: "1", appVersion, file: false }` once the app listens; `photocraft:failed { error }` if it cannot start, **and when the module stops** (the handler that already shows "PhotoCraft stopped"). | The Kazo window knows when to send the file and when to show an error. |
| H2 | `photocraft:open { name, bytes }` opens the file like File › Open, in a new tab, refused above a limit before it is copied. ★ The document opened by the host **keeps its name as its path** (`path = Some(name)`). | With a path, Save rewrites the same file when its format allows it, instead of always proposing `.psd`. |
| H3 | ★ A save **of the document** (`copy == false`) goes out as `photocraft:save { name, bytes, saveAs, id }`. `saveAs` is false when the document is written under its own path (rewritten in place, or Save of a flat image when the proposed name is its own file), true otherwise (Save As, first save, a format that can no longer hold the document). | The host knows whether it rewrites the bound file or must ask where to save. |
| H4 | The document is marked saved only when `photocraft:saved { id }` arrives; with no answer it stays modified. | Fixes for PhotoCraft the "the app says it is clean before the write" pitfall (craft-libs #611). |
| H5 | **Copies, exports and preset files** keep the download (`<a download>`). | The Kazo module already intercepts them and hands them to the window as copies (ADR 0264); nothing to invent. |
| H6 | `photocraft:dirty { dirty }` when one of the open documents has changes, or none has any more. | Window title. |
| H7 | `window.photocraft` (`craft-web-api`: `request`, `execute`, `commands`, `docInspect`, `uiInspect`, `screenshot`) on the app's control queue, installed with `?control` like PdfCraft and FigCraft. A call the page gave up on is not run (`Dispatch::give_up`). | Theme, language, document list, close guard. |
| H8 | Outside host mode nothing changes (downloads, browser picker). | The patch stays proposable upstream. |
| H9 | The "ArtCraft" link of the About window becomes the text the brand licence allows. In host mode the home screen does not show the "Website", "Discord" and "GitHub" links; the icon at the top left is checked against `docs/brand/` and removed if it is a mark. | Modified build. |

Tests asked in the fork: unit tests of the pure rules (default name, `saveAs`, size limit),
`cargo clippy -D warnings`, `cargo xtask layers`, `cargo xtask wasm`, a Trunk build, and a
Playwright journey on a test page that plays the host (`open`, `save`, `saved`, `dirty`, `failed`,
`window.photocraft.request`).

## Decisions

| # | Subject | Decision |
| --- | --- | --- |
| 1 | Name and identity | "**Kazo Photo**". Id `ca.kazo.photo`, publisher `pub.kazo`, assembly `Kazo.Photo`, Kazo Fig's category, several windows, window of 1280 × 820, minimum content 760 × 480. |
| 2 | Common code | The `Kazo.Os.AppSdk.Craft` module. Minimum version: **the Kazo OS version that contains `craft-exports/base`** (ADR 0264), because H5 depends on it; not published yet, Benoit decides when. Until then the OS submodule follows `craft-exports/base`, like Kazo Pdf. |
| 3 | Changing screen | `canChangeScreen: false` in the manifest from work item 1 (issue [#2](https://github.com/Kazo-ca/Os-App-Photo/issues/2), ADR 0263). |
| 4 | Where the build comes from | Always compiled from the fork: branch `kazo/v0.23.1` (= upstream tag `v0.23.1`, commit `45066c48`), integration branch `kazo/base` created from it, which receives the fork's work items. Submodule `external/photocraft` on `kazo/base`. Trunk's output (`dist/web`) copied into `wwwroot/photocraft/` with `photocraft.json` (version, commit, names and SHA-256), in the repository: the Store compiles no Rust. |
| 5 | Iframe page | `wwwroot/host/index.html`: canvas `photocraft_canvas` with `data-host="page"`, `data-craft-app="photocraft"`, `data-craft-build="../photocraft/photocraft.json"`, plus the `#photocraft_loading` and `#photocraft_stopped` elements of the upstream page. Parameter `control` for `window.photocraft`. |
| 6 | Saving | `Saving`: the module's rule (`DocumentSession.Decide(name, saveAs)`); the window returns `true` only once the file is written, which triggers `saved` (H4). `Exporting`: always a copy (`DecideCopy`). |
| 7 | Two files of the same name | A name is bound once per window. Opening a file whose name is already bound in this window opens it in a **new window**: PhotoCraft would otherwise take both tabs for the same file. |
| 8 | Close guard | Read from `session.inspect` (`documents[{ index, name, path, dirty }]`); the window activates each modified document (`document.activate`), runs `file.save` and waits for its own writes to finish. |
| 9 | Theme and language | `ui.set { theme }`: `pro` for the OS's dark theme, `proLight` for the light one, on every change. Language: `prefs.set` of `interface.language` at start (`CraftLanguage.Pick` over the 35 codes). |
| 10 | Shortcuts | `ReservedKeys.Os` **without** `Ctrl+N`: in a tabbed image editor, `Ctrl+N` is PhotoCraft's "New document". Conflicts between the OS chords (`Alt+F3`…`F11`, `Alt+Space`) and PhotoCraft's are listed in work item 2 and noted in the listing. |
| 11 | Large files | The Kazo OS limit (128 MiB per file, ADR 0245) applies; a larger file is refused with a message. Work item 2 measures opening and saving a `.psd` of 50 to 100 MB (time, memory) and records the result. |
| 12 | Upstream track | In parallel with the fork: the issue texts (in English, no agent mention) are prepared in `/mnt/project-files/photo/upstream/`; Benoit publishes them. The fork branches are written so that they can become upstream pull requests. |
| 13 | Kazo OS ADR | None planned: the module already has what is needed. If a work item needs one, the next free number is **0267** as of 2026-11-13 (check `main`, the open pull requests and every `*/base` branch again when writing it). |

## Questions asked to Benoit

Asked in the thread on 2026-11-13. **Benoit answered A to the four questions on 2026-11-13**: host mode in our fork and proposed upstream (Q1), PhotoCraft's tabs and a new window on double-click (Q2), default app for what nothing else opens (Q3), the Latin fonts of `craft-fonts` served on demand (Q4).

**Q1. Where does PhotoCraft's host mode live?**

- **A (recommended)**: in our fork now, and proposed upstream in parallel (Claude prepares the
  issue and the branches, Benoit publishes). Kazo Photo waits for nobody; if upstream accepts, the
  fork becomes a plain copy again.
- B: upstream only. Nothing to maintain, but Kazo Photo waits for the answer and for an upstream
  release, with no date.
- C: in the fork only, proposing nothing. Every PhotoCraft version bump means carrying the patch
  over, forever.

**Q2. Several images in one window?**

- **A (recommended)**: keep PhotoCraft's tabs. A Kazo Photo window holds several images, each tab
  bound to its file (like Kazo Fig). A double-click on a file in the File Explorer opens a new
  window. Dragging layers between images keeps working, and an image does not cost a whole
  PhotoCraft instance (several hundred MB of memory).
- B: one image per window, like Kazo Word. A simpler save rule, but each image costs an instance,
  and the features that work across documents lose their point.
- C: like A, and a double-click in the File Explorer adds a tab to the window already open. Closer
  to Photoshop, but the OS has to route an open request to an existing window: one more work item
  in `Kazo-ca/Os`.

**Q3. Formats associated with Kazo Photo.** Kazo Paint already opens `.png .jpg .jpeg .bmp .gif .webp`.

- **A (recommended)**: default app for what nothing else opens: layered documents
  (`.pcraft .psd .psb .xcf .ora .kra .pdn`, Affinity files), RAW, and the images Kazo Paint does
  not read (`.tif .tiff .tga .exr .hdr .heic .heif .dds .qoi .ico`…). "Open with" only for
  `.png .jpg .jpeg .bmp .gif .webp .svg`: a double-click on a photo still opens the light app.
- B: Kazo Photo by default for every image, once installed. A double-click on a PNG then starts a
  heavy editor that takes several seconds to load.
- C: default only for `.pcraft .psd .psb`, everything else through "Open with".

**Q4. Fonts of the Type tool.** The web build only has Inter and JetBrains Mono.

- **A (recommended)**: ship the Latin fonts of `craft-fonts` in the package (about 25 MB, OFL
  licences), served on demand: a family is loaded on first use only. No CJK fonts in v1 (55 MB
  more).
- B: no extra font in v1; limit written in the listing.
- C: all of `craft-fonts` (82 MB). The package would pass 100 MiB extracted, close to the 128 MiB
  limit of Kazo OS.

## Work items

Two repositories: the fork `Kazo-ca/photocraft` (Rust) and `Kazo-ca/Os-App-Photo` (C#). In each,
one integration branch, one branch and one pull request per work item; one implementation thread
per work item. Benoit merges `photo/base` into `main`; the fork's `main` stays the copy of
upstream.

| No. | Repository, branch (to) | Content | Depends on |
| --- | --- | --- | --- |
| 0 | Os-App-Photo, `photo/0-plan` (`photo/base`) | this plan (fr and en) | — |
| F1 | photocraft, `kazo/1-web-api` (`kazo/base`) | H7: `window.photocraft` | plan merged |
| F2 | photocraft, `kazo/2-web-host` (`kazo/base`) | H1 to H6, H8, H9: host mode | F1 (same files) |
| 1 | Os-App-Photo, `photo/1-solution` (`photo/base`) | solution, manifest, build from the fork, iframe page, a window that starts | plan merged; "done" waits for F2 |
| 2 | Os-App-Photo, `photo/2-window` (`photo/base`) | open, save, tabs, exports, close guard, picker, shortcuts, theme, language, settings, fonts (Q4) | 1 and F2 |
| 3 | Os-App-Photo, `photo/3-store` (`photo/base`) | listing, README, licences, measures, tag, catalog, `features.md` | 2, and the Kazo OS version that contains `craft-exports/base` |

**Order**: F1 then F2 in one thread (they touch the same files); work item 1 in a second thread,
at the same time; then 2; then 3.

### F1. `window.photocraft` (fork)

- Create `kazo/v0.23.1` on upstream commit `45066c48` (`git fetch https://github.com/storytold/photocraft refs/tags/v0.23.1`), then `kazo/base` from that branch.
- `apps/photocraft-web/src/api.rs` on the model of `apps/gridcraft-web/src/api.rs`: a `ControlRequest` queue given to the app through `PhotocraftApp::with_control`, `craft_web_api::page::PageApi::new("photocraft", …).install()`, installed with `?control`. A call the page gives up on releases the request (`Dispatch::give_up`).
- `craft-web-api` pinned to a revision of `storytold/craft-libs`, like the other `craft-*` dependencies of the root `Cargo.toml`.
- **Done when**: in a page served by `trunk serve`, `await window.photocraft.request("engine.execute", { command: "session.inspect" })` answers, `ui.set { theme: "proLight" }` changes the theme, and the checks of the "What has to be added" section pass.

### F2. Host mode (fork)

- `apps/photocraft-web/src/host.rs` and `hostmode.rs` on GridCraft's model: `Host::connect("photocraft", canvas)`, listening for `open` and `saved`, `post_ready`, `post_save`, `post_dirty`, `post_failed`.
- The two ★ openings in `crates/ui-egui`, as small as possible and useful outside Kazo: a file handed over with its path (H2), and the document write told apart from the write of a copy (H3), with the confirmation (H4) in place of `write_unconfirmed` in host mode. Read `app/files.rs` (`save_dialog_flow`, `write_document`, `saved`, `saves_copy`) and `file_open.rs` before choosing the shape.
- H9, and a line in `docs/development.md` ("Web build") that describes host mode.
- **Done when**: the test page that plays the host opens a PNG and a PSD, receives `save` with the right `saveAs` in the four cases (rewriting a PSD, a PNG that stayed flat, a PNG that became layered, Save As), the document becomes clean only after `saved`, Export As still downloads, and the same page without `data-host` behaves as before.

### 1. Solution and build (`photo/1-solution`)

- Solution renamed (`Kazo.Photo`, `Kazo.Photo.Os`, `Kazo.Photo.Tests`), the `[AppManifest]` of decision 1, `canChangeScreen: false`, Kazo Fig's capabilities, associations per Q3.
- Submodule `external/KazoOs` on `craft-exports/base` (CI: `KAZOOS_BRANCH`), submodule `external/photocraft` on `kazo/base`.
- `tools/build-photocraft.sh` (today's stable Rust, target `wasm32-unknown-unknown`, Trunk 0.21.14, `craft-i18n` at the revision of `craft-i18n.rev`, `CRAFT_I18N_REQUIRED=1`, `trunk build --release` in `apps/photocraft-web`), `tools/vendor-photocraft.sh --dir`, workflow `build-photocraft.yml`, xUnit test "the commit of `photocraft.json` is the submodule's". Models: `Os-App-Fig` and `Os-App-PDF`.
- Iframe page (decision 5) and a window on `CraftFrame` and `FrameStartup`: loading, app, error, "Retry".
- Until F2 is merged, compile `kazo/v0.23.1` to break in the tooling; the journey that waits for `ready` comes with F2.
- **Done when**: the app starts in WebGPU and in WebGL 2, two windows run together, the `.kazoapp` package installs in a published Kazo OS; package size and memory per window recorded here.

**Done on 2026-11-13** (PR `photo/1-solution` → `photo/base`), with the fork still on `kazo/v0.23.1` (F2 not merged):

| Point | Result |
| --- | --- |
| Solution | `Kazo.Photo.slnx`: `Kazo.Photo` (app), `Kazo.Photo.Os` (host), `Kazo.Photo.Tests` (xUnit), `tests/e2e` (Playwright). |
| Manifest | `ca.kazo.photo` 0.1.0, `pub.kazo`, `graphics`, several windows, 1280 × 820, minimum 762 × 520 (760 × 480 of content plus the frame), `canChangeScreen: false`, Kazo Fig's capabilities, minimum version 3.3.3. |
| Associations (Q3 = A) | `open` and `edit` for layered documents, RAW files, the images Kazo Paint does not read and the presets (`.abr .grd .asl .aco .act .ase`); `open` alone for `.png .jpg .jpeg .bmp .gif .webp .svg`. `.svgz` and `.svg` have two media types, so no type mixes both cases (`Documents/PhotoFormats.cs`). |
| Submodules | `external/KazoOs` on `craft-exports/base` (`c91effd`, CI: `KAZOOS_BRANCH`, `KAZOOS_USE_SOURCE`), `external/photocraft` on `kazo/v0.23.1` (`45066c48`). |
| Build tooling | `tools/build-photocraft.sh`: Trunk 0.21.14 directly (not `packaging/web/package.sh`, which cuts the `.wasm` into parts), `craft-i18n` required at the revision of `craft-i18n.rev`. 16 minutes from scratch here (4 cores, Rust 1.97.0). Workflow `build-photocraft.yml` on the model of Kazo Pdf. |
| Build | `.wasm` of 39,250,977 bytes (37.4 MiB), `.js` of 182 KB, plus the licences (MIT, Apache, `NOTICE`, `ATTRIBUTION.md`, OFL of Inter and JetBrains Mono). `.kazoapp` package (`-t:KazoOsPack`): **16.2 MB**. |
| Window | `CraftFrame` and `FrameStartup`: loading screen, error with "Retry" after 60 s, theme (`pro`, `proLight`) and language (`prefs.set interface.language`) on `ready`, `ReservedKeys.Os` without `Ctrl+N` (decision 10). Iframe page with `#photocraft_loading` and `#photocraft_stopped` (`host/photocraft-page.css`). |
| Browser | In Kazo OS (Chromium with no graphics card), PhotoCraft starts in each window's iframe, two windows together, and draws its home screen on WebGL 2 (`--webgl`). On software WebGPU (SwiftShader), PhotoCraft loses its device right after starting ("A valid external Instance reference no longer exists"): a limit of that browser, **to check on a real graphics card**. |
| Memory | Chromium renderer processes, no image open: 768 MiB for the OS alone, 886 MiB with one Kazo Photo window, 930 MiB with two. To measure again with a `.psd` in work item 2 (decision 11). |
| Tests | 69 xUnit tests; 3 Playwright journeys green, the 4th ("the window shows PhotoCraft once it is ready") marked `todo` until F2. |

**Left for "done"**, after F2: move `external/photocraft` to `kazo/base`, rebuild, remove the `todo` of the `ready` journey, check WebGPU on a real graphics card and install the `.kazoapp` in a published Kazo OS. As long as PhotoCraft does not send `photocraft:ready`, the window stays on "Starting PhotoCraft…" then shows the error after 60 s, while PhotoCraft runs behind it.

### 2. Window (`photo/2-window`)

- Opening: the file asked at launch, `OpenAsync`, decision 7. Presets (`.abr .grd .asl .aco .act .ase`): sent to PhotoCraft with no binding.
- Saving: decision 6; format list (`CraftDocumentFormats`); the four cases of F2 end to end in Kazo OS; a cancelled dialog or a refused write leaves the document modified.
- Tabs per Q2: each name bound to its path, window title on the active tab, close guard (decision 8).
- Exports as copies (`Exporting`), file picker (`PickRequested`, one or several files), shortcuts (decision 10), theme and language (decision 9), per-user settings (`WebStorageFile`), served fonts per Q4 (`host/fonts/manifest.txt`, licences in `THIRD-PARTY-NOTICES`).
- The measure of decision 11.
- **Done when**: no case loses changes unless the user chose to, no browser dialog or download comes out of the app, and the OS shortcuts work with the cursor in the image.

### 3. Store (`photo/3-store`)

- Icon without the ArtCraft logo, listing in fr and en ("based on PhotoCraft by the ArtCraft team", known limits: 128 MiB per file, memory, presets shared between users, fonts), screenshots, README rebuilt from the listing (`store:readme`, "Try it" with `?autolaunch=ca.kazo.photo`), `THIRD-PARTY-NOTICES`.
- OS submodule on the tag of the version that contains `craft-exports/base`, `minimumVersion`, journey in a published Kazo OS, "Tag a release" workflow, pull request to the `Os-AppMarketPlace` catalog, `features.md` row in `Kazo-ca/Os`.
- **Done when**: Kazo Photo installs from the Store, opens a `.psd` on double-click and saves it.

## Upstream track

Drafts in English, published by Benoit, in `/mnt/project-files/photo/upstream/`:

- PhotoCraft: host mode of the web build (`craft-web-host`), with behaviours H1 to H6;
- PhotoCraft: `window.photocraft` (`craft-web-api`);
- `craft-libs`: join #611 (save confirmation) and #610 (`open-request`).

Not checked: the issues already open upstream on these subjects (`storytold` is out of the
session's reach); Benoit looks before publishing. DesignCraft and VectorCraft have the same gap:
what is done here is their model.

## Progress

- [x] Step 0: reading PhotoCraft 0.23.1, building and trying the web build.
- [x] Plan in fr and en.
- [x] Benoit's answers to Q1 to Q4: A everywhere (2026-11-13).
- [ ] F1, F2 (fork).
- [x] Work item 1: solution, manifest, build from the fork, iframe page, window (2026-11-13); fully "done" after F2.
- [ ] Work items 2 and 3.
