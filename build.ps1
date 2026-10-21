param([Parameter(Mandatory = $true)][string]$OriginalExe)
$ErrorActionPreference = 'Stop'
$original = (Resolve-Path -LiteralPath $OriginalExe).Path
if ((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne '6B6E1260CB5A1D6A303855F1C60CAB3D0BC1AEBBA7E40A964180188B271E7386') {
    throw 'Original executable does not match the verified 2.99.0.0 baseline.'
}
Set-Location -LiteralPath $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.dotnet'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -find 'MSBuild/**/Bin/MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'Visual Studio MSBuild with WPF support is required.' }
& $msbuild 'source/Plain Craft Launcher 2/Plain Craft Launcher 2.vbproj' /t:Build /p:Configuration=Release /p:BuildProjectReferences=false /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw 'Upstream source build failed.' }
dotnet build 'tools/Rebuild.csproj' -v:q
if ($LASTEXITCODE -ne 0) { throw 'Method restoration tool build failed.' }
$tool = 'tools/bin/Debug/net9.0/Rebuild.dll'
$donor = 'source/Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe'
$output = 'artifacts/Plain Craft Launcher (PCL) 本地下载修复版.exe'
dotnet $tool merge $original $donor $output
if ($LASTEXITCODE -ne 0) { throw 'Method restoration failed.' }
dotnet $tool verify $original $output
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText((Join-Path $PSScriptRoot 'artifacts/SHA256SUMS.txt'), "$hash  Plain Craft Launcher (PCL) 本地下载修复版.exe`n", [System.Text.UTF8Encoding]::new($false))
Write-Output "Built and verified: $output"
