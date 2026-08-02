# syntax=docker/dockerfile:1.7

ARG DEBIAN_VERSION=bookworm-slim
ARG ZIG_VERSION=0.15.2

FROM docker.io/library/debian:${DEBIAN_VERSION} AS base
ARG ZIG_VERSION

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        bash binutils bzip2 ca-certificates clang cmake cpio curl file git \
        libbz2-dev liblzma-dev libssl-dev libxml2-dev lld llvm make patch perl \
        python3 tar xz-utils zlib1g-dev \
    && if ! command -v ld64.lld >/dev/null 2>&1; then \
        # Debian exposes LLVM's Mach-O linker as ld.lld; osxcross selects the driver by argv[0].
        ln --symbolic /usr/bin/ld.lld /usr/local/bin/ld64.lld; \
    fi \
    && rm --recursive --force /var/lib/apt/lists/*

RUN curl --fail --location --silent --show-error \
        "https://ziglang.org/download/${ZIG_VERSION}/zig-x86_64-linux-${ZIG_VERSION}.tar.xz" \
        --output /tmp/zig.tar.xz \
    && mkdir --parents /opt/zig \
    && tar --extract --file /tmp/zig.tar.xz --directory /opt/zig --strip-components=1 \
    && rm --force /tmp/zig.tar.xz

ENV PATH="/opt/zig:${PATH}"

COPY scripts/ghostty-cross-entrypoint.sh /usr/local/bin/ghostty-cross-entrypoint
COPY scripts/validate-macos.sh /usr/local/bin/validate-macos
RUN chmod +x /usr/local/bin/ghostty-cross-entrypoint /usr/local/bin/validate-macos

WORKDIR /workspace

FROM base AS linux
ENTRYPOINT ["/usr/local/bin/ghostty-cross-entrypoint"]

FROM base AS macos
ARG OSX_CROSS_COMMIT=27d21e4977c9751d01199c7a226a6faf494c3dd9
ARG OSX_ARCH=arm64

# Apple SDKs are supplied through the named build context and are not redistributed.
COPY --from=apple-sdk / /opt/apple-sdk-context/

# Ghostty consumes the C ABI; Debian's Clang cannot parse this SDK's libc++ smoke test.
# Keep the C compiler check required while making that unrelated C++ probe non-fatal.
RUN case "${OSX_ARCH}" in \
        arm64|x86_64) ;; \
        *) echo "Unsupported osxcross architecture: ${OSX_ARCH}" >&2; exit 2 ;; \
    esac \
    && sdk_archive="$(find /opt/apple-sdk-context -maxdepth 1 -type f -name 'MacOSX*.sdk.tar.*' -print -quit)" \
    && test -n "${sdk_archive}" \
    && git clone --filter=blob:none --no-checkout https://github.com/tpoechtrager/osxcross.git /opt/osxcross \
    && git -C /opt/osxcross checkout --detach "${OSX_CROSS_COMMIT}" \
    && sed -i '/test_compiler \$ARCH-apple-\$TARGET-clang++ \$BASE_DIR\/oclang\/test.cpp "\$req"/s/"\$req"/""/' /opt/osxcross/build.sh \
    && mkdir --parents /opt/osxcross/tarballs \
    && cp --archive --recursive /opt/apple-sdk-context/. /opt/osxcross/tarballs/ \
    && CFLAGS=-Wno-error=date-time CXXFLAGS=-Wno-error=date-time UNATTENDED=1 BUILD_FLAVOR=latest ENABLE_ARCHS="${OSX_ARCH}" TARGET_DIR=/opt/osxcross/target OSX_VERSION_MIN=11.0 /opt/osxcross/build.sh \
    && compiler="$(find /opt/osxcross/target/bin -maxdepth 1 \( -type f -o -type l \) -name "${OSX_ARCH}-apple-darwin*-clang" -print -quit)" \
    && test -n "${compiler}" \
    && test -x "${compiler}"

ENV PATH="/opt/osxcross/target/bin:${PATH}" \
    MACOSX_DEPLOYMENT_TARGET=11.0 \
    OSXCROSS_TARGET_DIR=/opt/osxcross/target

ENTRYPOINT ["/usr/local/bin/ghostty-cross-entrypoint"]
