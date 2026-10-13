#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Prépare un poste client SSI : approuve le certificat du serveur, copie le client WPF et règle l'adresse du serveur.
.EXAMPLE
  .\installer-poste.ps1 -Serveur SERVEUR-SSI -Certificat \\SERVEUR-SSI\SSI$\Certificats\ssi-serveur.cer
#>
param(
    [Parameter(Mandatory)][string]$Serveur,
    [Parameter(Mandatory)][string]$Certificat,
    [int]$Port = 5443,
    [string]$Dossier = "C:\Program Files\SSI"
)
$ErrorActionPreference = "Stop"
Import-Certificate -FilePath $Certificat -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
$source = Join-Path $PSScriptRoot "publish\client"
if (-not (Test-Path (Join-Path $source "Ssi.Client.Wpf.exe"))) { throw "Publication du client absente : $source" }
New-Item -ItemType Directory -Force -Path $Dossier | Out-Null
Copy-Item -Recurse -Force "$source\*" $Dossier
@{ Serveur = @{ Adresse = "https://${Serveur}:$Port" } } | ConvertTo-Json -Depth 3 | Set-Content -Encoding UTF8 (Join-Path $Dossier "appsettings.json")
$raccourci = (New-Object -ComObject WScript.Shell).CreateShortcut("$env:PUBLIC\Desktop\SSI.lnk")
$raccourci.TargetPath = Join-Path $Dossier "Ssi.Client.Wpf.exe"
$raccourci.Save()
try { Invoke-WebRequest -UseBasicParsing "https://${Serveur}:$Port/health" -TimeoutSec 5 | Out-Null; Write-Host "Serveur joignable." -ForegroundColor Green }
catch { Write-Warning "Serveur injoignable depuis ce poste : vérifiez le réseau et le pare-feu du serveur." }
