@echo off
setlocal
cd /d "%~dp0"
echo ==========================================================
echo   NRS Workbench - Public Readiness

echo ==========================================================
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\public-readiness.ps1"
if errorlevel 1 (
  echo.
  echo [ERROR] Public readiness checks failed.
  pause
  exit /b 1
)
echo.
echo All public-readiness checks passed.
pause
