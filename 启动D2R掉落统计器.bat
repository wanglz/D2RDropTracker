@echo off
set "APP=%~dp0D2RDropTracker\bin\Release\net8.0-windows\D2RDropTracker.exe"
if not exist "%APP%" (
  echo Application not found. Run build.ps1 first.
  pause
  exit /b 1
)
start "D2R Drop Tracker" "%APP%"
