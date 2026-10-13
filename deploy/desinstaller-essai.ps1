#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Retire l'installation d'ESSAI de SSI de ce poste (installée par installer-essai.ps1), sans toucher aux autres logiciels.
.DESCRIPTION
  Ne retire que des éléments au nom de SSI : services « SSIApi » et « SSI-PostgreSQL », tâche « SSI - Sauvegarde »,
  règle de pare-feu « SSI API (réseau local) », certificat du serveur SSI, client (C:\Program Files\SSI), raccourci,
  et le dossier C:\SSI, qui contient la base et les sauvegardes de l'essai : celles-ci sont DÉFINITIVEMENT effacées,
  d'où la confirmation demandée (sauf -Confirme).
#>
param([switch]$Confirme)
$ErrorActionPreference = "Stop"
$Dossier = "C:\SSI"
if (-not $Confirme) {
    Write-Host "Toutes les données de l'essai SSI (base et sauvegardes dans $Dossier) seront effacées définitivement." -ForegroundColor Yellow
    if ((Read-Host "Tapez SUPPRIMER pour confirmer") -cne "SUPPRIMER") { Write-Host "Abandon : rien n'a été modifié."; return }
}
foreach ($nom in "SSIApi", "SSI-PostgreSQL") {
    if (Get-Service $nom -ErrorAction SilentlyContinue) {
        Stop-Service $nom -Force -ErrorAction SilentlyContinue
        sc.exe delete $nom | Out-Null
        Write-Host "Service $nom retiré."
    }
}
Unregister-ScheduledTask -TaskName "SSI - Sauvegarde" -Confirm:$false -ErrorAction SilentlyContinue
Get-NetFirewallRule -DisplayName "SSI API (réseau local)" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
$cer = Join-Path $Dossier "Certificats\ssi-serveur.cer"
if (Test-Path $cer) {
    $empreinte = (New-Object Security.Cryptography.X509Certificates.X509Certificate2 $cer).Thumbprint
    foreach ($magasin in "Cert:\LocalMachine\Root", "Cert:\LocalMachine\My") {
        Get-ChildItem $magasin | Where-Object Thumbprint -eq $empreinte | Remove-Item
    }
    Write-Host "Certificat du serveur SSI retiré."
}
Remove-Item -Force "$env:PUBLIC\Desktop\SSI.lnk" -ErrorAction SilentlyContinue
foreach ($d in "C:\Program Files\SSI", $Dossier) {
    if (Test-Path $d) { Start-Sleep -Seconds 2; Remove-Item -Recurse -Force $d; Write-Host "Dossier $d supprimé." }
}
Write-Host "`nSSI (essai) a été retiré de ce poste. Les autres logiciels n'ont pas été modifiés." -ForegroundColor Green
