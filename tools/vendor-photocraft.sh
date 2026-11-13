#!/usr/bin/env bash
# Puts a PhotoCraft web build in src/Kazo.Photo/wwwroot/photocraft/ (decision 4 of doc/en/plan.md;
# the same method as Kazo Fig's vendor-figcraft.sh). That folder is never edited by hand: this
# script replaces it whole and writes photocraft.json, its provenance (version, source, name, size
# and SHA-256 of each file). tests/Kazo.Photo.Tests checks the files still match it.
#
# Usage:
#   tools/vendor-photocraft.sh --dir <folder> [--craft-i18n <sha>] [--rustc <text>] [--trunk <version>]
#
# <folder> is Trunk's output for apps/photocraft-web of the external/photocraft submodule (one
# photocraft-web-<hash>.js, one photocraft-web-<hash>_bg.wasm, snippets/), as
# tools/build-photocraft.sh makes it. There is no "official zip" mode: Kazo Photo is always built
# from our fork (doc/en/plan.md, decision 4), and the official zip cuts its .wasm into parts. The
# provenance names the fork and the submodule's commit; the options add what the build used.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="$repo_root/src/Kazo.Photo/wwwroot/photocraft"
submodule="$repo_root/external/photocraft"

fail() { echo "$*" >&2; exit 1; }

build_dir=""
i18n_rev=""
rustc_version=""
trunk_version=""
while [[ $# -gt 0 ]]; do
  [[ $# -ge 2 || "$1" == -h || "$1" == --help ]] || fail "$1 needs a value"
  case "$1" in
    --dir) build_dir="$2"; shift 2 ;;
    --craft-i18n) i18n_rev="$2"; shift 2 ;;
    --rustc) rustc_version="$2"; shift 2 ;;
    --trunk) trunk_version="$2"; shift 2 ;;
    -h|--help) sed -n '2,14p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$build_dir" ]] || fail "--dir <folder> is required (Trunk's output; tools/build-photocraft.sh makes it)"
[[ -d "$build_dir" ]] || fail "$build_dir is not a folder"
source_dir="$(cd "$build_dir" && pwd)"

# The provenance names the fork and the submodule's commit, which must be clean so the commit alone
# says what was compiled.
[[ -f "$submodule/Cargo.toml" ]] || fail "external/photocraft is empty. Run: git submodule update --init external/photocraft"
[[ -z "$(git -C "$submodule" status --porcelain)" ]] || fail "external/photocraft has local changes; the provenance could not name what was built"
commit="$(git -C "$submodule" rev-parse HEAD)"
repository="$(git -C "$repo_root" config -f .gitmodules submodule.external/photocraft.url)"
version="$(awk '/^\[/ { in_pkg = ($0 == "[workspace.package]"); next }
                in_pkg && $1 == "version" { gsub(/[" ]/, "", $3); print $3; exit }' "$submodule/Cargo.toml")"
[[ -n "$version" ]] || fail "no [workspace.package] version in external/photocraft/Cargo.toml"

# Escapes a value for a JSON string (the rustc version is free text).
json_string() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "$value"
}

# Exactly one glue module and one .wasm, named by Trunk with the same hash.
shopt -s nullglob
glues=("$source_dir"/photocraft-web-*.js)
wasms=("$source_dir"/photocraft-web-*_bg.wasm)
shopt -u nullglob
if [[ ${#glues[@]} -ne 1 || ${#wasms[@]} -ne 1 ]]; then
  fail "Expected one photocraft-web-*.js and one photocraft-web-*_bg.wasm in $source_dir"
fi
glue="$(basename "${glues[0]}")"
wasm="$(basename "${wasms[0]}")"
[[ "${glue%.js}" == "${wasm%_bg.wasm}" ]] || fail "$glue and $wasm do not come from the same build"

# What the app needs: the glue, the .wasm, the snippets the glue imports (when the build has any),
# then every licence and notice of the release: PhotoCraft's own (MIT OR Apache-2.0, NOTICE), its
# asset list (ATTRIBUTION.md) and the OFL licences of the two fonts its .wasm embeds (Inter,
# JetBrains Mono, assets/fonts). Left out: Trunk's index.html (host/index.html replaces it).
rm -rf "$target"
mkdir -p "$target"
cp "$source_dir/$glue" "$source_dir/$wasm" "$target/"
[[ ! -d "$source_dir/snippets" ]] || cp -R "$source_dir/snippets" "$target/"
for notice in LICENSE-MIT LICENSE-APACHE NOTICE ATTRIBUTION.md; do
  [[ -f "$submodule/$notice" ]] || fail "external/photocraft/$notice is missing"
  cp "$submodule/$notice" "$target/"
done
shopt -s nullglob
font_licences=("$submodule"/assets/fonts/OFL-*.txt)
shopt -u nullglob
[[ ${#font_licences[@]} -gt 0 ]] || fail "no assets/fonts/OFL-*.txt in external/photocraft"
cp "${font_licences[@]}" "$target/"

# The provenance file, files sorted by path so a new version gives a readable diff.
{
  printf '{\n'
  printf '  "version": "%s",\n' "$version"
  printf '  "source": {\n'
  printf '    "kind": "source-build",\n'
  printf '    "repository": %s,\n' "$(json_string "$repository")"
  printf '    "commit": "%s"' "$commit"
  [[ -z "$i18n_rev" ]] || printf ',\n    "craftI18n": "%s"' "$i18n_rev"
  [[ -z "$rustc_version" ]] || printf ',\n    "rustc": %s' "$(json_string "$rustc_version")"
  [[ -z "$trunk_version" ]] || printf ',\n    "trunk": %s' "$(json_string "$trunk_version")"
  printf '\n  },\n'
  printf '  "glue": "%s",\n' "$glue"
  printf '  "wasm": "%s",\n' "$wasm"
  printf '  "files": [\n'
  first=1
  while IFS= read -r path; do
    size="$(stat -c %s "$target/$path")"
    sha="$(sha256sum "$target/$path" | cut -d' ' -f1)"
    [[ $first -eq 1 ]] || printf ',\n'
    first=0
    printf '    { "path": "%s", "size": %s, "sha256": "%s" }' "$path" "$size" "$sha"
  done < <(cd "$target" && find . -type f ! -path ./photocraft.json | sed 's#^\./##' | LC_ALL=C sort)
  printf '\n  ]\n'
  printf '}\n'
} > "$target/photocraft.json"

echo "PhotoCraft $version vendored in ${target#"$repo_root"/}: $glue, $wasm"
