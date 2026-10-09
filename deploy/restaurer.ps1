#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Restaure une sauvegarde SSI dans une NOUVELLE base (jamais par-dessus la base en service), vérifie l'empreinte SHA-256 et les
  totaux de contrôle enregistrés lors de la sauvegarde. La bascule de l'API vers la base restaurée est une décision explicite (-Basculer).
.EXAMPLE
  .\restaurer.ps1 -Fichier C:\SSI\Sauvegardes\ssi-ssi-20261009-233000.dump -Cible ssi_restauree
  .\restaurer.ps1 -Fichier ... -Cible ssi_restauree -Basculer
#>
param(
    [Parameter(Mandatory)][string]$Fichier,
    [Parameter(Mandatory)][string]$Cible,
    [string]$Dossier = "C:\SSI",
    [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
    [string]$PgSuperUtilisateur = "postgres",
    [switch]$Basculer
)
$ErrorActionPreference = "Stop"
$ext = if ($IsLinux -or $IsMacOS) { "" } else { ".exe" }
$psql = Join-Path $PgBin "psql$ext"; $pgrestore = Join-Path $PgBin "pg_restore$ext"
$controles = Join-Path $PSScriptRoot "controles.sql"
if (-not $env:PGHOST) { $env:PGHOST = "127.0.0.1" }
$env:PGUSER = $PgSuperUtilisateur

if (Test-Path "$Fichier.sha256") {
    if ((Get-FileHash -Algorithm SHA256 $Fichier).Hash.ToLower() -ne (Get-Content -Raw "$Fichier.sha256").Trim()) {
        throw "L'empreinte SHA-256 ne correspond pas : fichier altéré ou incomplet. Restauration refusée."
    }
} else { Write-Warning "Pas d'empreinte SHA-256 à côté du fichier : intégrité non vérifiable." }

if ((& $psql -X -A -t -d postgres -c "SELECT 1 FROM pg_database WHERE datname = '$Cible'") -eq "1") {
    throw "La base $Cible existe déjà. Choisissez un nouveau nom : la restauration n'écrase jamais une base."
}
& $psql -X -q -v ON_ERROR_STOP=1 -d postgres -c "CREATE DATABASE $Cible OWNER ssi_app ENCODING 'UTF8' TEMPLATE template0;" | Out-Null
& $pgrestore --no-owner --role=ssi_app --exit-on-error -d $Cible $Fichier
if ($LASTEXITCODE -ne 0) { throw "pg_restore a échoué (code $LASTEXITCODE). La base $Cible est incomplète : supprimez-la avant de réessayer." }

if (Test-Path "$Fichier.controles.txt") {
    $attendu = (Get-Content -Raw -Encoding UTF8 "$Fichier.controles.txt").Trim() -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ }
    $obtenu = @(& $psql -X -q -A -t -d $Cible -f $controles | Where-Object { $_ })
    if (Compare-Object $attendu $obtenu) { throw "Totaux de contrôle différents de ceux de la sauvegarde : ne basculez pas sur cette base." }
    Write-Host "Totaux de contrôle identiques à ceux de la sauvegarde." -ForegroundColor Green
}

if ($Basculer) {
    $config = Join-Path $Dossier "Api\appsettings.Production.json"
    Copy-Item $config "$config.$(Get-Date -Format yyyyMMddHHmmss).bak"
    $json = Get-Content -Raw $config | ConvertFrom-Json
    $json.ConnectionStrings.Ssi = $json.ConnectionStrings.Ssi -replace "Database=[^;]+", "Database=$Cible"
    $json | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $config
    Restart-Service SSIApi
    Write-Host "Le service SSI API utilise maintenant la base $Cible (ancienne configuration sauvegardée en .bak)."
} else {
    Write-Host "Base $Cible restaurée. Relancez avec -Basculer pour que l'application l'utilise."
}
