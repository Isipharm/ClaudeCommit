#Requires -Version 5.1
$ErrorActionPreference = "Stop"
$root     = $PSScriptRoot
$manifest = "$root\src\ClaudeCommit\source.extension.vsixmanifest"
$csproj   = "$root\src\ClaudeCommit\ClaudeCommit.csproj"

# bump patch version in manifest
[xml]$xml = Get-Content $manifest
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("vs", "http://schemas.microsoft.com/developer/vsx-schema/2011")
$identity = $xml.SelectSingleNode("//vs:Identity", $ns)
$ver    = [System.Version]$identity.Version
$newVer = "$($ver.Major).$($ver.Minor).$($ver.Build + 1)"
$identity.Version = $newVer
$xml.Save($manifest)
Write-Host "Version bumped to $newVer"

# delete stale VSIX so CreateVsixContainer always regenerates
$vsix = "$root\src\ClaudeCommit\bin\Release\net472\ClaudeCommit.vsix"
Remove-Item $vsix -ErrorAction SilentlyContinue

# build
Write-Host "Building..."
& dotnet build $csproj -c Release -nologo /v:m
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
if (-not (Test-Path $vsix)) { Write-Error "VSIX not generated"; exit 1 }
Write-Host "VSIX ready: $vsix"

# wait for all VS instances to close before installing
$installers = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe",
    "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\VSIXInstaller.exe"
) | Where-Object { Test-Path $_ }

if ($installers.Count -eq 0) {
    Write-Warning "No VSIXInstaller.exe found — is VS installed?"
    exit 1
}

$running = Get-Process devenv -ErrorAction SilentlyContinue
if ($running) {
    Write-Host ""
    Write-Host "Visual Studio instances currently open:" -ForegroundColor Yellow
    $running | ForEach-Object { Write-Host "  [$($_.Id)] $($_.MainWindowTitle)" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Close ALL Visual Studio windows, then press ENTER to install..." -ForegroundColor Cyan
    $null = Read-Host
}

# verify VS is actually gone before proceeding
$retries = 0
while ((Get-Process devenv -ErrorAction SilentlyContinue) -and $retries -lt 30) {
    Write-Host "  Waiting for devenv.exe to exit..." -ForegroundColor DarkYellow
    Start-Sleep -Seconds 2
    $retries++
}
if (Get-Process devenv -ErrorAction SilentlyContinue) {
    Write-Error "devenv.exe still running after wait — installation aborted."
    exit 1
}

# install into each VS instance
foreach ($installer in $installers) {
    Write-Host "Installing via $installer ..."
    $proc = Start-Process -FilePath $installer -ArgumentList "/quiet `"$vsix`"" -Wait -PassThru
    if ($proc.ExitCode -eq 0) {
        Write-Host "  OK" -ForegroundColor Green
    } else {
        Write-Host "  Exit code $($proc.ExitCode) - check %TEMP%\dd_VSIXInstaller_*.log" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Done. Restart VS to activate version $newVer." -ForegroundColor Green
