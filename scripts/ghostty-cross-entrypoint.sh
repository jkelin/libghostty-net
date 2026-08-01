#!/usr/bin/env bash
set -euo pipefail

runtime="${1:?runtime is required}"
target="${2:?Zig target is required}"

case "${runtime}:${target}" in
  linux-x64:x86_64-linux-gnu)
    ;;
  linux-arm64:aarch64-linux-gnu)
    ;;
  osx-arm64:aarch64-macos)
    compiler_arch=arm64
    export OSXCROSS_SDKROOT="${OSXCROSS_SDKROOT:-$(find /opt/osxcross/target/SDK -maxdepth 1 -type d -name 'MacOSX*.sdk' -print -quit)}"
    ;;
  osx-x64:x86_64-macos)
    compiler_arch=x86_64
    export OSXCROSS_SDKROOT="${OSXCROSS_SDKROOT:-$(find /opt/osxcross/target/SDK -maxdepth 1 -type d -name 'MacOSX*.sdk' -print -quit)}"
    ;;
  *)
    printf 'Unsupported Ghostty Docker target: %s:%s\n' "${runtime}" "${target}" >&2
    exit 2
    ;;
esac

if [[ "${runtime}" == osx-* ]]; then
  if [[ -z "${OSXCROSS_SDKROOT}" || ! -d "${OSXCROSS_SDKROOT}" ]]; then
    printf 'Unable to locate the osxcross Apple SDK.\n' >&2
    exit 1
  fi

  export CC="${CC:-$(find /opt/osxcross/target/bin \
    \( -type f -o -type l \) -name "${compiler_arch}-apple-darwin*-clang" -print -quit)}"
  export CXX="${CXX:-${CC}++}"
  if [[ -z "${CC}" || ! -x "${CC}" || ! -x "${CXX}" ]]; then
    printf 'Unable to locate the osxcross %s compiler.\n' "${compiler_arch}" >&2
    exit 1
  fi
fi

cache_root="${GHOSTTY_ZIG_CACHE_ROOT:-/tmp/libghostty-zig-cache-${runtime}}"
native_output_root="/tmp/libghostty-native-${runtime}"
rm --recursive --force "${native_output_root}" "/workspace/artifacts/native/${runtime}"
mkdir --parents \
  "${native_output_root}" \
  "/workspace/artifacts/native/${runtime}" \
  "${cache_root}"

zig build \
  --cache-dir "${cache_root}" \
  --prefix "${native_output_root}" \
  --prefix-exe-dir . \
  --prefix-lib-dir . \
  -Dtarget="${target}" \
  -Dfont-backend=freetype \
  -Doptimize=ReleaseSafe \
  -Demit-lib-vt=true \
  -Demit-exe=false

# Dereference Zig's installed library links for reliable package staging.
cp --recursive --dereference --force "${native_output_root}/." \
  "/workspace/artifacts/native/${runtime}/"

pty_helper_output="/workspace/artifacts/native/${runtime}/$([[ "${runtime}" == osx-* ]] && printf 'libmuxer-pty.dylib' || printf 'libmuxer-pty.so')"
if [[ "${runtime}" == osx-* ]]; then
  "${CC}" -O2 -dynamiclib -fPIC \
    /workspace/native/unix-pty-helper.c \
    -o "${pty_helper_output}"
else
  zig cc -target "${target}" -O2 -fPIC -shared \
    /workspace/native/unix-pty-helper.c \
    -o "${pty_helper_output}" -lutil
fi
