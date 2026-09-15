[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [string]$AllowedOrigins,

    [switch]$DevelopmentOrigins,

    [switch]$SkipAgentTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$innoPackageVersion = '6.4.3'
$innoPackageSha256 = '9D67A2B1155CA5BC825475CEB907AA357C5FEB42E474CFD5347EDDEEF5AD92A5'
$scriptRoot = $PSScriptRoot
$toolsRoot = Split-Path -Parent $scriptRoot
$agentProject = Join-Path $toolsRoot 'Walos.PrintAgent\Walos.PrintAgent.csproj'
$testProject = Join-Path $toolsRoot 'Walos.PrintAgent.Tests\Walos.PrintAgent.Tests.csproj'
$artifactsRoot = Join-Path $scriptRoot 'artifacts'
$dotnetArtifacts = Join-Path $artifactsRoot 'dotnet'
$publishDirectory = Join-Path $artifactsRoot 'publish\win-x64'
$installerDirectory = Join-Path $artifactsRoot 'installer'
$smokeInstallerDirectory = Join-Path $artifactsRoot 'smoke-installer'
$toolchainRoot = Join-Path $scriptRoot '.toolchain'
$toolchainDirectory = Join-Path $toolchainRoot "Tools.InnoSetup.$innoPackageVersion"
$innoPackage = Join-Path $toolchainRoot "Tools.InnoSetup.$innoPackageVersion.nupkg"
$iscc = Join-Path $toolchainDirectory 'tools\ISCC.exe'
$iss = Join-Path $scriptRoot 'Walos.PrintAgent.iss'

function Assert-PathUnder([string]$Path, [string]$Parent) {
    $absolutePath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $absoluteParent = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (-not $absolutePath.StartsWith($absoluteParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "La ruta '$absolutePath' está fuera de '$absoluteParent'."
    }
}

function Reset-Directory([string]$Path) {
    Assert-PathUnder $Path $scriptRoot
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

function Assert-AllowedOrigins([string]$Value, [bool]$AllowDevelopmentHttp) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw 'AllowedOrigins es obligatorio. Indicá los orígenes HTTPS reales de Walos; para loopback local usá además -DevelopmentOrigins.'
    }
    $origins = @($Value.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() })
    if ($origins.Count -eq 0) { throw 'AllowedOrigins debe contener al menos un origen.' }
    foreach ($origin in $origins) {
        $uri = $null
        if (-not [Uri]::TryCreate($origin, [UriKind]::Absolute, [ref]$uri) -or
            @('http', 'https') -notcontains $uri.Scheme -or
            $origin -eq '*' -or
            $uri.AbsolutePath -ne '/' -or
            $uri.Query -or
            $uri.Fragment) {
            throw "Origen no válido: '$origin'. Usá solo scheme + host + puerto opcional."
        }
        if ($uri.Scheme -ne 'https') {
            $isLoopback = $uri.Host -in @('localhost', '127.0.0.1', '::1')
            if (-not ($AllowDevelopmentHttp -and $isLoopback)) {
                throw "Origen inseguro '$origin'. Los releases exigen HTTPS; solo loopback de desarrollo admite HTTP con -DevelopmentOrigins."
            }
        }
    }
}

function Install-LocalInnoToolchain {
    New-Item -ItemType Directory -Path $toolchainRoot -Force | Out-Null
    $url = "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$innoPackageVersion/tools.innosetup.$innoPackageVersion.nupkg"

    if (-not (Test-Path -LiteralPath $innoPackage)) {
        Write-Host "Descargando toolchain Inno Setup fijado $innoPackageVersion..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $url -OutFile $innoPackage
    }

    $actualHash = (Get-FileHash -LiteralPath $innoPackage -Algorithm SHA256).Hash
    if ($actualHash -ne $innoPackageSha256) {
        throw "Checksum inválido para Tools.InnoSetup $innoPackageVersion. Esperado $innoPackageSha256; recibido $actualHash."
    }

    # Never trust a previously extracted compiler. Recreate it on every build
    # from the package whose pinned checksum was verified above.
    Reset-Directory $toolchainDirectory
    $archive = Join-Path $toolchainDirectory "Tools.InnoSetup.$innoPackageVersion.zip"
    Copy-Item -LiteralPath $innoPackage -Destination $archive -Force
    Expand-Archive -LiteralPath $archive -DestinationPath $toolchainDirectory -Force
    if (-not (Test-Path -LiteralPath $iscc)) {
        throw "No se encontró ISCC.exe en el paquete fijado."
    }
}

Assert-AllowedOrigins $AllowedOrigins $DevelopmentOrigins.IsPresent
Reset-Directory $publishDirectory
Reset-Directory $installerDirectory
Reset-Directory $smokeInstallerDirectory

if (-not $SkipAgentTests) {
    # Keep release automation isolated from a development agent that may be
    # running from tools/Walos.PrintAgent/bin.
    & dotnet test $testProject -c Release --artifacts-path $dotnetArtifacts
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron los tests del Print Agent.' }
}

Write-Host 'Publicando Walos Agent self-contained/single-file para win-x64...'
& dotnet publish $agentProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --artifacts-path $dotnetArtifacts `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $publishDirectory
if ($LASTEXITCODE -ne 0) { throw 'Falló dotnet publish.' }

$agentConfiguration = @{
    WALOS_PRINT_AGENT_ALLOWED_ORIGINS = $AllowedOrigins
} | ConvertTo-Json
[IO.File]::WriteAllText(
    (Join-Path $publishDirectory 'appsettings.json'),
    $agentConfiguration,
    [Text.UTF8Encoding]::new($false))

Install-LocalInnoToolchain

Write-Host 'Compilando instalador Inno Setup...'
& $iscc "/DSourceDir=$publishDirectory" "/DOutputDir=$installerDirectory" "/DAppVersion=$Version" $iss
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador.' }

Write-Host 'Compilando variante aislada para smoke test (no distribuible)...'
& $iscc "/DSourceDir=$publishDirectory" "/DOutputDir=$smokeInstallerDirectory" "/DAppVersion=$Version" '/DSmokeTest=1' $iss
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador de smoke.' }

$installer = Join-Path $installerDirectory 'Walos-Agent-Setup.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw "No se generó $installer" }

$file = Get-Item -LiteralPath $installer
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
$signature = Get-AuthenticodeSignature -LiteralPath $installer
Write-Host ''
Write-Host "Instalador: $($file.FullName)"
Write-Host "Tamaño:     $($file.Length) bytes"
Write-Host "SHA-256:    $hash"
Write-Host "Firma:      $($signature.Status)"
if ($signature.Status -ne 'Valid') {
    Write-Warning 'Artefacto válido para pruebas internas, pero NO-GO para distribución pública hasta firmarlo con Authenticode.'
}
