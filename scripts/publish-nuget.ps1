[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Package,

    [Parameter(Mandatory = $true)]
    [string]$Source
)

$ErrorActionPreference = "Stop"
$apiKey = $env:NUGET_API_KEY

if ($apiKey -like "op://*") {
    throw "NUGET_API_KEY is an unresolved 1Password reference. Run just through 'op run --env-file=.env -- just publish'."
}

$arguments = @(
    "nuget",
    "push",
    $Package,
    "--source",
    $Source,
    "--skip-duplicate",
    "--interactive"
)

if (-not [string]::IsNullOrWhiteSpace($apiKey)) {
    $arguments += @("--api-key", $apiKey)
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
