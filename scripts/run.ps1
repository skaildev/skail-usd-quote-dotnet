#!/usr/bin/env pwsh
# Builds and runs the front end (web) and the skailFunc (worker) with podman or docker.
# The skail server is the one of your environment in the skail portal (SKAIL_SIDECAR and SKAIL_BASE_URL
# in .env); nothing skail-related runs locally.
#
# Usage: scripts/run.ps1 [build|up|down|restart|logs|status]   (default: up)
# Override the engine with $env:CONTAINER_ENGINE = 'docker' or 'podman'.
# Same as scripts/run.sh, for PowerShell 7+ (pwsh): runs on Windows, Linux and macOS.
param([string]$Command = 'up')

$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')

$Engine = $env:CONTAINER_ENGINE
if (-not $Engine) {
    if (Get-Command podman -ErrorAction SilentlyContinue) { $Engine = 'podman' }
    elseif (Get-Command docker -ErrorAction SilentlyContinue) { $Engine = 'docker' }
    else { throw 'neither podman nor docker found' }
}

$Network = 'usd-quote'
$Web = 'usd-quote-web'
$Worker = 'usd-quote-worker'

if (-not (Test-Path .env)) { throw '.env not found. Run: Copy-Item .env.example .env' }
# load .env into the process environment (KEY=VALUE lines), like `set -a; . ./.env` in run.sh
foreach ($line in Get-Content .env) {
    if ($line -match '^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        $key = $Matches[1]
        $value = $Matches[2].Trim()
        if ($value -match '^"(.*)"$' -or $value -match "^'(.*)'$") { $value = $Matches[1] }
        [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
}

# ${NAME:-default}: the default also applies when the variable is empty
function Get-Setting([string]$Key, [string]$Default = '') {
    $value = [Environment]::GetEnvironmentVariable($Key)
    if ([string]::IsNullOrEmpty($value)) { $Default } else { $value }
}

# native commands do not stop the script on failure by themselves: check the exit code
function Invoke-Engine {
    & $Engine @args
    if ($LASTEXITCODE -ne 0) { throw "$Engine $($args -join ' ') failed (exit code $LASTEXITCODE)" }
}

# same, but failures and output are ignored (removing containers that may not exist, probing a network)
function Invoke-EngineQuiet {
    & $Engine @args *> $null
}

function Build-Images {
    Invoke-Engine build -f src/SkailUsdQuote.Worker/Dockerfile -t $Worker .
    Invoke-Engine build -f src/SkailUsdQuote.Web/Dockerfile -t $Web .
}

function Remove-Containers {
    # one call per container: podman stops at the first missing name and would leave the other one behind
    foreach ($container in @($Worker, $Web)) { Invoke-EngineQuiet rm -f $container }
}

function Initialize-Network {
    Invoke-EngineQuiet network inspect $Network
    if ($LASTEXITCODE -ne 0) { Invoke-Engine network create $Network | Out-Null }
}

function Start-Worker {
    Initialize-Network
    Invoke-EngineQuiet rm -f $Worker
    # resource limits for the worker: 1 GB of memory and 1 CPU
    Invoke-Engine run -d --name $Worker --network $Network --env-file .env --memory 1g --cpus 1 $Worker | Out-Null
}

function Start-Stack {
    Build-Images
    Remove-Containers
    Initialize-Network
    $port = Get-Setting 'WEB_PORT' '8080'
    Invoke-Engine run -d --name $Web --network $Network --env-file .env -p "${port}:8080" $Web | Out-Null
    Start-Worker
    Write-Host "front end: http://localhost:$port"
    Write-Host 'logs:      scripts/run.ps1 logs'
}

function Show-Logs {
    # docker logs takes a single container: fall back to the worker when the two-container form fails
    & $Engine logs -f --tail 100 $Worker $Web
    if ($LASTEXITCODE -ne 0) { & $Engine logs -f $Worker }
}

switch ($Command) {
    'build' { Build-Images }
    'up' { Start-Stack }
    'down' { Remove-Containers }
    'restart' { Invoke-Engine restart $Worker $Web }
    'logs' { Show-Logs }
    'status' { Invoke-Engine ps --filter 'name=usd-quote' }
    default {
        [Console]::Error.WriteLine('Usage: scripts/run.ps1 [build|up|down|restart|logs|status]')
        exit 1
    }
}
