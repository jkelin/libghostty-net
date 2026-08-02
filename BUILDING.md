# Building LibGhostty.Net

This document covers building the managed library, native runtime assets, tests, and NuGet package. The user-facing API and package behavior are documented in [README.md](README.md).

## Prerequisites

Install the following tools before building:

- .NET 8 SDK.
- [`just`](https://just.systems/) for the repository build recipes.
- Git with submodule support.
- Docker Desktop with the Linux container engine for Linux and macOS cross-builds.
- Zig 0.15.2 for the Windows Ghostty build.
- Visual Studio with the C++ workload and Windows SDK for the embedded Windows Terminal build.

Initialize the native submodules after checkout:

```sh
git submodule update --init --recursive
```

## Build recipes

Run commands from the repository root. Native artifacts are written to `artifacts/native/<rid>/` and NuGet packages are written to `artifacts/packages/`.

| Command | Purpose |
| --- | --- |
| `just check` | Build the managed library in Release configuration. |
| `just test` | Build host native assets and run the managed/native tests for the current host. |
| `just native_windows` | Build Ghostty and the Windows Terminal ConPTY assets on Windows. |
| `just test_linux` | Build Linux x64 and ARM64 assets and run both Linux test suites in Docker. |
| `just ghostty_macos` | Build and validate macOS x64 and ARM64 Ghostty and Unix PTY assets in Docker. |
| `just test_macos` | Run the macOS test suite on a macOS host. |
| `just test_macos_arm64` | Build, validate, package, and inspect the macOS ARM64 assets. |
| `just pack` | Build the managed project and create the NuGet package. |
| `just publish` | Build the package and publish it with configured NuGet credentials. |
| `just all` | Run the host test workflow and package the library. |

The canonical package output is:

```text
artifacts/packages/LibGhostty.Net.1.0.0.nupkg
```

## Windows native build

On Windows, build the native assets with:

```powershell
just native_windows
```

This invokes `scripts/build-windows-terminal.cmd` to build the vendored Windows Terminal components and stages the following files under `artifacts/native/win-x64/`:

- `ghostty-vt.dll`
- `conpty.dll`
- `OpenConsole.exe`

The current Windows package supports x64. The Windows Terminal build requires Visual Studio's C++ tools, the Windows SDK, and the terminal repository's generated/build dependencies.

After native assets are available, run the managed tests and package build:

```powershell
just test
just pack
```

## Linux native build

Linux assets are built in Docker so that the native toolchain and runtime environment are reproducible:

```sh
just ghostty_linux
```

This produces:

- `artifacts/native/linux-x64/libghostty-vt.so`
- `artifacts/native/linux-x64/libmuxer-pty.so`
- `artifacts/native/linux-arm64/libghostty-vt.so`
- `artifacts/native/linux-arm64/libmuxer-pty.so`

Build and run both Linux test suites with:

```sh
just test_linux
```

The Linux ARM64 test suite runs in an ARM64 .NET container. It requires Docker to support the requested platform through its configured builder.

## macOS cross-builds from Windows or Linux

The macOS native assets use [osxcross](https://github.com/tpoechtrager/osxcross). Apple does not provide an unauthenticated direct URL for redistributing SDK archives, so the SDK must be obtained from an Apple developer download and packaged locally under Apple's license terms.

The repository's cross-builds target macOS 11.0 as the minimum deployment version. The validators inspect the resulting Mach-O files and fail if either library has a higher or unknown minimum version.

### 1. Obtain an Apple SDK source package

Use Apple's developer downloads page:

- [Apple developer downloads](https://developer.apple.com/download/all/)
- [Xcode license agreement](https://www.apple.com/legal/sla/docs/xcode.pdf)

Download either Xcode or the Xcode Command Line Tools. You may need to sign in with an Apple developer account.

### 2. Generate an osxcross SDK archive

The downloaded Xcode package is not the archive consumed by this repository. Use the official osxcross packaging tools to generate an archive named like `MacOSX*.sdk.tar.*`.

The authoritative instructions are in [osxcross README.SDK.md](https://github.com/tpoechtrager/osxcross/blob/master/README.SDK.md).

On macOS, run one of these from the osxcross checkout:

```sh
./tools/gen_sdk_package.sh
./tools/gen_sdk_package_tools.sh
```

On Linux or WSL, use the Xcode `.xip` with:

```sh
./tools/gen_sdk_package_pbzx.sh /path/to/Xcode.xip
```

For the Command Line Tools `.dmg`, this repository provides a Dockerized extractor. Docker Desktop must be running:

```powershell
$clt = Get-ChildItem "$env:USERPROFILE\Downloads\Command_Line_Tools_for_Xcode_*.dmg" |
    Select-Object -First 1
if ($null -eq $clt) {
    throw "Command Line Tools .dmg was not found in Downloads"
}
$env:MACOS_CLT_DMG = $clt.FullName

just macos_sdk_setup
```

The extractor reads the `.dmg` through a read-only Docker mount and writes exactly one `MacOSX*.sdk.tar.*` archive to `artifacts/macos-sdk`. It does not copy the Apple SDK into the Docker image.

### 3. Validate the SDK context

`just macos_sdk_setup` uses `artifacts/macos-sdk` as the default context. The default SDK version is `15.4`:

```powershell
$env:MACOS_SDK_VERSION = "15.4"
just macos_sdk_setup
just macos_sdk_check
```

To use another context directory, set `MACOS_SDK_CONTEXT` before running the check or build:

```powershell
$env:MACOS_SDK_CONTEXT = "C:\path\to\osxcross-sdk"
just macos_sdk_check
```

The context must contain exactly one `MacOSX*.sdk.tar.*` archive directly inside the directory.

### 4. Build and validate both architectures

Build and validate both macOS architectures with:

```powershell
just ghostty_macos
```

That recipe runs both architecture-specific validation paths:

- `build_macos_arm64` and `validate_macos_arm64`
- `build_macos_x64` and `validate_macos_x64`

Each validator checks Mach-O architecture, the macOS 11.0 deployment floor, Ghostty and PTY ABI exports, a C ABI link probe, and the native output files.

To run the ARM64 package inspection workflow, including a Release build and NuGet pack:

```powershell
just test_macos_arm64
```

The macOS native output directories are:

```text
artifacts/native/osx-arm64/
artifacts/native/osx-x64/
```

The x64 and ARM64 macOS libraries must be rebuilt together when changing the Ghostty ABI, the cross compiler, or the deployment target.

## Packaging

After all desired native runtime assets have been staged, create the package with:

```sh
just pack
```

The project fails the pack operation when no native assets are available or when one half of a required native asset pair is missing. The package uses the standard NuGet runtime layout:

```text
runtimes/win-x64/native/
 ghostty-vt.dll
 conpty.dll
 OpenConsole.exe
runtimes/linux-x64/native/
 libghostty-vt.so
 libmuxer-pty.so
runtimes/linux-arm64/native/
 libghostty-vt.so
 libmuxer-pty.so
runtimes/osx-x64/native/
 libghostty-vt.dylib
 libmuxer-pty.dylib
runtimes/osx-arm64/native/
 libghostty-vt.dylib
 libmuxer-pty.dylib
```

Inspect the package contents with:

```sh
unzip -l artifacts/packages/LibGhostty.Net.1.0.0.nupkg
```

The package includes `README.md` as its NuGet package readme. Build instructions remain repository documentation in this file and are not copied into the package readme.

## Publishing to NuGet

`just publish` builds the package and invokes `dotnet nuget push` with `--interactive`. The command reads source and credential settings from the normal NuGet configuration chain and can use a feed's credential provider:

```powershell
$env:NUGET_SOURCE = "https://your-feed.example/v3/index.json"
just publish
```

There is no general `nuget login` command that creates a session for package publishing. The standalone `nuget.exe setapikey` command only stores an API key in `NuGet.Config`; it is not a username/password login flow. For private feeds, use the feed's supported credential provider or configure the source credentials in NuGet configuration. Avoid storing passwords in clear text.

Publishing to nuget.org still requires nuget.org's publishing credential. For CI, prefer [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), which exchanges a GitHub Actions OIDC token for a short-lived credential. For a local nuget.org publish, use the authentication method required by nuget.org and its current package publishing policy.

The recipe uses `--skip-duplicate`, so publishing an already-existing package version is treated as a successful no-op. It does not place credentials in the Justfile or pass an API key on the command line.

## Validation expectations

Before publishing a package, run the checks applicable to the native assets being distributed:

```sh
just check
just pack
```

For macOS cross-builds, also run:

```sh
just ghostty_macos
just test_macos_arm64
```

The native validation scripts are designed to fail fast on missing files, wrong architecture, missing ABI exports, invalid deployment versions, failed C ABI linking, or missing package entries.
