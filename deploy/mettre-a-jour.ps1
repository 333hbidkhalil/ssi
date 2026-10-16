#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Met à jour SSI déjà installé (serveur et/ou poste) avec les fichiers de .\publish, sans toucher aux données ni aux réglages.
.DESCRIPTION
  1. lance la sauvegarde vérifiée (tâche « SSI - Sauvegarde ») et s'arrête si elle échoue (sauf -SansSauvegarde) ;
  2. serveur : arrête le service, conserve l'ancienne version (Api.precedent-…), copie la nouvelle en gardant
     appsettings.Production.json, redémarre ; la base est mise à niveau par le service au démarrage ;
  3. poste : remplace le client en gardant son appsettings.json.
  Retour arrière : arrêter le service, remettre le dossier Api.precedent-… à la place de Api, redémarrer, puis restaurer la sauvegarde.
#>
param([string]$Dossier = "C:\SSI", [string]$DossierClient = "C:\Program Files\SSI", [switch]$SansSauvegarde)
$ErrorActionPreference = "Stop"
function Etape($m) { Write-Host "`n### $m" -ForegroundColor Yellow }
$api = Join-Path $Dossier "Api"
$serveur = Test-Path (Join-Path $api "Ssi.Api.exe")
$poste = Test-Path (Join-Path $DossierClient "Ssi.Client.Wpf.exe")
if (-not $serveur -and -not $poste) { throw "SSI n'est pas installé sur ce poste ($api, $DossierClient) : utilisez le script d'installation." }

if ($serveur) {
    Etape "Sauvegarde vérifiée avant mise à jour"
    if ($SansSauvegarde) { Write-Warning "Mise à jour sans sauvegarde préalable (option -SansSauvegarde)." }
    else {
        $tache = Get-ScheduledTask -TaskName "SSI - Sauvegarde" -ErrorAction SilentlyContinue
        if (-not $tache) { throw "Tâche « SSI - Sauvegarde » introuvable : sauvegardez la base, puis relancez avec -SansSauvegarde." }
        Start-ScheduledTask -TaskName "SSI - Sauvegarde"
        do { Start-Sleep 5 } while ((Get-ScheduledTask -TaskName "SSI - Sauvegarde").State -eq "Running")
        $resultat = (Get-ScheduledTaskInfo -TaskName "SSI - Sauvegarde").LastTaskResult
        if ($resultat -ne 0) { throw "La sauvegarde a échoué (code $resultat) : mise à jour interrompue, rien n'a été modifié. Voir $Dossier\Sauvegardes\sauvegardes.log." }
        Write-Host "Sauvegarde vérifiée."
    }

    Etape "Serveur SSI"
    $source = Join-Path $PSScriptRoot "publish\api"
    if (-not (Test-Path (Join-Path $source "Ssi.Api.exe"))) { throw "Nouvelle version du serveur absente : $source" }
    $config = Get-Content -Raw (Join-Path $api "appsettings.Production.json")
    $port = if ($config -match "https://[^:]+:(\d+)") { $Matches[1] } else { "5443" }
    Stop-Service SSIApi
    (Get-Service SSIApi).WaitForStatus("Stopped", [TimeSpan]::FromSeconds(60))
    $precedent = "$api.precedent-$(Get-Date -Format yyyyMMdd-HHmmss)"
    Copy-Item -Recurse $api $precedent
    Get-ChildItem $source | Where-Object Name -ne "appsettings.Production.json" | Copy-Item -Destination $api -Recurse -Force
    Start-Service SSIApi
    if ($PSVersionTable.PSVersion.Major -lt 6) { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 }
    $ok = $false; $erreur = ""
    for ($i = 0; $i -lt 45 -and -not $ok; $i++) {
        Start-Sleep 2
        try { $ok = (Invoke-WebRequest -UseBasicParsing "https://localhost:$port/health" -TimeoutSec 5).StatusCode -eq 200 } catch { $erreur = $_.Exception.Message }
    }
    if (-not $ok) { throw "Le serveur ne répond pas après la mise à jour ($erreur). Ancienne version conservée dans $precedent ; journaux : $Dossier\Logs." }
    Write-Host "Serveur mis à jour et opérationnel (https://localhost:$port). Ancienne version : $precedent"
}

if ($poste) {
    Etape "Logiciel SSI (poste)"
    Get-Process Ssi.Client.Wpf -ErrorAction SilentlyContinue | ForEach-Object { throw "SSI est ouvert sur ce poste : fermez-le, puis relancez la mise à jour." }
    $source = Join-Path $PSScriptRoot "publish\client"
    if (-not (Test-Path (Join-Path $source "Ssi.Client.Wpf.exe"))) { throw "Nouvelle version du logiciel absente : $source" }
    Get-ChildItem $source | Where-Object Name -ne "appsettings.json" | Copy-Item -Destination $DossierClient -Recurse -Force
    Write-Host "Logiciel mis à jour."
}
Write-Host "`nMise à jour terminée." -ForegroundColor Green
