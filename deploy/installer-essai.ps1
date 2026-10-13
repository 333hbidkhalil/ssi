#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installation d'ESSAI de SSI sur un seul ordinateur (serveur, base et client sur le même poste), pour découvrir le logiciel.
.DESCRIPTION
  1. installe PostgreSQL 16 avec winget s'il est absent (mot de passe super-utilisateur généré) ;
  2. exécute installer-serveur.ps1 puis installer-poste.ps1 (adresse : localhost) ;
  3. écrit les identifiants dans « SSI - identifiants d'essai.txt » sur le bureau et ouvre SSI.
  Pour une station en exploitation, suivez LISEZMOI.md (serveur dédié, postes séparés, copie externe des sauvegardes).
#>
param([string]$PgBin = "", [switch]$SansLancement)
$ErrorActionPreference = "Stop"
function Etape($m) { Write-Host "`n### $m" -ForegroundColor Yellow }
function Secret([int]$n) {
    $c = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $b = New-Object byte[] $n; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    -join ($b | ForEach-Object { $c[$_ % $c.Length] })
}
function TrouverPg { Get-ChildItem "C:\Program Files\PostgreSQL\*\bin\psql.exe" -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1 }

Etape "PostgreSQL"
$mdpPostgres = $null
if (-not $PgBin) {
    $psql = TrouverPg
    if (-not $psql) {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
            throw "PostgreSQL est absent et winget n'est pas disponible : installez PostgreSQL 16 (postgresql.org/download/windows), puis relancez ce script."
        }
        $mdpPostgres = Secret 20
        Write-Host "Installation de PostgreSQL 16 (quelques minutes)…"
        winget install --id PostgreSQL.PostgreSQL.16 -e --silent --accept-package-agreements --accept-source-agreements `
            --override "--mode unattended --unattendedmodeui none --superpassword $mdpPostgres --serverport 5432"
        $psql = TrouverPg
        if (-not $psql) { throw "L'installation de PostgreSQL n'a pas abouti (code winget $LASTEXITCODE)." }
    } else { Write-Host "PostgreSQL déjà installé : $($psql.FullName)" }
    $PgBin = $psql.DirectoryName
}
Get-Service postgresql* | Where-Object Status -ne Running | Start-Service
if ($mdpPostgres) { $env:PGPASSWORD = $mdpPostgres }   # sinon installer-serveur.ps1 demande le mot de passe du super-utilisateur existant

Etape "Serveur SSI"
& (Join-Path $PSScriptRoot "installer-serveur.ps1") -PgBin $PgBin -NomServeur localhost *>&1 | Tee-Object -Variable journal
$journal | Out-File -Encoding UTF8 (Join-Path $env:TEMP "ssi-installation-essai.log")
$mdpAdmin = ($journal | Select-String "Compte initial : admin / (\S+)").Matches | Select-Object -First 1 | ForEach-Object { $_.Groups[1].Value }
if (-not $mdpAdmin) { throw "Mot de passe initial introuvable : voir $env:TEMP\ssi-installation-essai.log" }

Etape "Poste client (ce même ordinateur)"
& (Join-Path $PSScriptRoot "installer-poste.ps1") -Serveur localhost -Certificat "C:\SSI\Certificats\ssi-serveur.cer"

$fiche = Join-Path ([Environment]::GetFolderPath("Desktop")) "SSI - identifiants d'essai.txt"
@"
SSI — installation d'essai du $(Get-Date -Format 'dd/MM/yyyy HH:mm')

Ouvrir SSI : raccourci « SSI » sur le bureau.
Identifiant : admin
Mot de passe initial : $mdpAdmin
(un nouveau mot de passe de 10 caractères minimum est demandé à la première connexion)

$(if ($mdpPostgres) { "Super-utilisateur PostgreSQL : postgres / $mdpPostgres" } else { "PostgreSQL : installation existante, mot de passe inchangé." })

Supprimez ce fichier après avoir noté ces informations en lieu sûr.
"@ | Set-Content -Encoding UTF8 $fiche
Write-Host "`nInstallation d'essai terminée. Identifiants : $fiche" -ForegroundColor Green
if (-not $SansLancement) { Start-Process (Join-Path "C:\Program Files\SSI" "Ssi.Client.Wpf.exe") }
