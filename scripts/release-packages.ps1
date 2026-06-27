param(
    [string]$Configuration = "Release",
    [string]$Source = "Nexus",
    [string]$Output = "artifacts\packages"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:NEXUS_API_KEY)) {
    throw "NEXUS_API_KEY environment variable is required."
}

$root = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $root $Output

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath | Out-Null

dotnet restore (Join-Path $root "Lan.Shapes.sln")

dotnet build (Join-Path $root "Lan.Shapes.sln") `
    --configuration $Configuration `
    --no-restore

$projects = @(
    "src\Lan.ImageViewer\Lan.ImageViewer.csproj",
    "src\Lan.ImageViewer.Prism\Lan.ImageViewer.Prism.csproj"
)

foreach ($project in $projects) {
    dotnet pack (Join-Path $root $project) `
        --configuration $Configuration `
        --no-restore `
        --output $outputPath
}

dotnet nuget push (Join-Path $outputPath "*.nupkg") `
    --source $Source `
    --api-key $env:NEXUS_API_KEY `
    --skip-duplicate
