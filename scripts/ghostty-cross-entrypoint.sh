#!/usr/bin/env bash
set -euo pipefail

runtime="${1:?runtime is required}"
target="${2:?Zig target is required}"

case "${runtime}:${target}" in
  linux-x64:x86_64-linux-gnu)
    ;;
  linux-arm64:aarch64-linux-gnu)
    ;;
  osx-arm64:aarch64-macos.11.0)
    compiler_arch=arm64
    export OSXCROSS_SDKROOT="${OSXCROSS_SDKROOT:-$(find /opt/osxcross/target/SDK -maxdepth 1 -type d -name 'MacOSX*.sdk' -print -quit)}"
    ;;
  osx-x64:x86_64-macos.11.0)
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
    \( -type f -o -type l \) -name "${compiler_arch}-apple-darwin*-clang" ! -name '*-cmake-clang' -print -quit)}"
  export CXX="${CXX:-${CC}++}"
  if [[ -z "${CC}" || ! -x "${CC}" || ! -x "${CXX}" ]]; then
    printf 'Unable to locate the osxcross %s compiler.\n' "${compiler_arch}" >&2
    exit 1
  fi
  host_crt_path="$("/usr/bin/clang" -print-file-name=crt1.o)"
  if [[ ! -f "${host_crt_path}" ]]; then
    printf 'Unable to locate the host CRT needed by Zig libc discovery.\n' >&2
    exit 1
  fi
  macos_cross_cc="${CC}"
  macos_cc_wrapper="/tmp/osxcross-cc-wrapper"
  cat > "${macos_cc_wrapper}" <<EOF
#!/bin/sh
if [ "\$1" = "-print-file-name=crt1.o" ]; then
  printf '%s\n' "${host_crt_path}"
else
  exec "${macos_cross_cc}" "\$@"
fi
EOF
  chmod +x "${macos_cc_wrapper}"
  export CC="${macos_cc_wrapper}"
fi

cache_root="${GHOSTTY_ZIG_CACHE_ROOT:-/tmp/libghostty-zig-cache-${runtime}}"
native_output_root="/tmp/libghostty-native-${runtime}"
zig_build_args=(
  --cache-dir "${cache_root}"
  --prefix "${native_output_root}"
  --prefix-exe-dir .
  --prefix-lib-dir .
  -Dtarget="${target}"
  -Dfont-backend=freetype
  -Doptimize=ReleaseSafe
  -Demit-lib-vt=true
  -Demit-exe=false
)
zig build "${zig_build_args[@]}"

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
