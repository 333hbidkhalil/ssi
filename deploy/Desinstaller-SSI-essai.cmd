@echo off
rem Retire l'installation d'essai de SSI de ce PC (demande confirmation) : droits administrateur puis desinstaller-essai.ps1.
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0desinstaller-essai.ps1"
echo.
pause
