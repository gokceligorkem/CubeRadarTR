# Local publisher: private password via Windows DPAPI; only encrypted assets go to GitHub.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$ReleaseDirectory,
    [Parameter(Mandatory)][string]$SourceZip,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$Python,
    [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')][string]$Repository,
    [string]$NotesFile,
    [switch]$Publish
)
$ErrorActionPreference='Stop'
$releasePath=(Resolve-Path -LiteralPath $ReleaseDirectory).Path
$sourcePath=(Resolve-Path -LiteralPath $SourceZip).Path
$outputPath=[IO.Path]::GetFullPath($OutputDirectory)
$exe=Get-Item -LiteralPath (Join-Path $releasePath 'CubeRadarTR.exe')
if ($exe.VersionInfo.ProductVersion -notmatch ('^'+[regex]::Escape($Version)+'(?:\+|$)')) { throw 'EXE version does not match the requested release.' }
if ($Publish) {
    if ([string]::IsNullOrWhiteSpace($Repository)) { throw 'An explicit destination repository is required. There is no default company publication target.' }
    if (-not $NotesFile -or -not (Test-Path -LiteralPath $NotesFile -PathType Leaf)) { throw 'A reviewed release-notes file is required.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI is required for command-line publication. The encrypted package can also be uploaded through Releases.' }
    & gh auth status --hostname github.com
    if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI sign-in is required. Do not paste tokens into chat.' }
    & gh release view "tr-v$Version" --repo $Repository *> $null
    if ($LASTEXITCODE -eq 0) { throw 'This release already exists; versions are immutable. Choose a new version.' }
}
$privateDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'CubeRadarPublisher'
[IO.Directory]::CreateDirectory($privateDirectory) | Out-Null
$protectedPath=Join-Path $privateDirectory 'zip-password.dpapi'
if (-not (Test-Path -LiteralPath $protectedPath)) {
    $random=[Security.Cryptography.RandomNumberGenerator]::GetBytes(24)
    $generated=[Convert]::ToBase64String($random).TrimEnd('=').Replace('+','-').Replace('/','_')
    $secure=ConvertTo-SecureString -String $generated -AsPlainText -Force
    ConvertFrom-SecureString -SecureString $secure | Set-Content -LiteralPath $protectedPath -Encoding utf8
    $generated=$null
}
$secure=(Get-Content -LiteralPath $protectedPath -Raw).Trim() | ConvertTo-SecureString
$pointer=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$previousPassword=[Environment]::GetEnvironmentVariable('CUBE_RADAR_RELEASE_PASSWORD','Process')
try {
    $password=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    [Environment]::SetEnvironmentVariable('CUBE_RADAR_RELEASE_PASSWORD',$password,'Process')
    & $Python (Join-Path $PSScriptRoot 'package_tr_release.py') --folder $releasePath --source $sourcePath --output $outputPath --version $Version
    if ($LASTEXITCODE -ne 0) { throw 'Encrypted package verification failed.' }
    # This private handoff file is OUTSIDE the public asset directory and is never uploaded.
    $passwordHandoff=Join-Path (Split-Path -Parent $outputPath) 'PRIVATE-CubeRadarTR-zip-password.txt'
    [IO.File]::WriteAllText($passwordHandoff,"Cube Radar TR ZIP parolasi`r`n`r`n$password`r`n`r`nBu dosyayi GitHub'a yukleme. Parolayi arkadaslarina ayri ilet.",[Text.UTF8Encoding]::new($false))
    Write-Output "Private password handoff: $passwordHandoff"
}
finally {
    [Environment]::SetEnvironmentVariable('CUBE_RADAR_RELEASE_PASSWORD',$previousPassword,'Process')
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    $password=$null;$secure.Dispose()
}
if ($Publish) {
    $zip=Join-Path $outputPath 'CubeRadarTR.zip';$sum=Join-Path $outputPath 'SHA256SUMS.txt'
    & gh release create "tr-v$Version" $zip $sum --repo $Repository --title "Cube Radar TR $Version" --notes-file $NotesFile --draft
    if ($LASTEXITCODE -ne 0) { throw 'Draft upload failed; no new public release was published.' }
    $release=& gh release view "tr-v$Version" --repo $Repository --json assets | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $release.assets.Count -ne 2) { throw 'Draft verification failed; inspect the draft before publishing.' }
    foreach ($asset in $release.assets) {
        if ($asset.name -notin @('CubeRadarTR.zip','SHA256SUMS.txt')) { throw 'Unexpected asset; draft remains unpublished.' }
        $local=Join-Path $outputPath $asset.name
        if ((Get-Item -LiteralPath $local).Length -ne $asset.size) { throw 'Upload size mismatch; draft remains unpublished.' }
    }
    & gh release edit "tr-v$Version" --repo $Repository --draft=false --latest
    if ($LASTEXITCODE -ne 0) { throw 'Release publication failed.' }
    Write-Output "https://github.com/$Repository/releases/latest/download/CubeRadarTR.zip"
}
