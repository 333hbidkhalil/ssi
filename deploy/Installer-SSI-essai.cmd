@echo off
rem Installation d'essai de SSI sur ce PC : demande les droits administrateur puis lance installer-essai.ps1.
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0installer-essai.ps1"
echo.
pause
