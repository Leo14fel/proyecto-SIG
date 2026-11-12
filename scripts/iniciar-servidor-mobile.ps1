param(
    [int]$Port = 8080
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repositoryRoot 'src\VisorDatosSIG.Web\VisorDatosSIG.Web.csproj'

$network = Get-NetIPConfiguration |
    Where-Object { $_.IPv4DefaultGateway -and $_.IPv4Address } |
    Select-Object -First 1

if (-not $network) {
    throw 'No se encontró una conexión LAN/Wi-Fi IPv4 activa.'
}

$lanIp = $network.IPv4Address[0].IPAddress
$listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    $process = Get-Process -Id $listener[0].OwningProcess -ErrorAction SilentlyContinue
    if ($process.ProcessName -eq 'VisorDatosSIG.Web') {
        Write-Host "VisorDatosSIG ya esta iniciado (PID $($listener[0].OwningProcess))." -ForegroundColor Green
        Write-Host "Telefono: http://$lanIp`:$Port" -ForegroundColor Green
        exit 0
    }

    throw "El puerto $Port esta en uso por $($process.ProcessName) (PID $($listener[0].OwningProcess))."
}

$secrets = dotnet user-secrets list --project $webProject 2>$null
$hasUserSecret = $secrets -match '^ConnectionStrings:DefaultConnection\s*='
$hasEnvironmentConnection = -not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)
if (-not $hasUserSecret -and -not $hasEnvironmentConnection) {
    throw 'No existe ConnectionStrings:DefaultConnection en User Secrets ni como variable de entorno. Configúrala antes de iniciar el servidor.'
}

Write-Host "VisorDatosSIG se iniciara para la red local." -ForegroundColor Cyan
Write-Host "Telefono: http://$lanIp`:$Port" -ForegroundColor Green
Write-Host 'SQL: cadena de conexión configurada en User Secrets (no se muestra).' -ForegroundColor DarkGray

$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project $webProject --urls "http://0.0.0.0:$Port"
