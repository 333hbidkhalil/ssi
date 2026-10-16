@echo off
rem Mise a jour de SSI sur ce PC : droits administrateur puis mettre-a-jour.ps1.
net session >nul 2>&1
if %errorlevel% neq 0 (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0mettre-a-jour.ps1"
echo.
pause
