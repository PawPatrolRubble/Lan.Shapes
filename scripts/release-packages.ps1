param(
    [string]$Configuration = "Release",
    [string]$Source = "Nexus",
    [string]$Output = "artifacts\packages",
    [switch]$PackOnly
)

$ErrorActionPreference = "Stop"

if (-not $PackOnly -and [string]::IsNullOrWhiteSpace($env:NEXUS_API_KEY)) {
    throw "NEXUS_API_KEY environment variable is required."
}

$root = Split-Path -Parent $PSScriptRoot
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $root $Output))
$rootPrefix = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

if (-not $outputPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Output must be a directory inside the repository: $outputPath"
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath | Out-Null

dotnet restore (Join-Path $root "Lan.Shapes.sln")
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

dotnet build (Join-Path $root "Lan.Shapes.sln") `
    --configuration $Configuration `
    --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

$projects = @(
    "src\Lan.ImageViewer\Lan.ImageViewer.csproj",
    "src\Lan.ImageViewer.Prism\Lan.ImageViewer.Prism.csproj"
)

foreach ($project in $projects) {
    dotnet pack (Join-Path $root $project) `
        --configuration $Configuration `
        --no-restore `
        --output $outputPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed for $project with exit code $LASTEXITCODE." }
}

if ($PackOnly) {
    Write-Output "Packages prepared in $outputPath"
    return
}

dotnet nuget push (Join-Path $outputPath "*.nupkg") `
    --source $Source `
    --api-key $env:NEXUS_API_KEY `
    --skip-duplicate
if ($LASTEXITCODE -ne 0) { throw "dotnet nuget push failed with exit code $LASTEXITCODE." }
