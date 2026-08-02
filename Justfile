set unstable
set lists
set dotenv-load := true

project := "src/LibGhostty.Net/LibGhostty.Net.csproj"
native_root := justfile_directory() / "artifacts/native"
ghostty_docker_image := "libghostty-net/ghostty-cross:zig-0.15.2"
macos_arm64_docker_image := "libghostty-net/ghostty-cross:zig-0.15.2-macos-arm64"
macos_sdk_context := env_var_or_default("MACOS_SDK_CONTEXT", justfile_directory() / "artifacts/macos-sdk")
macos_clt_dmg := env_var_or_default("MACOS_CLT_DMG", "")
macos_sdk_version := env_var_or_default("MACOS_SDK_VERSION", "15.4")
macos_sdk_extractor_image := "libghostty-net/macos-sdk-extractor:osxcross-27d21e49"
tests_project := "tests/LibGhostty.Net.Tests/LibGhostty.Net.Tests.csproj"
tests_image := "libghostty-net/ghostty-tests:net10.0"
nuget_package := "artifacts/packages/LibGhostty.Net.1.0.1.nupkg"
nuget_source := env_var_or_default("NUGET_SOURCE", "https://api.nuget.org/v3/index.json")
nuget_push_command := if env_var_or_default("NUGET_API_KEY", "") == "" { "dotnet nuget push \"" + nuget_package + "\" --source \"" + nuget_source + "\" --skip-duplicate --interactive" } else if os() == "windows" { "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + (justfile_directory() / "scripts/publish-nuget.ps1") + "\" -Package \"" + nuget_package + "\" -Source \"" + nuget_source + "\"" } else { "dotnet nuget push \"" + nuget_package + "\" --source \"" + nuget_source + "\" --api-key \"$NUGET_API_KEY\" --skip-duplicate --interactive" }



[doc('Extract the local Command Line Tools SDK through Docker')]
macos_sdk_setup:
    {{ if macos_clt_dmg == "" { error("Set MACOS_CLT_DMG to the downloaded Command Line Tools .dmg file.") } }}
    docker build --platform linux/amd64 --file "{{ justfile_directory() / "docker/macos-sdk.Dockerfile" }}" --tag "{{ macos_sdk_extractor_image }}" "{{ justfile_directory() }}"
    {{ if os() == "windows" { "powershell -NoProfile -Command \"New-Item -ItemType Directory -Force -Path '" + macos_sdk_context + "' | Out-Null\"" } else { "mkdir -p \"" + macos_sdk_context + "\"" } }}
    docker run --rm --platform linux/amd64 --env "MACOS_SDK_VERSION={{ macos_sdk_version }}" --mount type=bind,source="{{ macos_clt_dmg }}",target=/input/Command_Line_Tools_for_Xcode_Extract.dmg,readonly --mount type=bind,source="{{ macos_sdk_context }}",target=/output "{{ macos_sdk_extractor_image }}" /input/Command_Line_Tools_for_Xcode_Extract.dmg /output
    just macos_sdk_check



[doc('Validate the configured macOS SDK context')]
macos_sdk_check:
    {{ if macos_sdk_context == "" { error("Set MACOS_SDK_CONTEXT to a directory containing an osxcross-compatible MacOSX*.sdk.tar.* archive.") } }}
    {{ if os() == "windows" { "powershell -NoProfile -ExecutionPolicy Bypass -File \"" + justfile_directory() / "scripts/check-macos-sdk.ps1" + "\" -Context \"" + macos_sdk_context + "\"" } else { "test -d \"" + macos_sdk_context + "\" || (echo 'MACOS_SDK_CONTEXT is not a directory' >&2; exit 1); set -- \"" + macos_sdk_context + "\"/MacOSX*.sdk.tar.*; if [ \"$#\" -ne 1 ] || [ ! -f \"$1\" ]; then echo 'Expected exactly one MacOSX*.sdk.tar.* archive' >&2; exit 1; fi; printf '%s\\n' \"$1\"" } }}


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

[doc('Build and publish the NuGet package with configured NuGet credentials')]
publish: pack
    {{ nuget_push_command }}

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

[doc('Build macOS ARM64 Ghostty and Unix PTY binaries in Docker')]
build_macos_arm64: macos_sdk_check
    docker build --platform linux/amd64 --build-arg OSX_ARCH=arm64 --build-context apple-sdk="{{ macos_sdk_context }}" --file "{{ justfile_directory() / "docker/ghostty-cross.Dockerfile" }}" --target macos --tag "{{ macos_arm64_docker_image }}" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-osx-arm64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ macos_arm64_docker_image }}" osx-arm64 aarch64-macos.11.0

[doc('Validate macOS ARM64 Mach-O files, exports, and cross-linking in Docker')]
validate_macos_arm64: build_macos_arm64
    docker run --rm --platform linux/amd64 --workdir /workspace --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --entrypoint /usr/local/bin/validate-macos "{{ macos_arm64_docker_image }}"

[doc('Build, validate, package, and inspect macOS ARM64 native assets')]
test_macos_arm64: validate_macos_arm64
    just check
    {{ if os() == "windows" { "powershell -NoProfile -Command \"if (Test-Path 'artifacts/macos-arm64-package-check') { Remove-Item 'artifacts/macos-arm64-package-check' -Recurse -Force }; New-Item -ItemType Directory -Path 'artifacts/macos-arm64-package-check' -Force | Out-Null\"" } else { "rm -rf artifacts/macos-arm64-package-check && mkdir -p artifacts/macos-arm64-package-check" } }}
    dotnet pack "{{ project }}" --configuration Release --no-build --output "artifacts/macos-arm64-package-check" --verbosity minimal
    docker run --rm --platform linux/amd64 --workdir /workspace --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --entrypoint /usr/local/bin/validate-macos "{{ macos_arm64_docker_image }}" /workspace/artifacts/native/osx-arm64 /workspace/artifacts/macos-arm64-package-check

[doc('Build macOS x64 Ghostty and Unix PTY binaries in Docker')]
build_macos_x64: macos_sdk_check
    docker build --platform linux/amd64 --build-arg OSX_ARCH=x86_64 --build-context apple-sdk="{{ macos_sdk_context }}" --file "{{ justfile_directory() / "docker/ghostty-cross.Dockerfile" }}" --target macos --tag "{{ ghostty_docker_image }}-macos-x64" "{{ justfile_directory() }}"
    docker run --rm --platform linux/amd64 --workdir /workspace/native/ghostty --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --mount type=volume,source=libghostty-net-zig-cache-osx-x64,target=/zig-cache --env GHOSTTY_ZIG_CACHE_ROOT=/zig-cache "{{ ghostty_docker_image }}-macos-x64" osx-x64 x86_64-macos.11.0

[doc('Validate macOS x64 Mach-O files, exports, and cross-linking in Docker')]
validate_macos_x64: build_macos_x64
    docker run --rm --platform linux/amd64 --workdir /workspace --mount type=bind,source="{{ justfile_directory() }}",target=/workspace --entrypoint /usr/local/bin/validate-macos "{{ ghostty_docker_image }}-macos-x64" /workspace/artifacts/native/osx-x64

[doc('Build and validate Ghostty and Unix PTY binaries for both macOS architectures')]
ghostty_macos: validate_macos_arm64 validate_macos_x64

[doc('Build the native assets for the current host runtime')]
native:
    {{ if os() == "windows" { "just native_windows" } else if os() == "linux" { "just ghostty_linux" } else if os() == "macos" { "just ghostty_macos" } else { error("Native assets support only Windows, Linux, and macOS.") } }}

[doc('Build native assets, run tests, and package the NuGet library')]
all: test pack
