#!/usr/bin/env bash
set -euo pipefail

runtime_root="${1:-/workspace/artifacts/native/osx-arm64}"
package_path="${2:-}"
runtime_id="$(basename "${runtime_root}")"

case "${runtime_id}" in
  osx-arm64)
    compiler_arch=arm64
    architecture_label='ARM64'
    architecture_pattern='arm64'
    ;;
  osx-x64)
    compiler_arch=x86_64
    architecture_label='x64'
    architecture_pattern='x86_64'
    ;;
  *)
    printf 'Unsupported macOS runtime directory: %s\n' "${runtime_id}" >&2
    exit 2
    ;;
esac

fail() {
  printf 'macOS %s validation failed: %s\n' "${architecture_label}" "$1" >&2
  exit 1
}

require_file() {
  local path="$1"
  if [[ ! -f "${path}" ]]; then
    fail "required file is missing: ${path}"
  fi
}

resolve_package_path() {
  local path="$1"
  local package_candidates=()

  if [[ ! -d "${path}" ]]; then
    require_file "${path}"
    printf '%s\n' "${path}"
    return 0
  fi

  while IFS= read -r -d '' candidate; do
    package_candidates+=("${candidate}")
  done < <(
    find "${path}" -maxdepth 1 -type f \
      -name 'LibGhostty.Net.*.nupkg' \
      ! -name '*.symbols.nupkg' \
      -print0
  )
  if (( ${#package_candidates[@]} != 1 )); then
    fail "expected exactly one LibGhostty.Net package in ${path}, found ${#package_candidates[@]}"
  fi

  printf '%s\n' "${package_candidates[0]}"
}

require_macho() {
  local path="$1"
  local description
  description="$(file --brief "${path}")"
  case "${description}" in
    *Mach-O*64-bit*"${architecture_pattern}"*) ;;
    *) fail "${path} is not a Mach-O ${architecture_label} binary: ${description}" ;;
  esac
}

require_macos_min_version() {
  local path="$1"
  local minimum_version
  minimum_version="$("${otool_tool}" -l "${path}" | awk '
    $1 == "cmd" {
      in_version_command = ($2 == "LC_BUILD_VERSION" || $2 == "LC_VERSION_MIN_MACOSX")
      next
    }
    in_version_command && $1 == "minos" {
      print $2
      exit
    }
    in_version_command && $1 == "version" {
      print $2
      exit
    }
  ')"
  if [[ "${minimum_version}" != "11.0" ]]; then
    fail "${path} has macOS minimum version ${minimum_version:-unknown}, expected 11.0"
  fi
}

find_tool() {
  local pattern="$1"
  local excluded_pattern="${2:-}"
  if [[ -n "${excluded_pattern}" ]]; then
    find /opt/osxcross/target/bin -maxdepth 1 \
      \( -type f -o -type l \) \
      -name "${pattern}" ! -name "${excluded_pattern}" \
      -print -quit
  else
    find /opt/osxcross/target/bin -maxdepth 1 \
      \( -type f -o -type l \) \
      -name "${pattern}" \
      -print -quit
  fi
}

require_symbol() {
  local path="$1"
  local symbol="$2"
  local symbol_table="$3"
  local exported_symbol="_${symbol}"
  local address type name

  while read -r address type name; do
    if [[ "${name}" == "${symbol}" || "${name}" == "${exported_symbol}" ]]; then
      return 0
    fi
  done <<< "${symbol_table}"

  fail "${path} does not export ${symbol}"
}

library_path="${runtime_root}/libghostty-vt.dylib"
pty_path="${runtime_root}/libmuxer-pty.dylib"
require_file "${library_path}"
require_file "${pty_path}"
require_macho "${library_path}"
require_macho "${pty_path}"
otool_tool="$(find_tool "${compiler_arch}-apple-darwin*-otool")"
if [[ -z "${otool_tool}" ]]; then
  fail "unable to locate the osxcross otool tool"
fi
require_macos_min_version "${library_path}"
require_macos_min_version "${pty_path}"

nm_tool="$(find_tool "${compiler_arch}-apple-darwin*-nm")"
if [[ -z "${nm_tool}" ]]; then
  nm_tool="$(command -v llvm-nm || true)"
fi
if [[ -z "${nm_tool}" ]]; then
  fail "unable to locate an osxcross nm or llvm-nm tool"
fi

defined_symbols() {
  local path="$1"
  if [[ "$(basename "${nm_tool}")" == llvm-nm* ]]; then
    "${nm_tool}" -g --defined-only "${path}"
  else
    "${nm_tool}" -gU "${path}"
  fi
}

library_symbols="$(defined_symbols "${library_path}")" || fail "unable to inspect defined symbols in ${library_path} with ${nm_tool}"
pty_symbols="$(defined_symbols "${pty_path}")" || fail "unable to inspect defined symbols in ${pty_path} with ${nm_tool}"

for symbol in \
  ghostty_terminal_new \
  ghostty_terminal_free \
  ghostty_terminal_resize \
  ghostty_terminal_set \
  ghostty_terminal_vt_write \
  ghostty_terminal_get \
  ghostty_key_encoder_new \
  ghostty_key_encoder_free \
  ghostty_key_encoder_setopt_from_terminal \
  ghostty_key_encoder_encode \
  ghostty_key_event_new \
  ghostty_key_event_free \
  ghostty_key_event_set_action \
  ghostty_key_event_set_key \
  ghostty_key_event_set_mods \
  ghostty_key_event_set_consumed_mods \
  ghostty_key_event_set_utf8 \
  ghostty_key_event_set_unshifted_codepoint \
  ghostty_paste_encode \
  ghostty_mouse_event_new \
  ghostty_mouse_event_free \
  ghostty_mouse_event_set_action \
  ghostty_mouse_event_set_button \
  ghostty_mouse_event_clear_button \
  ghostty_mouse_event_set_mods \
  ghostty_mouse_event_set_position \
  ghostty_mouse_encoder_new \
  ghostty_mouse_encoder_free \
  ghostty_mouse_encoder_setopt \
  ghostty_mouse_encoder_setopt_from_terminal \
  ghostty_mouse_encoder_encode \
  ghostty_terminal_scroll_viewport \
  ghostty_render_state_free \
  ghostty_render_state_new \
  ghostty_render_state_update \
  ghostty_render_state_get \
  ghostty_render_state_colors_get \
  ghostty_render_state_set \
  ghostty_render_state_row_iterator_new \
  ghostty_render_state_row_iterator_free \
  ghostty_render_state_row_iterator_next \
  ghostty_render_state_row_get \
  ghostty_render_state_row_cells_new \
  ghostty_render_state_row_cells_free \
  ghostty_render_state_row_cells_select \
  ghostty_render_state_row_cells_get \
  ghostty_cell_get; do
  require_symbol "${library_path}" "${symbol}" "${library_symbols}"
done

require_symbol "${pty_path}" muxer_forkpty_exec "${pty_symbols}"

compiler="$(find_tool "${compiler_arch}-apple-darwin*-clang" '*-cmake-clang')"
if [[ -z "${compiler}" ]]; then
  fail "unable to locate the osxcross ${compiler_arch} compiler"
fi
sdk_root="${OSXCROSS_SDKROOT:-$(find /opt/osxcross/target/SDK -maxdepth 1 -type d -name 'MacOSX*.sdk' -print -quit)}"
if [[ -z "${sdk_root}" || ! -d "${sdk_root}" ]]; then
  fail "unable to locate the osxcross Apple SDK"
fi

probe_path="/tmp/libghostty-macos-${runtime_id}-abi-smoke"
"${compiler}" \
  -O2 \
  -isysroot "${sdk_root}" \
  -I"${runtime_root}/include" \
  -I/workspace/native/ghostty/include \
  -mmacosx-version-min="${MACOSX_DEPLOYMENT_TARGET:-11.0}" \
  /workspace/native/macos-abi-smoke.c \
  "${library_path}" \
  "${pty_path}" \
  -o "${probe_path}" \
  || fail "macOS ${architecture_label} ABI probe failed to link"
require_macho "${probe_path}"

if [[ -n "${package_path}" ]]; then
  package_path="$(resolve_package_path "${package_path}")"
  python3 - "${package_path}" "${runtime_id}" <<'PY'
import sys
import zipfile

package_path, runtime_id = sys.argv[1:]
required = {
    f"runtimes/{runtime_id}/native/libghostty-vt.dylib",
    f"runtimes/{runtime_id}/native/libmuxer-pty.dylib",
}
with zipfile.ZipFile(package_path) as package:
    entries = set(package.namelist())
missing = sorted(required - entries)
if missing:
    raise SystemExit(f"{runtime_id} package assets are missing: {', '.join(missing)}")
PY
fi

printf 'macOS %s artifacts, ABI exports, deployment target, cross-link, and package assets are valid.\n' "${architecture_label}"
