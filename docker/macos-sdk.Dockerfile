# syntax=docker/dockerfile:1.7

ARG DEBIAN_VERSION=bookworm-slim
ARG OSX_CROSS_COMMIT=27d21e4977c9751d01199c7a226a6faf494c3dd9

FROM docker.io/library/debian:${DEBIAN_VERSION}
ARG OSX_CROSS_COMMIT

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        bash bzip2 ca-certificates clang cmake cpio file git \
        libbz2-dev liblzma-dev libssl-dev libxml2-dev make patch perl \
        python3 tar xz-utils zlib1g-dev \
    && rm --recursive --force /var/lib/apt/lists/*

RUN git clone --filter=blob:none --no-checkout \
        https://github.com/tpoechtrager/osxcross.git /opt/osxcross \
    && git -C /opt/osxcross checkout --detach "${OSX_CROSS_COMMIT}"

# Newer Command Line Tools archives can emit symlinks before their parent directories.
# Ensure GNU cpio creates missing directories while extracting those payloads.
RUN sed -i 's/cpio -i)/cpio -idm)/' /opt/osxcross/tools/gen_sdk_package_tools_dmg.sh

COPY scripts/extract-macos-sdk.sh /usr/local/bin/extract-macos-sdk
RUN chmod +x /usr/local/bin/extract-macos-sdk

WORKDIR /opt/osxcross
ENTRYPOINT ["/usr/local/bin/extract-macos-sdk"]
