#!/usr/bin/env bash
set -euo pipefail

input_path="${1:?input Command Line Tools .dmg path is required}"
output_root="${2:?output directory is required}"

if [[ ! -f "${input_path}" ]]; then
  printf 'SDK extraction failed: input file does not exist: %s\n' "${input_path}" >&2
  exit 1
fi

case "$(basename "${input_path}")" in
  Command_Line_Tools_for_Xcode_*.dmg) ;;
  *)
    printf 'SDK extraction failed: expected a Command_Line_Tools_for_Xcode_*.dmg file, got %s\n' "$(basename "${input_path}")" >&2
    exit 1
    ;;
esac

mkdir --parents "${output_root}"
temporary_archive="${output_root}/.macos-sdk-archive.$$.tmp"

cleanup() {
  rm --force "${temporary_archive}"
}
trap cleanup EXIT

./tools/gen_sdk_package_tools_dmg.sh "${input_path}"

mapfile -t archives < <(
  find /opt/osxcross -maxdepth 1 -type f -name 'MacOSX*.sdk.tar.*' -print
)
if (( ${#archives[@]} == 0 )); then
  printf 'SDK extraction failed: no generated MacOSX*.sdk.tar.* archives found\n' >&2
  exit 1
fi

requested_sdk_version="${MACOS_SDK_VERSION:-}"
selected_archive="$(
  printf '%s\n' "${archives[@]}" |
    while IFS= read -r archive; do
      archive_basename="$(basename "${archive}")"
      if [[ "${archive_basename}" =~ ^MacOSX([0-9]+\.[0-9]+)\.sdk\.tar\. ]]; then
        sdk_version="${BASH_REMATCH[1]}"
        if [[ -z "${requested_sdk_version}" || "${sdk_version}" == "${requested_sdk_version}" ]]; then
          printf '%s\t%s\n' "${sdk_version}" "${archive}"
        fi
      fi
    done |
    sort --key=1,1 --field-separator=$'\t' --version-sort |
    sed -n '$p' |
    cut --fields=2-
)"
if [[ -z "${selected_archive}" ]]; then
  if [[ -n "${requested_sdk_version}" ]]; then
    printf 'SDK extraction failed: no MacOSX%s.sdk.tar.* archive found\n' "${requested_sdk_version}" >&2
  else
    printf 'SDK extraction failed: no versioned MacOSX<major>.<minor>.sdk.tar.* archive found\n' >&2
  fi
  exit 1
fi

archive_name="$(basename "${selected_archive}")"
cp --archive "${selected_archive}" "${temporary_archive}"
mv --force "${temporary_archive}" "${output_root}/${archive_name}"
find "${output_root}" -maxdepth 1 -type f -name 'MacOSX*.sdk.tar.*' ! -name "${archive_name}" -delete
printf 'Generated SDK archive: %s\n' "${output_root}/${archive_name}"
