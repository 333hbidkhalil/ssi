#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Installation d'ESSAI de SSI sur un seul ordinateur (serveur, base et client sur le même poste), pour découvrir le logiciel.
.DESCRIPTION
  SSI s'installe à côté des logiciels déjà présents sans jamais les modifier :
  1. base PostgreSQL PROPRE À SSI (dossier C:\SSI\Base, service « SSI - PostgreSQL », accessible depuis ce seul ordinateur),
     sur le premier port libre à partir de 5432 ; les binaires PostgreSQL 16 sont téléchargés dans C:\SSI\PostgreSQL
     (ou pris dans -PgBin) ; un PostgreSQL déjà installé n'est ni utilisé ni modifié ;
  2. serveur SSI sur le premier port HTTPS libre à partir de 5443, puis client sur ce même poste ;
  3. identifiants dans « SSI - identifiants d'essai.txt » sur le bureau, puis ouverture de SSI.
  Pour tout retirer : desinstaller-essai.ps1. Pour une station en exploitation, suivez LISEZMOI.md.
#>
param([string]$PgBin = "", [switch]$SansLancement)
$ErrorActionPreference = "Stop"
$Dossier = "C:\SSI"
$ServicePg = "SSI-PostgreSQL"
function Etape($m) { Write-Host "`n### $m" -ForegroundColor Yellow }
function Secret([int]$n) {
    $c = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    $b = New-Object byte[] $n; [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    -join ($b | ForEach-Object { $c[$_ % $c.Length] })
}
function PortLibre([int]$depart) {
    $p = $depart
    while (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue) { $p++ }
    $p
}
# Les programmes PostgreSQL écrivent leurs messages sur la sortie d'erreur : seul le code de sortie compte (Windows PowerShell 5.1).
function Pg([string]$exe, [string[]]$arguments) {
    $ErrorActionPreference = "Continue"
    & (Join-Path $PgBin $exe) @arguments 2>&1 | ForEach-Object { "$_" } | Write-Host
    if ($LASTEXITCODE -ne 0) { throw "$exe a échoué (code $LASTEXITCODE)." }
}

Etape "Vérifications"
if (Get-Service SSIApi -ErrorAction SilentlyContinue) { throw "SSI est déjà installé sur ce poste. Pour repartir de zéro : desinstaller-essai.ps1." }
if ((Get-Service $ServicePg -ErrorAction SilentlyContinue) -or (Test-Path "$Dossier\Base")) {
    throw "Une base SSI existe déjà ($Dossier\Base). Pour repartir de zéro : desinstaller-essai.ps1 (rien n'a été modifié)."
}
$portBase = PortLibre 5432
$portApi = PortLibre 5443
if ($portBase -ne 5432) { Write-Host "Port 5432 déjà utilisé par un autre logiciel : la base SSI utilisera le port $portBase." }
if ($portApi -ne 5443) { Write-Host "Port 5443 déjà utilisé par un autre logiciel : le serveur SSI utilisera le port $portApi." }

Etape "PostgreSQL propre à SSI"
New-Item -ItemType Directory -Force -Path $Dossier | Out-Null
if (-not $PgBin) {
    $PgBin = Join-Path $Dossier "PostgreSQL\pgsql\bin"
    if (-not (Test-Path (Join-Path $PgBin "postgres.exe"))) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $archive = Join-Path $env:TEMP "ssi-postgresql-binaires.zip"
        $obtenu = $false
        foreach ($v in "16.10-1", "16.9-1", "16.8-1", "16.6-1", "16.4-1") {
            try {
                Write-Host "Téléchargement de PostgreSQL $v (environ 300 Mo)…"
                $ProgressPreference = "SilentlyContinue"
                Invoke-WebRequest -UseBasicParsing "https://get.enterprisedb.com/postgresql/postgresql-$v-windows-x64-binaries.zip" -OutFile $archive
                $obtenu = $true; break
            } catch { Write-Host "  version $v indisponible ($($_.Exception.Message))" }
        }
        if (-not $obtenu) { throw "Téléchargement de PostgreSQL impossible : vérifiez la connexion Internet, ou relancez avec -PgBin <dossier bin d'un PostgreSQL 16>." }
        Expand-Archive -Force $archive (Join-Path $Dossier "PostgreSQL")
        Remove-Item $archive
    }
}
foreach ($exe in "initdb.exe", "pg_ctl.exe", "postgres.exe", "psql.exe") {
    if (-not (Test-Path (Join-Path $PgBin $exe))) { throw "$exe introuvable dans $PgBin." }
}
$donnees = Join-Path $Dossier "Base"
$mdpPostgres = Secret 20
Pg initdb.exe @("-D", $donnees, "-U", "postgres", "-E", "UTF8", "-A", "trust")   # mot de passe fixé ci-dessous, puis accès par mot de passe seulement
# Base accessible depuis ce seul ordinateur, sur le port libre choisi.
Add-Content -Encoding ASCII (Join-Path $donnees "postgresql.conf") "`nport = $portBase`nlisten_addresses = 'localhost'"
# Service Windows dédié, sous le compte Service réseau (SID S-1-5-20 : nom indépendant de la langue de Windows).
icacls $donnees /grant "*S-1-5-20:(OI)(CI)M" /T /Q | Out-Null
Pg pg_ctl.exe @("register", "-N", $ServicePg, "-D", $donnees, "-S", "auto", "-U", "NT AUTHORITY\NetworkService", "-w")
Start-Service $ServicePg
$pret = $false
for ($i = 0; $i -lt 30 -and -not $pret; $i++) {
    Start-Sleep -Seconds 1
    & (Join-Path $PgBin "pg_isready.exe") -h 127.0.0.1 -p $portBase -q
    $pret = $LASTEXITCODE -eq 0
}
if (-not $pret) { throw "La base SSI ne démarre pas : voir $donnees\log et l'observateur d'événements." }
# Mot de passe du super-utilisateur, puis authentification obligatoire (connexions locales uniquement).
Pg psql.exe @("-X", "-q", "-v", "ON_ERROR_STOP=1", "-h", "127.0.0.1", "-p", "$portBase", "-U", "postgres", "-d", "postgres", "-c", "ALTER ROLE postgres PASSWORD '$mdpPostgres';")
"host all all 127.0.0.1/32 scram-sha-256`r`nhost all all ::1/128 scram-sha-256" | Set-Content -Encoding ASCII (Join-Path $donnees "pg_hba.conf")
Restart-Service $ServicePg
$env:PGPASSWORD = $mdpPostgres
$pret = $false
for ($i = 0; $i -lt 30 -and -not $pret; $i++) {
    Start-Sleep -Seconds 1
    $ErrorActionPreference = "Continue"   # message d'attente de psql sur la sortie d'erreur : seul le code compte
    & (Join-Path $PgBin "psql.exe") -X -A -t -h 127.0.0.1 -p $portBase -U postgres -d postgres -c "SELECT 1" 2>&1 | Out-Null
    $pret = $LASTEXITCODE -eq 0
    $ErrorActionPreference = "Stop"
}
if (-not $pret) { throw "La base SSI ne répond pas avec son mot de passe : voir l'observateur d'événements." }
Write-Host "Base SSI démarrée (service « $ServicePg », port $portBase, accès local uniquement, par mot de passe)."

Etape "Serveur SSI"
& (Join-Path $PSScriptRoot "installer-serveur.ps1") -Dossier $Dossier -PgBin $PgBin -PortBase $portBase -Port $portApi -NomServeur localhost *>&1 | Tee-Object -Variable journal
$journal | Out-File -Encoding UTF8 (Join-Path $env:TEMP "ssi-installation-essai.log")
$mdpAdmin = ($journal | Select-String "Compte initial : admin / (\S+)").Matches | Select-Object -First 1 | ForEach-Object { $_.Groups[1].Value }
if (-not $mdpAdmin) { throw "Mot de passe initial introuvable : voir $env:TEMP\ssi-installation-essai.log" }

Etape "Poste client (ce même ordinateur)"
& (Join-Path $PSScriptRoot "installer-poste.ps1") -Serveur localhost -Port $portApi -Certificat "$Dossier\Certificats\ssi-serveur.cer"

$fiche = Join-Path ([Environment]::GetFolderPath("Desktop")) "SSI - identifiants d'essai.txt"
@"
SSI — installation d'essai du $(Get-Date -Format 'dd/MM/yyyy HH:mm')

Ouvrir SSI : raccourci « SSI » sur le bureau.
Identifiant : admin
Mot de passe initial : $mdpAdmin
(un nouveau mot de passe de 10 caractères minimum est demandé à la première connexion)

Serveur SSI : https://localhost:$portApi
Base SSI : service « $ServicePg », port $portBase, dossier $donnees (séparée de tout autre logiciel)
Super-utilisateur de la base SSI : postgres / $mdpPostgres

Tout retirer : desinstaller-essai.ps1 (clic droit > Exécuter avec PowerShell, en administrateur).
Supprimez ce fichier après avoir noté ces informations en lieu sûr.
"@ | Set-Content -Encoding UTF8 $fiche
Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
Write-Host "`nInstallation d'essai terminée. Identifiants : $fiche" -ForegroundColor Green
if (-not $SansLancement) { Start-Process (Join-Path "C:\Program Files\SSI" "Ssi.Client.Wpf.exe") }
