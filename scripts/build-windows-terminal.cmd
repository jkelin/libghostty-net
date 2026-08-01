@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0.."
for %%I in ("%ROOT%") do set "ROOT=%%~fI"
set "WINDOWS_TERMINAL_ROOT=%ROOT%\native\windows-terminal"
set "OVERLAY_DIRECTORY=%ROOT%\artifacts\vcpkg-overlays"
set "NATIVE_DIRECTORY=%ROOT%\artifacts\native\win-x64"

if not exist "%WINDOWS_TERMINAL_ROOT%\OpenConsole.slnx" (
  echo Windows Terminal submodule is missing. Initialize native/windows-terminal first. 1>&2
  exit /b 1
)

for %%I in ("%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe") do set "VSWHERE=%%~sI"
if not exist "%VSWHERE%" (
  echo Visual Studio discovery tool is missing: %VSWHERE% 1>&2
  exit /b 1
)

set "VS_INSTALLATION_PATH="
for /f "usebackq delims=" %%I in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do if not defined VS_INSTALLATION_PATH set "VS_INSTALLATION_PATH=%%I"
if not defined VS_INSTALLATION_PATH (
  echo No Visual Studio installation with the C++ toolchain was found. 1>&2
  exit /b 1
)

set "VCVARS=%VS_INSTALLATION_PATH%\VC\Auxiliary\Build\vcvars64.bat"
if not exist "%VCVARS%" (
  echo Visual Studio C++ environment script is missing: %VCVARS% 1>&2
  exit /b 1
)

call "%VCVARS%"
if errorlevel 1 (
  echo Visual Studio C++ environment setup failed. 1>&2
  exit /b 1
)

set "PLATFORM_TOOLSET=v143"
set "VISUAL_STUDIO_VERSION=17.0"
for /f "tokens=1-2 delims=." %%A in ("%VCToolsVersion%") do (
  set "VC_MAJOR=%%A"
  set "VC_MINOR=%%B"
)
if "%VC_MAJOR%"=="14" if %VC_MINOR% GEQ 50 (
  set "PLATFORM_TOOLSET=v145"
  set "VISUAL_STUDIO_VERSION=18.0"
)

set "Platform=x64"
set "VCPKG_PLATFORM_TOOLSET=%PLATFORM_TOOLSET%"
if not exist "%OVERLAY_DIRECTORY%" mkdir "%OVERLAY_DIRECTORY%"
> "%OVERLAY_DIRECTORY%\x64-windows-static.cmake" echo set(VCPKG_TARGET_ARCHITECTURE x64)
>> "%OVERLAY_DIRECTORY%\x64-windows-static.cmake" echo set(VCPKG_CRT_LINKAGE static)
>> "%OVERLAY_DIRECTORY%\x64-windows-static.cmake" echo set(VCPKG_LIBRARY_LINKAGE static)
>> "%OVERLAY_DIRECTORY%\x64-windows-static.cmake" echo set(VCPKG_PLATFORM_TOOLSET %PLATFORM_TOOLSET%)

pushd "%WINDOWS_TERMINAL_ROOT%"
"%WINDOWS_TERMINAL_ROOT%\dep\nuget\nuget.exe" restore "%WINDOWS_TERMINAL_ROOT%\dep\nuget\packages.config"
if errorlevel 1 (
  popd
  echo Windows Terminal packages.config restore failed. 1>&2
  exit /b 1
)

msbuild.exe ".\src\winconpty\dll\winconptydll.vcxproj" /m "/p:SolutionDir=%WINDOWS_TERMINAL_ROOT%/" /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=%VISUAL_STUDIO_VERSION% /p:PlatformToolset=%PLATFORM_TOOLSET% "/p:VcpkgAdditionalInstallOptions=--x-feature=terminal --overlay-triplets=%OVERLAY_DIRECTORY%"
if errorlevel 1 (
  popd
  echo Windows Terminal Release build failed: winconptydll.vcxproj 1>&2
  exit /b 1
)

msbuild.exe ".\src\host\exe\Host.EXE.vcxproj" /m "/p:SolutionDir=%WINDOWS_TERMINAL_ROOT%/" /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=%VISUAL_STUDIO_VERSION% /p:PlatformToolset=%PLATFORM_TOOLSET% "/p:VcpkgAdditionalInstallOptions=--x-feature=terminal --overlay-triplets=%OVERLAY_DIRECTORY%"
if errorlevel 1 (
  popd
  echo Windows Terminal Release build failed: Host.EXE.vcxproj 1>&2
  exit /b 1
)
popd

if not exist "%NATIVE_DIRECTORY%" mkdir "%NATIVE_DIRECTORY%"
copy /y "%WINDOWS_TERMINAL_ROOT%\bin\x64\Release\conpty.dll" "%NATIVE_DIRECTORY%\conpty.dll" >nul
if errorlevel 1 exit /b 1
copy /y "%WINDOWS_TERMINAL_ROOT%\bin\x64\Release\OpenConsole.exe" "%NATIVE_DIRECTORY%\OpenConsole.exe" >nul
if errorlevel 1 exit /b 1
