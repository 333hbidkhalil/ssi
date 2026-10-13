#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Planifie la sauvegarde quotidienne vérifiée (tâche « SSI - Sauvegarde », compte SYSTEM) et enregistre les identifiants
  du rôle ssi_sauvegarde dans C:\SSI\Certificats\pgpass.conf (accès réservé à SYSTEM et aux administrateurs).
#>
param(
    [string]$Dossier = "C:\SSI",
    [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
    [string]$Heure = "23:30",
    [Parameter(Mandatory)][string]$MotDePasse,
    [string]$CopieExterne = ""
)
$ErrorActionPreference = "Stop"
# Fichier pgpass propre à SSI (et non celui du compte SYSTEM, que d'autres logiciels peuvent utiliser) ; tous ports,
# toutes bases : base SSI et base « postgres » (création/suppression de la base temporaire de vérification).
$pgpass = Join-Path $Dossier "Certificats\pgpass.conf"
"127.0.0.1:*:*:ssi_sauvegarde:$MotDePasse" | Set-Content -Encoding ASCII $pgpass
$acl = New-Object System.Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
# Comptes désignés par leur SID (Administrateurs, Système) : les noms changent avec la langue de Windows.
foreach ($sid in "S-1-5-32-544", "S-1-5-18") {
    $id = New-Object System.Security.Principal.SecurityIdentifier $sid
    $acl.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($id, "FullControl", "Allow")))
}
Set-Acl $pgpass $acl

if (-not [System.Diagnostics.EventLog]::SourceExists("SSI Sauvegarde")) { New-EventLog -LogName Application -Source "SSI Sauvegarde" }
$script = Join-Path $Dossier "Scripts\sauvegarder.ps1"
$arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -Dossier `"$Dossier`" -PgBin `"$PgBin`""
if ($CopieExterne) { $arguments += " -CopieExterne `"$CopieExterne`"" }
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument $arguments
$declencheur = New-ScheduledTaskTrigger -Daily -At $Heure
$reglages = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) -RestartCount 2 -RestartInterval (New-TimeSpan -Minutes 30)
Register-ScheduledTask -TaskName "SSI - Sauvegarde" -Action $action -Trigger $declencheur -Settings $reglages -User ((New-Object System.Security.Principal.SecurityIdentifier "S-1-5-18").Translate([System.Security.Principal.NTAccount]).Value) -RunLevel Highest -Force | Out-Null
Write-Host "Sauvegarde vérifiée planifiée chaque jour à $Heure. Résultats : table « sauvegardes » (écran Administration) et journal Application."
Write-Host "Recommandation : ajoutez -CopieExterne vers un disque externe ou un autre poste ; une sauvegarde sur le seul serveur ne protège pas contre sa perte."
