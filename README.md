> [Version française](readme-fr.md)

# Kazo Photo

The image editor of Kazo OS (layers, masks, adjustments, filters, `.psd` files), built on [PhotoCraft](https://github.com/storytold/photocraft) (ArtCraft team), compiled from our fork [`Kazo-ca/photocraft`](https://github.com/Kazo-ca/photocraft). Work in progress: see the [plan](doc/en/plan.md). This repository was made with the `kazoos-solution` template; the Store listing replaces this page at the Store work item.

| Folder | Content |
| --- | --- |
| `src/Kazo.Photo` | The app (window app, its `[AppManifest]` class generates `manifest.json`) |
| `src/Kazo.Photo.Os` | A host that runs the whole Kazo OS with the base apps and this app: the project to start |
| `tests/Kazo.Photo.Tests` | Tests of the app (xUnit) |
| `tests/e2e` | End-to-end runs in the Kazo OS host (Playwright): `npm run test:e2e` after `dotnet build Kazo.Photo.slnx -c Debug` |
| `external/KazoOs` | The Kazo OS repository as a git submodule; until the Kazo OS release with ADR 0264, on a commit of its `craft-exports/base` branch |
| `external/photocraft` | PhotoCraft's sources (our fork `Kazo-ca/photocraft`) as a git submodule, only to compile PhotoCraft (`tools/build-photocraft.sh`, workflow Build PhotoCraft) |
| `src/Kazo.Photo/wwwroot/photocraft` | The PhotoCraft web build and its provenance `photocraft.json`, written by `tools/build-photocraft.sh`, never edited by hand |
| `store/` | What the Kazo OS Store shows: icon (PNG 256×256), short descriptions, descriptions, `store.json` |

## Compiling PhotoCraft

`tools/build-photocraft.sh` compiles the commit of `external/photocraft` with Trunk 0.21.14, Rust
stable and the `wasm32-unknown-unknown` target, with the translations of `craft-i18n` at the
revision PhotoCraft pins (about 13 minutes from scratch on 4 cores), then replaces
`src/Kazo.Photo/wwwroot/photocraft/` with the result and its provenance. The **Build PhotoCraft**
workflow (Actions tab) does the same and opens a pull request with the build. To move to another
PhotoCraft commit: move the submodule to a commit of the fork, then rebuild; a test fails while
the build and the submodule disagree.

## First steps

```bash
# A repository created from the GitHub template already has the submodule:
git clone --recurse-submodules <your repository>

# A folder made with "dotnet new kazoos-solution" adds it once:
git init
git submodule add --depth 1 https://github.com/Kazo-ca/Os.git external/KazoOs
git -C external/KazoOs checkout v<Kazo OS version>
```

Then open `Kazo.Photo.slnx` in Visual Studio or Rider and start `Kazo.Photo.Os`, or run
`dotnet run --project src/Kazo.Photo.Os`.

## The Kazo OS version

`external/KazoOs/wasm2/KazoOs/build/Versions.props` is the Kazo OS version of this repository.
To move to another version, move the submodule to its tag and commit it:

```bash
git -C external/KazoOs fetch --depth 1 origin tag v2.2.0
git -C external/KazoOs checkout v2.2.0
git add external/KazoOs
```

Never edit the submodule: the OS is not modified from an app repository, and CI refuses a
submodule with local changes or off its tag.

## Two build profiles

| Profile | Kazo OS comes from | Use |
| --- | --- | --- |
| `Debug` | the projects of the submodule (`KazoOsUseSource=true`) | step into the SDK and the OS, breakpoints anywhere |
| `Release` | the `Kazo.Os.*` packages of nuget.org, at the submodule's version | CI, `dotnet publish`, the Kazo OS Store |

Force either one with `-p:KazoOsUseSource=true` or `false`.

## Publishing to the Kazo OS Store

1. Fill `store/` (icon, short descriptions, descriptions, category and tags in `store.json`).
2. Change `Version` in the app's `[AppManifest]` class and commit it to `main`, then tag it: run the
   **Tag a release** workflow (Actions tab, or the GitHub API for an agent), which tags the head of
   `main` with the manifest's version and starts the two publishing workflows; or push the tag
   yourself: `git tag v1.0.0 && git push --tags`.
3. The **Publish to the Kazo OS Store** workflow builds the app and makes the catalog folder
   `apps/<appId>_<version>/`. With a `HACK_STORE_TOKEN` secret (a token allowed to fork and open
   pull requests), it opens the pull request to `Kazo-ca/Os-AppMarketPlace` from your fork;
   without it, download the run's artifact and open the pull request yourself.

`HACK_STORE_TOKEN` is a GitHub personal access token of yours, not something Kazo OS gives you:

1. GitHub ▸ **Settings** ▸ **Developer settings** ▸ **Personal access tokens** ▸ **Tokens (classic)** ▸
   **Generate new token (classic)**, with a short expiration and the **`repo`** and **`workflow`**
   scopes (a fine-grained token cannot cover both your fork and the catalog's repository).
2. Copy it (shown once) into this repository: **Settings** ▸ **Secrets and variables** ▸ **Actions** ▸
   **New repository secret**, name `HACK_STORE_TOKEN` (or `gh secret set HACK_STORE_TOKEN`).

`repo` reaches all your repositories: keep the token only in this secret and revoke it if in doubt.

The catalog never takes a binary from you: once your pull request is reviewed and merged, its CI
compiles the app at the tagged commit.

## Publishing on the `kazoos-packages` branch

The same `v*` tag also runs **Publish the Kazo OS packages**: it packs the app and pushes it to the
`kazoos-packages` branch of this repository, keeping the versions already published. Kazo OS
users then install it with `kazopkg install <owner>/<repo>`, without the Kazo OS Store.

These packages are not signed: apps are signed only in the Kazo OS Store repository
(`Kazo-ca/Os-AppMarketPlace`), when it builds your app from the catalog (ADR 0219). Kazo OS checks
them by their SHA-256 and asks an administrator before installing from an untrusted source. Users
who want a signed package install your app from the Kazo OS Store.
