#!/usr/bin/env bash
# Compiles PhotoCraft from the external/photocraft submodule (our fork Kazo-ca/photocraft; decision 4
# and work item 1 of doc/en/plan.md) with Trunk, the way its release builds the web app, then vendors
# the result in src/Kazo.Photo/wwwroot/photocraft/ with tools/vendor-photocraft.sh --dir. The
# .github/workflows/build-photocraft.yml workflow runs it; it also runs on a developer machine.
#
# Usage:
#   tools/build-photocraft.sh
#
# Needs: Rust stable (1.95 or later) with the wasm32-unknown-unknown target, Trunk at the version
# below, git and network access (craft-i18n). PhotoCraft's web build embeds the translations of
# storytold/craft-i18n at the commit of its craft-i18n.rev; this build requires them
# (CRAFT_I18N_REQUIRED). CRAFT_I18N_DIR, when set, names a craft-i18n checkout to use instead of
# cloning one; it must be on that commit. No craft-fonts: PhotoCraft never embeds it in its wasm
# build (crates/text/build.rs); the fonts of the Text tool are served next to the page instead.
#
# Unlike Kazo Fig and Kazo Pdf, this does not run PhotoCraft's packaging/web/package.sh: that zip
# cuts the .wasm into gzipped parts started by its own boot page, while the Kazo OS module starts
# one wasm-bindgen glue and one .wasm. Trunk's own output is what we take.
set -euo pipefail

# The Trunk version of PhotoCraft's release workflow (craft-common-actions, install-trunk).
trunk_version="0.21.14"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
photocraft="$repo_root/external/photocraft"

case "${1:-}" in
  "") ;;
  -h|--help) sed -n '2,19p' "$0"; exit 0 ;;
  *) echo "Unknown argument: $1" >&2; exit 2 ;;
esac

fail() { echo "error: $*" >&2; exit 1; }

[[ -f "$photocraft/Cargo.toml" ]] || fail "external/photocraft is empty. Run: git submodule update --init external/photocraft"
# A local change of PhotoCraft must go through a commit of the fork, so the provenance names it.
[[ -z "$(git -C "$photocraft" status --porcelain)" ]] || fail "external/photocraft has local changes; commit them in the fork first"

command -v trunk >/dev/null || fail "trunk not found; install Trunk $trunk_version"
actual_trunk="$(trunk --version | awk '{print $2}')"
[[ "$actual_trunk" == "$trunk_version" ]] || fail "trunk $actual_trunk found; PhotoCraft's release uses $trunk_version"
rustup target list --installed 2>/dev/null | grep -qx wasm32-unknown-unknown \
  || fail "the wasm32-unknown-unknown target is missing (rustup target add wasm32-unknown-unknown)"

commit="$(git -C "$photocraft" rev-parse HEAD)"
rustc_version="$(rustc --version)"
i18n_rev="$(tr -d '[:space:]' < "$photocraft/craft-i18n.rev")"
[[ "$i18n_rev" =~ ^[0-9a-f]{40}$ ]] || fail "external/photocraft/craft-i18n.rev does not hold one commit SHA"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# craft-i18n at PhotoCraft's pin: the build embeds its catalogs, and without them it has English only.
i18n="${CRAFT_I18N_DIR:-}"
if [[ -z "$i18n" ]]; then
  i18n="$work/craft-i18n"
  git init -q "$i18n"
  git -C "$i18n" fetch -q --depth 1 https://github.com/storytold/craft-i18n.git "$i18n_rev"
  git -C "$i18n" checkout -q FETCH_HEAD
fi
[[ "$(git -C "$i18n" rev-parse HEAD)" == "$i18n_rev" ]] || fail "$i18n is not on craft-i18n $i18n_rev"

# trunk build --release in apps/photocraft-web (profile wasm-release, feature heif, wasm-opt -Oz
# --converge: all set by its index.html), into a temporary folder so the submodule stays clean.
# The build date is the commit's (UTC), so building the same commit twice bakes the same date into
# the About window. CRAFT_FONTS_DIR is cleared so a value left in the developer's shell changes
# nothing.
build_date="$(TZ=UTC git -C "$photocraft" log -1 --format=%cd --date=format-local:%Y-%m-%d)"
echo "Building PhotoCraft $commit (rustc: $rustc_version, trunk $actual_trunk, craft-i18n $i18n_rev)"
(
  unset CRAFT_FONTS_DIR CRAFT_FONTS_REQUIRED PHOTOCRAFT_VERSION
  export CRAFT_I18N_DIR="$i18n" CRAFT_I18N_REQUIRED=1
  export PHOTOCRAFT_BUILD_SHA="$commit" PHOTOCRAFT_BUILD_DATE="$build_date"
  export CARGO_TARGET_DIR="${CARGO_TARGET_DIR:-$photocraft/target}"
  cd "$photocraft/apps/photocraft-web"
  trunk build --release --dist "$work/site"
)

"$repo_root/tools/vendor-photocraft.sh" --dir "$work/site" \
  --craft-i18n "$i18n_rev" --rustc "$rustc_version" --trunk "$actual_trunk"
