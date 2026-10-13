#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installe le serveur SSI (API + base PostgreSQL) sur le poste serveur Windows du réseau local de la station.

.DESCRIPTION
  Prérequis : PostgreSQL 16 installé (installateur officiel), publication de l'API dans .\publish\api
  (dotnet publish src/Ssi.Api -c Release -r win-x64 --self-contained -o deploy/publish/api).
  Le script :
    1. crée le rôle applicatif et la base, avec des mots de passe aléatoires ;
    2. génère la clé de signature des jetons et un certificat HTTPS pour le serveur ;
    3. écrit appsettings.Production.json (lisible par les seuls administrateurs et le service) ;
    4. installe et démarre le service Windows « SSI API » ;
    5. ouvre le port HTTPS au SEUL sous-réseau local (aucun accès distant par défaut) ;
    6. planifie la sauvegarde quotidienne vérifiée.
  Il n'écrase jamais une base existante.
#>
param(
    [string]$Dossier = "C:\SSI",
    [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
    [string]$PgSuperUtilisateur = "postgres",
    [int]$Port = 5443,
    [string]$NomServeur = $env:COMPUTERNAME,
    [string]$HeureSauvegarde = "23:30"
)
$ErrorActionPreference = "Stop"
function Etape($m) { Write-Host "`n=== $m" -ForegroundColor Cyan }
function Secret([int]$n) {
    $c = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $b = [byte[]]::new($n); [System.Security.Cryptography.RandomNumberGenerator]::Fill($b)
    -join ($b | ForEach-Object { $c[$_ % $c.Length] })
}

$psql = Join-Path $PgBin "psql.exe"
# Exécute une commande SQL en super-utilisateur ; arrêt immédiat si elle échoue (rien ne doit être tenu pour fait à tort).
function Sql([string]$base, [string]$commande) {
    & $psql -X -q -v ON_ERROR_STOP=1 -U $PgSuperUtilisateur -h 127.0.0.1 -d $base -c $commande | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Commande SQL en échec sur la base '$base' (code $LASTEXITCODE) : installation interrompue." }
}
if (-not (Test-Path $psql)) { throw "psql.exe introuvable dans $PgBin : installez PostgreSQL 16 ou indiquez -PgBin." }
$source = Join-Path $PSScriptRoot "publish\api"
if (-not (Test-Path (Join-Path $source "Ssi.Api.exe"))) { throw "Publication de l'API absente : $source (voir l'aide du script)." }

Etape "Base de données"
# Mot de passe demandé à l'écran, sauf s'il est déjà fourni par PGPASSWORD (installation automatisée).
if (-not $env:PGPASSWORD) { $env:PGPASSWORD = Read-Host "Mot de passe du super-utilisateur PostgreSQL '$PgSuperUtilisateur'" -AsSecureString |
    ForEach-Object { [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($_)) } }
$existe = & $psql -X -A -t -U $PgSuperUtilisateur -h 127.0.0.1 -d postgres -c "SELECT 1 FROM pg_database WHERE datname='ssi'"
if ($existe -eq "1") { throw "La base 'ssi' existe déjà : installation interrompue (aucune donnée n'a été modifiée)." }
$mdpApp = Secret 32
Sql postgres "CREATE ROLE ssi_app LOGIN PASSWORD '$mdpApp';"
Sql postgres "CREATE DATABASE ssi OWNER ssi_app ENCODING 'UTF8' TEMPLATE template0;"
Sql ssi "CREATE EXTENSION IF NOT EXISTS btree_gist;"

Etape "Fichiers de l'application"
foreach ($d in "Api", "Certificats", "Sauvegardes", "Logs", "Scripts") { New-Item -ItemType Directory -Force -Path (Join-Path $Dossier $d) | Out-Null }
Copy-Item -Recurse -Force "$source\*" (Join-Path $Dossier "Api")
Copy-Item -Force (Join-Path $PSScriptRoot "*.ps1"), (Join-Path $PSScriptRoot "controles.sql") (Join-Path $Dossier "Scripts")

Etape "Certificat HTTPS"
$ips = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike "169.*" -and $_.IPAddress -ne "127.0.0.1" }).IPAddress
$cert = New-SelfSignedCertificate -DnsName (@($NomServeur, "localhost") + $ips) -CertStoreLocation Cert:\LocalMachine\My `
    -FriendlyName "SSI API" -NotAfter (Get-Date).AddYears(5) -KeyExportPolicy Exportable
$mdpPfx = Secret 24
$pfx = Join-Path $Dossier "Certificats\ssi-serveur.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $mdpPfx -AsPlainText -Force) | Out-Null
Export-Certificate -Cert $cert -FilePath (Join-Path $Dossier "Certificats\ssi-serveur.cer") | Out-Null

Etape "Configuration"
$mdpAdmin = Secret 12
$config = [ordered]@{
    ConnectionStrings = @{ Ssi = "Host=127.0.0.1;Port=5432;Database=ssi;Username=ssi_app;Password=$mdpApp;Timeout=15;Command Timeout=60" }
    Jwt = @{ Cle = (Secret 64); Emetteur = "SSI"; DureeHeures = 8 }
    Ssi = @{ MotDePasseAdminInitial = $mdpAdmin; DossierLogs = (Join-Path $Dossier "Logs"); DossierSauvegardes = (Join-Path $Dossier "Sauvegardes") }
    Kestrel = @{ Endpoints = @{ Https = @{ Url = "https://0.0.0.0:$Port"; Certificate = @{ Path = $pfx; Password = $mdpPfx } } } }
}
$fichierConfig = Join-Path $Dossier "Api\appsettings.Production.json"
$config | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 $fichierConfig
# Le fichier contient des secrets : accès réservé aux administrateurs et au compte du service.
$acl = New-Object System.Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
foreach ($id in "BUILTIN\Administrators", "NT AUTHORITY\SYSTEM") {
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($id, "FullControl", "Allow")))
}
Set-Acl $fichierConfig $acl
Set-Acl $pfx $acl

Etape "Service Windows"
$exe = Join-Path $Dossier "Api\Ssi.Api.exe"
New-Service -Name "SSIApi" -DisplayName "SSI API" -BinaryPathName "`"$exe`"" -StartupType Automatic -Description "Station Service Intelligence — serveur d'application" | Out-Null
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\SSIApi" -Name Environment -Type MultiString -Value @("ASPNETCORE_ENVIRONMENT=Production")
sc.exe failure SSIApi reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
Start-Service SSIApi

Etape "Pare-feu (réseau local uniquement)"
New-NetFirewallRule -DisplayName "SSI API (réseau local)" -Direction Inbound -Protocol TCP -LocalPort $Port `
    -RemoteAddress LocalSubnet -Profile Domain, Private -Action Allow | Out-Null
if (Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object { $_.Enabled -eq "True" -and $_.Direction -eq "Inbound" } |
    Get-NetFirewallPortFilter | Where-Object { $_.LocalPort -eq 5432 }) {
    Write-Warning "Une règle de pare-feu ouvre le port PostgreSQL 5432 : supprimez-la, la base ne doit être accessible que depuis le serveur."
}

Etape "Vérification"
# Le certificat vient d'être créé et n'est pas encore approuvé sur ce poste : vérification sans contrôle de certificat
# (PowerShell 7 : -SkipCertificateCheck ; Windows PowerShell 5.1 : rappel de validation).
$appel = @{ UseBasicParsing = $true; TimeoutSec = 5; Uri = "https://localhost:$Port/health" }
if ($PSVersionTable.PSVersion.Major -ge 6) { $appel.SkipCertificateCheck = $true }
else { [Net.ServicePointManager]::ServerCertificateValidationCallback = { $true } }
$ok = $false; $derniereErreur = ""
for ($i = 0; $i -lt 30 -and -not $ok; $i++) {
    Start-Sleep -Seconds 2
    try { $ok = (Invoke-WebRequest @appel).StatusCode -eq 200 } catch { $derniereErreur = $_.Exception.Message }
}
if (-not $ok) { throw "Le service ne répond pas ($derniereErreur) : consultez $Dossier\Logs et l'observateur d'événements." }

Etape "Sauvegarde quotidienne vérifiée"
# Rôle dédié : lecture de toutes les données, création de la base temporaire de vérification, inscription du résultat.
$mdpSauvegarde = Secret 32
Sql ssi "CREATE ROLE ssi_sauvegarde LOGIN CREATEDB PASSWORD '$mdpSauvegarde' IN ROLE pg_read_all_data; GRANT INSERT ON sauvegardes TO ssi_sauvegarde;"
& (Join-Path $Dossier "Scripts\planifier-sauvegarde.ps1") -Dossier $Dossier -PgBin $PgBin -Heure $HeureSauvegarde -MotDePasse $mdpSauvegarde
Remove-Item Env:\PGPASSWORD

Write-Host "`nInstallation terminée." -ForegroundColor Green
Write-Host "Adresse des postes clients : https://${NomServeur}:$Port"
Write-Host "Certificat à installer sur chaque poste : $Dossier\Certificats\ssi-serveur.cer (deploy\installer-poste.ps1)"
Write-Host "Compte initial : admin / $mdpAdmin  (changement obligatoire à la première connexion ; notez-le maintenant, il n'est pas réaffiché)"
Write-Host "Avant la mise en service : renseignez la fiche société et VALIDEZ les taux de TVA (paramètres « À VALIDER »)."
