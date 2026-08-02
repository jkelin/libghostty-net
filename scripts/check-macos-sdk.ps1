param(
    [Parameter(Mandatory = $true)]
    [string]$Context
)

$ErrorActionPreference = "Stop"

$context = $Context
if ([string]::IsNullOrWhiteSpace($context)) {
    throw "SDK context is empty"
}

if (-not (Test-Path -LiteralPath $context -PathType Container)) {
    throw "MACOS_SDK_CONTEXT is not a directory: $context"
}

$archives = @(Get-ChildItem -LiteralPath $context -File | Where-Object {
    $_.Name -like "MacOSX*.sdk.tar.*"
})

if ($archives.Count -ne 1) {
    throw "Expected exactly one MacOSX*.sdk.tar.* archive in $context, found $($archives.Count)"
}

Write-Output $archives[0].FullName
