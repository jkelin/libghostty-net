# syntax=docker/dockerfile:1.7

ARG DEBIAN_VERSION=bookworm-slim
ARG ZIG_VERSION=0.15.2

FROM docker.io/library/debian:${DEBIAN_VERSION} AS base
ARG ZIG_VERSION

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        bash binutils bzip2 ca-certificates clang cmake cpio curl file git \
        libbz2-dev liblzma-dev libssl-dev libxml2-dev lld make patch perl \
        python3 tar xz-utils zlib1g-dev \
    && rm --recursive --force /var/lib/apt/lists/*

RUN curl --fail --location --silent --show-error \
        "https://ziglang.org/download/${ZIG_VERSION}/zig-x86_64-linux-${ZIG_VERSION}.tar.xz" \
        --output /tmp/zig.tar.xz \
    && mkdir --parents /opt/zig \
    && tar --extract --file /tmp/zig.tar.xz --directory /opt/zig --strip-components=1 \
    && rm --force /tmp/zig.tar.xz

ENV PATH="/opt/zig:${PATH}"

COPY scripts/ghostty-cross-entrypoint.sh /usr/local/bin/ghostty-cross-entrypoint
RUN chmod +x /usr/local/bin/ghostty-cross-entrypoint

WORKDIR /workspace

FROM base AS linux
ENTRYPOINT ["/usr/local/bin/ghostty-cross-entrypoint"]

FROM base AS macos
ARG OSX_CROSS_COMMIT=27d21e4977c9751d01199c7a226a6faf494c3dd9
ARG OSX_ARCH=arm64

# Apple SDKs are supplied through the named build context and are not redistributed.
COPY --from=apple-sdk / /opt/apple-sdk-context/

RUN git clone --filter=blob:none --no-checkout https://github.com/tpoechtrager/osxcross.git /opt/osxcross \
    && git -C /opt/osxcross checkout --detach "${OSX_CROSS_COMMIT}" \
    && mkdir --parents /opt/osxcross/tarballs \
    && cp --archive --recursive /opt/apple-sdk-context/. /opt/osxcross/tarballs/ \
    && test -n "$(find /opt/osxcross/tarballs -maxdepth 1 -type f -name 'MacOSX*.sdk.tar.*' -print -quit)" \
    && UNATTENDED=1 BUILD_FLAVOR=llvm ENABLE_ARCHS="${OSX_ARCH}" TARGET_DIR=/opt/osxcross/target OSX_VERSION_MIN=11.0 /opt/osxcross/build.sh \
    && test -n "$(find /opt/osxcross/target/bin -maxdepth 1 \( -type f -o -type l \) -name "${OSX_ARCH}-apple-darwin*-clang" -print -quit)"

ENV PATH="/opt/osxcross/target/bin:${PATH}" \
    MACOSX_DEPLOYMENT_TARGET=11.0 \
    OSXCROSS_TARGET_DIR=/opt/osxcross/target

ENTRYPOINT ["/usr/local/bin/ghostty-cross-entrypoint"]
