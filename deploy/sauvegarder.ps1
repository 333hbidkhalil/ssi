<#
.SYNOPSIS
  Sauvegarde SSI vérifiée (Windows). Même logique que sauvegarder.sh :
  dump dans un instantané exporté, totaux de contrôle de la source dans le même instantané, restauration complète dans une base
  temporaire, comparaison, empreinte SHA-256, inscription dans la table « sauvegardes », copie externe facultative, rotation.
  Le résultat n'est « VERIFIEE » que si la restauration et la comparaison réussissent ; sinon code de sortie non nul.
  Authentification : fichier pgpass.conf du compte qui exécute la tâche (créé par planifier-sauvegarde.ps1).
#>
param(
    [string]$Dossier = "C:\SSI",
    [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
    [string]$Base = "ssi",
    [string]$Utilisateur = "ssi_sauvegarde",
    [int]$Conservation = 30,
    [string]$CopieExterne = ""   # ex. un disque USB ou un partage : E:\SSI-Sauvegardes
)
$ErrorActionPreference = "Stop"
if (-not $env:PGHOST) { $env:PGHOST = "127.0.0.1" }
$env:PGUSER = $Utilisateur
$ext = if ($IsLinux -or $IsMacOS) { "" } else { ".exe" }   # PowerShell 7 sous Linux : utilisé pour les tests
$psql = Join-Path $PgBin "psql$ext"; $pgdump = Join-Path $PgBin "pg_dump$ext"; $pgrestore = Join-Path $PgBin "pg_restore$ext"
$sortie = if ($env:SSI_DOSSIER_SAUVEGARDES) { $env:SSI_DOSSIER_SAUVEGARDES } else { Join-Path $Dossier "Sauvegardes" }
$controles = Join-Path $PSScriptRoot "controles.sql"
$horodatage = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss")
$fichier = Join-Path $sortie "ssi-$Base-$horodatage.dump"
$verif = "ssi_verif_$horodatage".Replace("-", "_")
New-Item -ItemType Directory -Force -Path $sortie | Out-Null

function Journaliser([string]$statut, [string]$message) {
    $taille = 0; $sha = ""
    if (Test-Path $fichier) { $taille = (Get-Item $fichier).Length; $sha = (Get-FileHash -Algorithm SHA256 $fichier).Hash.ToLower() }
    $msg = $message.Replace("'", "''"); $f = $fichier.Replace("'", "''")
    & $psql -X -q -d $Base -c "INSERT INTO sauvegardes (horodatage, fichier, taille, sha256, statut, message) VALUES (now(), '$f', $taille, NULLIF('$sha',''), '$statut', '$msg');" | Out-Null
    try {
        Write-EventLog -LogName Application -Source "SSI Sauvegarde" -EventId ($(if ($statut -eq "VERIFIEE") { 1000 } else { 1001 })) `
            -EntryType ($(if ($statut -eq "VERIFIEE") { "Information" } else { "Error" })) -Message "$statut : $message"
    } catch { }   # journal Windows indisponible : le résultat reste dans la table sauvegardes
}
function Echec([string]$message, [int]$code) { Journaliser "ECHEC" $message; Write-Error $message -ErrorAction Continue; exit $code }

# Session psql qui garde l'instantané ouvert
$psi = New-Object System.Diagnostics.ProcessStartInfo $psql, "-X -q -A -t -d $Base"
$psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true; $psi.UseShellExecute = $false
$psi.StandardOutputEncoding = [Text.Encoding]::UTF8
$session = [System.Diagnostics.Process]::Start($psi)
try {
    $session.StandardInput.WriteLine("BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY; SELECT pg_export_snapshot();")
    $snapshot = $session.StandardOutput.ReadLine()
    if (-not $snapshot) { Echec "Impossible d'ouvrir un instantané." 2 }

    & $pgdump -Fc "--snapshot=$snapshot" -d $Base -f $fichier
    if ($LASTEXITCODE -ne 0) { Echec "pg_dump a échoué (code $LASTEXITCODE)." 3 }

    $fin = "__fin_controles__"
    $session.StandardInput.WriteLine((Get-Content -Raw -Encoding UTF8 $controles))
    $session.StandardInput.WriteLine("SELECT '$fin';")
    $source = New-Object System.Collections.Generic.List[string]
    while (($l = $session.StandardOutput.ReadLine()) -ne $null -and $l -ne $fin) { if ($l) { $source.Add($l) } }
    $session.StandardInput.WriteLine("COMMIT;"); $session.StandardInput.WriteLine("\q")
} finally { if (-not $session.HasExited) { $session.WaitForExit(10000) | Out-Null } }

try {
    & $psql -X -q -d postgres -c "CREATE DATABASE $verif;" | Out-Null
    & $pgrestore --no-owner --exit-on-error -d $verif $fichier 2> (Join-Path $sortie ".restauration.log")
    if ($LASTEXITCODE -ne 0) { Echec "Restauration de vérification impossible : $(Get-Content -Raw (Join-Path $sortie '.restauration.log'))" 4 }
    $restauree = @(& $psql -X -q -A -t -d $verif -f $controles | Where-Object { $_ })
    $ecarts = Compare-Object $source $restauree
    if ($ecarts) { Echec "Totaux de contrôle différents après restauration : $($ecarts | Out-String)" 5 }
} finally {
    & $psql -X -q -d postgres -c "DROP DATABASE IF EXISTS $verif WITH (FORCE);" | Out-Null
}

$source -join "`n" | Set-Content -NoNewline -Encoding UTF8 "$fichier.controles.txt"
(Get-FileHash -Algorithm SHA256 $fichier).Hash.ToLower() | Set-Content -NoNewline "$fichier.sha256"
if ($CopieExterne) {
    try { Copy-Item "$fichier*" $CopieExterne -Force }
    catch { Journaliser "VERIFIEE_SANS_COPIE" "Sauvegarde vérifiée mais copie externe impossible vers $CopieExterne : $_"; exit 6 }
}
Journaliser "VERIFIEE" "Restauration de contrôle réussie ; $($source.Count) totaux identiques$(if ($CopieExterne) { " ; copie vers $CopieExterne" })"
Get-ChildItem $sortie -Filter "ssi-$Base-*.dump*" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$Conservation) } | Remove-Item -Force
Write-Host "Sauvegarde vérifiée : $fichier"
