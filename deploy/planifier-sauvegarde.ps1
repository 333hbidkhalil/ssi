#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Planifie la sauvegarde quotidienne vérifiée (tâche « SSI - Sauvegarde », compte SYSTEM) et enregistre les identifiants
  du rôle ssi_sauvegarde dans le pgpass.conf du compte SYSTEM (accès réservé à SYSTEM et aux administrateurs).
#>
param(
    [string]$Dossier = "C:\SSI",
    [string]$PgBin = "C:\Program Files\PostgreSQL\16\bin",
    [string]$Heure = "23:30",
    [Parameter(Mandatory)][string]$MotDePasse,
    [string]$CopieExterne = ""
)
$ErrorActionPreference = "Stop"
$pgpassDossier = "C:\Windows\System32\config\systemprofile\AppData\Roaming\postgresql"
New-Item -ItemType Directory -Force -Path $pgpassDossier | Out-Null
$pgpass = Join-Path $pgpassDossier "pgpass.conf"
# Une ligne pour la base SSI, une pour la base « postgres » (création/suppression de la base temporaire de vérification)
"127.0.0.1:5432:*:ssi_sauvegarde:$MotDePasse" | Set-Content -Encoding ASCII $pgpass
$acl = New-Object System.Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
foreach ($id in "BUILTIN\Administrators", "NT AUTHORITY\SYSTEM") {
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
Register-ScheduledTask -TaskName "SSI - Sauvegarde" -Action $action -Trigger $declencheur -Settings $reglages -User "SYSTEM" -RunLevel Highest -Force | Out-Null
Write-Host "Sauvegarde vérifiée planifiée chaque jour à $Heure. Résultats : table « sauvegardes » (écran Administration) et journal Application."
Write-Host "Recommandation : ajoutez -CopieExterne vers un disque externe ou un autre poste ; une sauvegarde sur le seul serveur ne protège pas contre sa perte."
