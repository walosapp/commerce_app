[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0',

    [Parameter(Mandatory)]
    [switch]$ConfirmDisposableUserProfile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = $PSScriptRoot
$artifactsRoot = Join-Path $scriptRoot 'artifacts'
$installer = Join-Path $artifactsRoot "smoke-installer\Walos-Agent-Smoke-Setup-$Version.exe"
$sandbox = Join-Path $artifactsRoot 'smoke-install'
$stateDirectory = Join-Path $env:LOCALAPPDATA 'Walos\PrintAgent'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'WalosPrintAgent'
$agentProcessId = $null
$uninstaller = Join-Path $sandbox 'unins000.exe'
$sentinel = Join-Path $stateDirectory 'h31-smoke-sentinel.txt'

function Assert-PathUnder([string]$Path, [string]$Parent) {
    $absolutePath = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $absoluteParent = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (-not $absolutePath.StartsWith($absoluteParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "La ruta '$absolutePath' está fuera de '$absoluteParent'."
    }
}

function Wait-AgentHealth([int]$Attempts = 30) {
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return Invoke-RestMethod -Uri 'http://127.0.0.1:17831/v1/health' -TimeoutSec 1
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    throw 'El agente instalado no respondió en 127.0.0.1:17831.'
}

function Get-InstalledAgentProcessId([string]$ExpectedExecutable) {
    $listener = @(Get-NetTCPConnection -LocalAddress '127.0.0.1' -LocalPort 17831 -State Listen -ErrorAction SilentlyContinue)
    if ($listener.Count -ne 1) {
        throw "Se esperaba un único listener loopback y se encontraron $($listener.Count)."
    }

    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($listener[0].OwningProcess)"
    if ($null -eq $process -or
        -not [IO.Path]::GetFullPath($process.ExecutablePath).Equals(
            [IO.Path]::GetFullPath($ExpectedExecutable),
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "El listener no pertenece al agente instalado en '$ExpectedExecutable'."
    }

    return [int]$listener[0].OwningProcess
}

function Stop-InstalledAgent([string]$ExpectedExecutable) {
    $expected = [IO.Path]::GetFullPath($ExpectedExecutable)
    Get-CimInstance Win32_Process -Filter "Name='Walos.PrintAgent.exe'" |
        Where-Object {
            $_.ExecutablePath -and
            [IO.Path]::GetFullPath($_.ExecutablePath).Equals($expected, [StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force
            Wait-Process -Id $_.ProcessId -ErrorAction SilentlyContinue
        }
}

if (-not $ConfirmDisposableUserProfile) {
    throw 'Este smoke test solo se ejecuta en una cuenta Windows descartable o VM.'
}
if (-not (Test-Path -LiteralPath $installer)) {
    throw "No existe el instalador. Ejecutá Build-Installer.ps1 primero: $installer"
}
if (Test-Path -LiteralPath $stateDirectory) {
    throw "Existe configuración en '$stateDirectory'. El smoke test se niega a copiarla, borrarla o reemplazarla. Usá una cuenta/VM limpia."
}
if ((Test-Path -LiteralPath $runKey) -and
    ($null -ne (Get-ItemProperty -LiteralPath $runKey -Name $runValueName -ErrorAction SilentlyContinue))) {
    throw 'Ya existe el autoarranque WalosPrintAgent. Usá una cuenta/VM limpia.'
}
if (Get-NetTCPConnection -LocalPort 17831 -State Listen -ErrorAction SilentlyContinue) {
    throw 'El puerto 17831 ya está ocupado. El smoke test no detiene procesos ajenos.'
}

Assert-PathUnder $sandbox $scriptRoot
if (Test-Path -LiteralPath $sandbox) {
    Remove-Item -LiteralPath $sandbox -Recurse -Force
}

try {
    Write-Host 'Instalando silenciosamente en sandbox /CURRENTUSER...'
    $install = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS', "/DIR=$sandbox"
    ) -Wait -PassThru -WindowStyle Hidden
    if ($install.ExitCode -ne 0) { throw "El instalador devolvió $($install.ExitCode)." }

    $installedExe = Join-Path $sandbox 'Walos.PrintAgent.exe'
    if (-not (Test-Path -LiteralPath $installedExe)) { throw 'No se instaló Walos.PrintAgent.exe.' }
    if (-not (Test-Path -LiteralPath $uninstaller)) { throw 'No se registró el desinstalador.' }

    $runValue = (Get-ItemProperty -LiteralPath $runKey -Name $runValueName).$runValueName
    $expectedRunValue = '"' + $installedExe + '" --autostart'
    if ($runValue -ne $expectedRunValue) {
        throw "La entrada HKCU de smoke no apunta al agente instalado: $runValue"
    }

    # [Run] must start the tray even during a silent install.
    $health = Wait-AgentHealth
    if ($health.status -ne 'ok' -or $health.version -ne $Version) {
        throw "Health inesperado: $($health | ConvertTo-Json -Compress)"
    }
    $agentProcessId = Get-InstalledAgentProcessId $installedExe

    $listeners = @(Get-NetTCPConnection -OwningProcess $agentProcessId -State Listen)
    if ($listeners.Count -ne 1 -or $listeners[0].LocalAddress -ne '127.0.0.1' -or $listeners[0].LocalPort -ne 17831) {
        throw "Binding inseguro o inesperado: $($listeners | ConvertTo-Json -Compress)"
    }

    [IO.File]::WriteAllText($sentinel, 'preserve-h31', [Text.Encoding]::ASCII)
    $sentinelHash = (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash

    Write-Host 'Reinstalando mientras el agente está activo; debe cerrarlo, conservar el estado y relanzarlo...'
    $previousProcessId = $agentProcessId
    $reinstall = Start-Process -FilePath $installer -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS', "/DIR=$sandbox"
    ) -Wait -PassThru -WindowStyle Hidden
    if ($reinstall.ExitCode -ne 0) { throw "La reinstalación devolvió $($reinstall.ExitCode)." }

    $health = Wait-AgentHealth
    $agentProcessId = Get-InstalledAgentProcessId $installedExe
    if ($agentProcessId -eq $previousProcessId) { throw 'La reinstalación no reemplazó la instancia anterior.' }
    if (-not (Test-Path -LiteralPath $sentinel) -or
        (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) {
        throw 'La reinstalación alteró el estado local.'
    }

    Stop-InstalledAgent $installedExe
    $agentProcessId = $null

    Write-Host 'Simulando el comando de autoarranque registrado...'
    Start-Process -FilePath $installedExe -ArgumentList '--autostart' | Out-Null
    $health = Wait-AgentHealth
    $agentProcessId = Get-InstalledAgentProcessId $installedExe
    if ($health.status -ne 'ok' -or $health.version -ne $Version) {
        throw "Health inesperado al simular autoarranque: $($health | ConvertTo-Json -Compress)"
    }

    Stop-InstalledAgent $installedExe
    $agentProcessId = $null

    Write-Host 'Desinstalando normalmente; el estado creado debe conservarse...'
    $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -PassThru -WindowStyle Hidden
    if ($uninstall.ExitCode -ne 0) { throw "El desinstalador devolvió $($uninstall.ExitCode)." }
    if (Test-Path -LiteralPath $installedExe) { throw 'El ejecutable sigue instalado.' }
    if (-not (Test-Path -LiteralPath $sentinel) -or
        (Get-FileHash -LiteralPath $sentinel -Algorithm SHA256).Hash -ne $sentinelHash) {
        throw 'La desinstalación normal no conservó el estado.'
    }
    if ($null -ne (Get-ItemProperty -LiteralPath $runKey -Name $runValueName -ErrorAction SilentlyContinue)) {
        throw 'La desinstalación no eliminó su entrada de autoarranque.'
    }

    Write-Host 'Smoke sandbox: OK (instalación silenciosa, relanzamiento, reinstalación, estado, health/version, loopback, autoarranque HKCU y desinstalación).'
    Write-Warning "El estado de prueba quedó conservado deliberadamente en '$stateDirectory'."
    Write-Warning 'Este smoke NO valida UAC, Program Files, HKLM Run, limpieza explícita ni logoff/reinicio real.'
} finally {
    if (Test-Path -LiteralPath (Join-Path $sandbox 'Walos.PrintAgent.exe')) {
        Stop-InstalledAgent (Join-Path $sandbox 'Walos.PrintAgent.exe')
    }
    if (Test-Path -LiteralPath $uninstaller) {
        Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') -Wait -WindowStyle Hidden
    }
    if (Test-Path -LiteralPath $sandbox) {
        Assert-PathUnder $sandbox $scriptRoot
        Remove-Item -LiteralPath $sandbox -Recurse -Force
    }
}
