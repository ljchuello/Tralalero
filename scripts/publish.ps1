param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

# Tags are passed as v0.1.0 by the release workflow; MSBuild wants 0.1.0.
$Version = $Version -replace '^[vV]', ''

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"
$zip = Join-Path $artifacts "Tralalero-$Version-$Runtime.zip"
$project = Join-Path $root "Tralalero\Tralalero.csproj"

Write-Host "📦 Publishing Tralalero $Version for $Runtime…"

if (Test-Path $publish) {
    Remove-Item $publish -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publish | Out-Null

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:Version=$Version `
    -o $publish

Copy-Item (Join-Path $root "README.md") $publish -Force
Copy-Item (Join-Path $root "LICENSE") $publish -Force
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") $publish -Force
Copy-Item (Join-Path $root "docs") (Join-Path $publish "docs") -Recurse -Force

if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip -Force
Write-Host "✅ Release created: $zip"
