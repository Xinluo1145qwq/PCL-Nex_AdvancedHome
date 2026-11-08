param(
    [string] $Configuration = 'Release',
    [string] $SdkDir,
    [string] $DistDir,
    [switch] $NoPack,
    [switch] $Deploy
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\AdvancedHomepage'

if (-not $SdkDir) { $SdkDir = Join-Path $root 'sdk\current' }
$core = Join-Path $SdkDir 'PCL.Core.dll'
$main = Join-Path $SdkDir 'Plain Craft Launcher 2.dll'
if (-not (Test-Path -LiteralPath $core) -or -not (Test-Path -LiteralPath $main)) {
    throw "找不到启动器 DLL：$SdkDir —— 请把 PCL.Core.dll 与 Plain Craft Launcher 2.dll 放进 sdk\current\，或用 -SdkDir 指定目录。"
}

$dotnet = Join-Path $root '.dotnet10\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }

& $dotnet build (Join-Path $project 'AdvancedHomepage.csproj') -c $Configuration --nologo -v q `
    -p:PclCorePath=$core -p:PclMainPath=$main
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$stage = Join-Path $root 'artifacts\stage'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'lib'), (Join-Path $stage 'mixins') | Out-Null

Copy-Item -Force (Join-Path $project 'package\plugin.json') $stage
Copy-Item -Force (Join-Path $project 'package\advanced-homepage.mixins.json') (Join-Path $stage 'mixins')
if (Test-Path (Join-Path $project 'package\README.md')) {
    Copy-Item -Force (Join-Path $project 'package\README.md') $stage
}
Copy-Item -Force (Join-Path $project "bin\$Configuration\PCLN.AdvancedHomepage.dll") (Join-Path $stage 'lib')
$pdb = Join-Path $project "bin\$Configuration\PCLN.AdvancedHomepage.pdb"
if (Test-Path $pdb) { Copy-Item -Force $pdb (Join-Path $stage 'lib') }

# 本地调试部署（覆盖式，不删除目录）；启动器运行时 DLL 被占用，只提示不中断
if ($Deploy) {
    $pluginDir = Join-Path $root 'PCL\Plugins\com.pcln.advancedhomepage'
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
    try {
        Copy-Item -Path (Join-Path $stage '*') -Destination $pluginDir -Recurse -Force
        Write-Host "deployed -> $pluginDir"
    }
    catch {
        Write-Warning "部署失败（启动器可能正在运行并占用了 DLL）：$($_.Exception.Message)"
    }
}

if ($NoPack) { return }

if (-not $DistDir) { $DistDir = Join-Path $root 'dist' }
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$version = (Get-Content (Join-Path $project 'package\plugin.json') -Raw -Encoding UTF8 | ConvertFrom-Json).version
$artifact = Join-Path $DistDir ("AdvancedHomepage-{0}.pclx" -f $version)
$temp = Join-Path $env:TEMP ("advancedhomepage-{0}.zip" -f ([Guid]::NewGuid().ToString('N')))
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $temp
[IO.File]::Copy($temp, $artifact, $true)
[IO.File]::Delete($temp)
Write-Host "packed -> $artifact"
Write-Host ("sha256 " + (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant())

# 本地习惯存放目录（存在才复制，仓库里已忽略）
$localDir = Join-Path $root '插件做完之后放这里'
if (Test-Path -LiteralPath $localDir) {
    $localName = Join-Path $localDir ("高级主页-{0}.pclx" -f $version)
    Copy-Item -LiteralPath $artifact -Destination $localName -Force
    Write-Host "copied -> $localName"
}
