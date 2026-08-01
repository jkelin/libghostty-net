set unstable
set lists

project := "src/LibGhostty.Net/LibGhostty.Net.csproj"
native_root := justfile_directory() / "artifacts/native"
ghostty_docker_image := "libghostty-net/ghostty-cross:zig-0.15.2"
macos_sdk_context := env_var_or_default("MACOS_SDK_CONTEXT", "")
tests_project := "tests/LibGhostty.Net.Tests/LibGhostty.Net.Tests.csproj"
tests_image := "libghostty-net/ghostty-tests:net8.0"

[doc('Build the managed library')]
check:
    dotnet build "{{ project }}" --configuration Release --verbosity minimal

[doc('Build native assets for the host and run the managed/native test suite')]
test: native
    dotnet test "{{ tests_project }}" --configuration Release --verbosity minimal

[doc('Build Linux x64 and ARM64 assets and run both Linux test suites in Docker')]
test_linux: ghostty_linux
    just test_linux_x64
    just test_linux_arm64

[doc('Run the Linux x64 test suite in an amd64 .NET container')]
test_linux_x64:
    docker build --platform linux/amd64 --file "{{ justfile_directory() / "docker/ghostty-tests.Dockerfile" }}" --tag "{{ tests_image }}-linux-x64" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=tmpfs,destination=/workspace/src/LibGhostty.Net/obj --mount type=tmpfs,destination=/workspace/src/LibGhostty.Net/bin --mount type=tmpfs,destination=/workspace/tests/LibGhostty.Net.Tests/obj --mount type=tmpfs,destination=/workspace/tests/LibGhostty.Net.Tests/bin --mount type=volume,source=libghostty-net-tests-nuget-linux-x64,target=/root/.nuget/packages --env LIBGHOSTTY_NATIVE_DIR=/workspace/artifacts/native/linux-x64 "{{ tests_image }}-linux-x64" dotnet test "{{ tests_project }}" --configuration Release --verbosity minimal

[doc('Run the Linux ARM64 test suite in an ARM64 .NET container')]
test_linux_arm64:
    docker build --platform linux/arm64 --file "{{ justfile_directory() / "docker/ghostty-tests.Dockerfile" }}" --tag "{{ tests_image }}-linux-arm64" "{{ justfile_directory() }}"
    docker run --rm --platform linux/arm64 --workdir /workspace --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=tmpfs,destination=/workspace/src/LibGhostty.Net/obj --mount type=tmpfs,destination=/workspace/src/LibGhostty.Net/bin --mount type=tmpfs,destination=/workspace/tests/LibGhostty.Net.Tests/obj --mount type=tmpfs,destination=/workspace/tests/LibGhostty.Net.Tests/bin --mount type=volume,source=libghostty-net-tests-nuget-linux-arm64,target=/root/.nuget/packages --env LIBGHOSTTY_NATIVE_DIR=/workspace/artifacts/native/linux-arm64 "{{ tests_image }}-linux-arm64" dotnet test "{{ tests_project }}" --configuration Release --verbosity minimal

[doc('Build macOS assets and run the host macOS test suite')]
[macos]
test_macos: ghostty_macos
    dotnet test "{{ tests_project }}" --configuration Release --verbosity minimal

[doc('Build and package managed code plus all staged native assets')]
pack: check
    dotnet pack "{{ project }}" --configuration Release --no-build --output "artifacts/packages" --verbosity minimal

[doc('Build Ghostty and the embedded Windows Terminal for the host Windows runtime')]
[windows]
native_windows: windows_terminal_release ghostty_windows_build

[doc('Build the vendored Windows Terminal ConPTY DLL and OpenConsole host')]
[private]
[script("cmd.exe", "/d", "/s", "/c")]
[windows]
windows_terminal_release:
    call "{{ justfile_directory() / "scripts/build-windows-terminal.cmd" }}"

[doc('Build the Ghostty Windows x64 dynamic library')]
[private]
[working-directory("native/ghostty")]
ghostty_windows_build:
    zig build --cache-dir "../../artifacts/ghostty-zig-cache-win-x64" --prefix "{{ native_root / "win-x64" }}" --prefix-exe-dir . --prefix-lib-dir . -Dtarget=x86_64-windows-msvc -Dfont-backend=freetype -Doptimize=ReleaseSafe -Demit-lib-vt=true -Demit-exe=false

[doc('Build Ghostty and Unix PTY helpers for the Linux runtimes in Docker')]
ghostty_linux:
    docker build --platform linux/amd64 --file "{{ justfile_directory() / "docker/ghostty-cross.Dockerfile" }}" --target linux --tag "{{ ghostty_docker_image }}" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-linux-x64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ ghostty_docker_image }}" linux-x64 x86_64-linux-gnu
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-linux-arm64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ ghostty_docker_image }}" linux-arm64 aarch64-linux-gnu

[doc('Build Ghostty and Unix PTY helpers for the macOS runtimes in Docker')]
ghostty_macos:
    {{ if macos_sdk_context == "" { error("Set MACOS_SDK_CONTEXT to a directory containing an osxcross-compatible MacOSX*.sdk.tar.* archive.") } }}
    docker build --platform linux/amd64 --build-arg OSX_ARCH=arm64 --build-context apple-sdk="{{ macos_sdk_context }}" --file "{{ justfile_directory() / "docker/ghostty-cross.Dockerfile" }}" --target macos --tag "{{ ghostty_docker_image }}-macos-arm64" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-osx-arm64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ ghostty_docker_image }}-macos-arm64" osx-arm64 aarch64-macos
    docker build --platform linux/amd64 --build-arg OSX_ARCH=x86_64 --build-context apple-sdk="{{ macos_sdk_context }}" --file "{{ justfile_directory() / "docker/ghostty-cross.Dockerfile" }}" --target macos --tag "{{ ghostty_docker_image }}-macos-x64" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-osx-x64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ ghostty_docker_image }}-macos-x64" osx-x64 x86_64-macos

[doc('Build the native assets for the current host runtime')]
native:
    {{ if os() == "windows" { "just native_windows" } else if os() == "linux" { "just ghostty_linux" } else if os() == "macos" { "just ghostty_macos" } else { error("Native assets support only Windows, Linux, and macOS.") } }}

[doc('Build native assets, run tests, and package the NuGet library')]
all: test pack
