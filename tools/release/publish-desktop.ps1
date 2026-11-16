<#
.SYNOPSIS
    Builds the Toolkit desktop installer and update packages (Velopack). See docs/RELEASE.md.

.DESCRIPTION
    1. dotnet publish for Windows x64 with .NET included (self-contained).
    2. Writes the license server address and public key into appsettings.Desktop.json of the
       published copy (not of the repository) and adds toolkit.desktop, which starts it as the
       desktop app.
    3. vpk pack: ToolkitApp-win-Setup.exe, the full (and delta) .nupkg packages and releases.win.json
       in the output folder. Keep that folder between releases: deltas are made from the previous one.
    4. Optionally copies the output to the updates folder of the license server.

.EXAMPLE
    ./tools/release/publish-desktop.ps1 -Version 1.1.0 -ServerUrl https://license.example.com -PublicKey "MFkwEwYH..."

.EXAMPLE
    # Signing later, with a certificate (Setup.exe and the app files are signed):
    ./tools/release/publish-desktop.ps1 -Version 1.1.0 -ServerUrl ... -PublicKey ... `
        -SignParams '/a /fd sha256 /tr http://timestamp.digicert.com /td sha256 /f C:\certs\toolkit.pfx /p <password>'
#>
param(
    [Parameter(Mandatory = $true)][string] $Version,
    [Parameter(Mandatory = $true)][string] $ServerUrl,
    # The one-line public key from the license server's "Signing key" page (or -PublicKeyFile).
    [string] $PublicKey,
    [string] $PublicKeyFile,
    [string] $OutputDir = "artifacts/releases",
    # Release notes in Markdown (optional).
    [string] $NotesFile,
    # signtool arguments (Windows only). Empty: the installer is not signed (see docs/RELEASE.md).
    [string] $SignParams,
    # Optional: the updates folder of the license server (for example \\server\ToolkitData\License\updates).
    [string] $CopyTo,
    [string] $VpkVersion = "1.2.161"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $root

if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 1.2.0" }
if (-not $PublicKey -and $PublicKeyFile) { $PublicKey = (Get-Content $PublicKeyFile -Raw).Trim() }
if (-not $PublicKey) { throw "Give -PublicKey or -PublicKeyFile (license server > Signing key)." }

$csproj = "src/ToolboxWeb.Web/ToolboxWeb.Web.csproj"
$projectVersion = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if ($projectVersion -ne $Version) {
    Write-Warning "The csproj says <Version>$projectVersion</Version>; this release is $Version. Raise <Version> in the csproj so the web copies report the same number."
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    dotnet tool install -g vpk --version $VpkVersion
}

$publish = Join-Path "artifacts/publish" $Version
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# The published copy is the desktop app: license settings and the desktop marker.
$settingsPath = Join-Path $publish "appsettings.Desktop.json"
$settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
$settings.License.ServerUrl = $ServerUrl.TrimEnd('/')
$settings.License.PublicKey = $PublicKey
$settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding utf8
Set-Content (Join-Path $publish "toolkit.desktop") "This copy starts as the Toolkit desktop app (DesktopHost.FlagFileName)." -Encoding utf8

$pack = @(
    "pack",
    # Velopack installs to %LOCALAPPDATA%\<packId>. "ToolkitApp" keeps it apart from the data folder
    # %LOCALAPPDATA%\Toolkit, so uninstalling never deletes the data.
    "--packId", "ToolkitApp",
    "--packVersion", $Version,
    "--packDir", $publish,
    "--mainExe", "ToolboxWeb.Web.exe",
    "--packTitle", "Toolkit",
    "--packAuthors", "Toolkit",
    "--icon", "docs/brand/toolkit.ico",
    "--outputDir", $OutputDir
)
if ($NotesFile) { $pack += @("--releaseNotes", $NotesFile) }
if ($SignParams) { $pack += @("--signParams", $SignParams) }
vpk @pack
if ($LASTEXITCODE -ne 0) { throw "vpk pack failed" }

if ($CopyTo) {
    Copy-Item (Join-Path $OutputDir "*") $CopyTo -Force
    Write-Host "Copied to $CopyTo"
}

Write-Host ""
Write-Host "Done. Upload the files of $OutputDir on the license server (Releases > Installer and update packages),"
Write-Host "then publish version $Version there so installed copies see it."
