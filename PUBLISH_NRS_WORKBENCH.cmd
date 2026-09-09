@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo ==========================================================
echo   NRS Workbench - First public push
echo ==========================================================
echo.

where git >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Git no esta disponible en PATH.
  pause
  exit /b 1
)

if not exist "NRSWorkbench.sln" (
  echo [ERROR] Ejecuta este archivo dentro de la carpeta descomprimida de NRS Workbench v0.15.1.
  pause
  exit /b 1
)

if not exist "LICENSE" (
  echo [ERROR] Falta LICENSE. No se publicara nada.
  pause
  exit /b 1
)

echo [1/6] Inicializando repositorio local...
if not exist ".git" (
  git init -b main
  if errorlevel 1 goto :fail
)

echo [2/6] Configurando origin...
git remote get-url origin >nul 2>nul
if errorlevel 1 (
  git remote add origin https://github.com/Nfreakz/nrs-workbench.git
) else (
  git remote set-url origin https://github.com/Nfreakz/nrs-workbench.git
)
if errorlevel 1 goto :fail

echo [3/6] Sincronizando el commit inicial remoto...
git fetch origin main
if errorlevel 1 goto :fail

git branch -M main
if errorlevel 1 goto :fail

git reset --mixed origin/main
if errorlevel 1 goto :fail

echo [4/6] Preparando archivos auditados...
git add -A
if errorlevel 1 goto :fail

echo.
echo Archivos que se publicaran:
git status --short

echo.
echo [5/6] Creando commit publico...
git diff --cached --quiet
if not errorlevel 1 (
  echo [INFO] No hay cambios nuevos que commitear.
) else (
  git commit -m "feat: publish NRS Workbench v0.15.1 public preview"
  if errorlevel 1 goto :fail
)

echo [6/6] Publicando en GitHub...
git push -u origin main
if errorlevel 1 goto :fail

echo.
echo ==========================================================
echo   PUBLICACION COMPLETADA
 echo   https://github.com/Nfreakz/nrs-workbench
 echo ==========================================================
pause
exit /b 0

:fail
echo.
echo [ERROR] La publicacion se ha detenido. No ejecutes comandos adicionales.
echo Copia esta ventana y pasamela para revisarlo.
pause
exit /b 1
