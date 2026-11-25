param(
    [Parameter(Mandatory)]
    [string]$WixDirectory
)

$ErrorActionPreference = 'Stop'
$candle = Join-Path $WixDirectory 'candle.exe'
$light = Join-Path $WixDirectory 'light.exe'
$uiExtension = Join-Path $WixDirectory 'WixUIExtension.dll'
foreach ($tool in @($candle, $light, $uiExtension)) {
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) {
        throw "WiX Toolset 3.14 file is missing: $tool"
    }
}

$projectDirectory = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectDirectory 'Countdown.csproj'
$project = [xml](Get-Content -LiteralPath $projectPath -Raw)
$version = $project.SelectSingleNode('/Project/PropertyGroup/Version').InnerText
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Countdown.csproj must declare a three-part Version for the MSI.'
}
$releaseDirectory = Join-Path $projectDirectory "bin\Release\v$version"
$appDirectory = Join-Path $releaseDirectory 'app'
$workDirectory = Join-Path $projectDirectory "obj\installer\v$version"
$installerSource = Join-Path $PSScriptRoot 'Countdown.wxs'
$installerObject = Join-Path $workDirectory 'Countdown.wixobj'
$installerPath = Join-Path $releaseDirectory 'Countdown-Setup.msi'

dotnet build $projectPath -c Release -o $appDirectory --nologo
if ($LASTEXITCODE -ne 0) { throw 'Countdown build failed.' }

New-Item -ItemType Directory -Path $workDirectory -Force | Out-Null
& $candle -nologo -arch x86 "-dVersion=$version" "-dAppDirectory=$appDirectory" -out $installerObject $installerSource
if ($LASTEXITCODE -ne 0) { throw 'WiX compilation failed.' }

& $light -nologo -ext $uiExtension -cultures:ja-jp -pdbout (Join-Path $workDirectory 'Countdown.wixpdb') `
    -out $installerPath $installerObject
if ($LASTEXITCODE -ne 0) { throw 'WiX linking failed.' }

Get-Item -LiteralPath $installerPath
