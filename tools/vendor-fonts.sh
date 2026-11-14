#!/usr/bin/env bash
# Puts the Latin fonts of craft-fonts in src/Kazo.Photo/wwwroot/host/fonts/ (doc/en/plan.md,
# work item 2, Benoit's answer Q4 = A). That folder is never edited by hand: this script replaces
# it whole.
#
# Usage:
#   tools/vendor-fonts.sh --dir <craft-fonts checkout>
#
# <craft-fonts checkout> is a clone of https://github.com/storytold/craft-fonts at the commit of
# tools/craft-fonts.rev. The script takes its opt-in Latin catalog (fonts/latin-manifest.txt), less
# Inter, which PhotoCraft already carries in its .wasm, checks each file's SHA-256 against the
# catalog, and copies the files and their licences. PhotoCraft's web build reads
# fonts/manifest.txt relative to its page (apps/photocraft-web/src/web.rs, FONTS_MANIFEST) and
# fetches a family the first time a text layer or a font menu needs it; the catalog's paths
# (fonts/<family>/<file>) are already relative to the page, so the manifest keeps them. No CJK
# font (Q4 = A): Japanese, Chinese and Korean text falls back to PhotoCraft's own fonts.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
target="$repo_root/src/Kazo.Photo/wwwroot/host/fonts"
rev_file="$repo_root/tools/craft-fonts.rev"

fail() { echo "$*" >&2; exit 1; }

source_dir=""
while [[ $# -gt 0 ]]; do
  [[ $# -ge 2 || "$1" == -h || "$1" == --help ]] || fail "$1 needs a value"
  case "$1" in
    --dir) source_dir="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$source_dir" ]] || fail "--dir <craft-fonts checkout> is required"
[[ -f "$source_dir/fonts/latin-manifest.txt" ]] || fail "$source_dir has no fonts/latin-manifest.txt"

wanted="$(tr -d '[:space:]' < "$rev_file")"
actual="$(git -C "$source_dir" rev-parse HEAD)"
[[ "$actual" == "$wanted" ]] || fail "craft-fonts is at $actual, tools/craft-fonts.rev wants $wanted"

staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
mkdir -p "$staging/fonts"
manifest="$staging/fonts/manifest.txt"
{
  echo "# Fonts PhotoCraft serves in Kazo Photo (doc/en/plan.md, Q4 = A), written by tools/vendor-fonts.sh"
  echo "# from storytold/craft-fonts $wanted, fonts/latin-manifest.txt, less Inter (already in PhotoCraft)."
  echo "# family | style | file | scripts | licence | licence file | sha256 | source; paths relative to host/index.html."
} > "$manifest"

count=0
while IFS= read -r line; do
  [[ -z "${line// }" || "$line" == \#* ]] && continue
  IFS='|' read -r family _style file _scripts licence licence_file sha _source <<< "$line"
  family="$(echo "$family" | xargs)"; file="$(echo "$file" | xargs)"; licence="$(echo "$licence" | xargs)"
  licence_file="$(echo "$licence_file" | xargs)"; sha="$(echo "$sha" | xargs)"
  [[ "$family" == "Inter" ]] && continue
  [[ "$licence" == "OFL-1.1" ]] || fail "$file: licence $licence is not OFL-1.1"
  [[ "$file" == fonts/* && "$licence_file" == fonts/* ]] || fail "$file: not under fonts/"
  got="$(sha256sum "$source_dir/$file" | cut -d' ' -f1)"
  [[ "$got" == "$sha" ]] || fail "$file: SHA-256 $got, the catalog says $sha"
  mkdir -p "$staging/$(dirname "$file")"
  cp "$source_dir/$file" "$staging/$file"
  cp "$source_dir/$licence_file" "$staging/$licence_file"
  echo "$line" >> "$manifest"
  count=$((count + 1))
done < "$source_dir/fonts/latin-manifest.txt"

[[ $count -gt 0 ]] || fail "no font taken from the catalog"
rm -rf "$target"
mkdir -p "$(dirname "$target")"
cp -r "$staging/fonts" "$target"
echo "$count font files in $target ($(du -sh "$target" | cut -f1))"
