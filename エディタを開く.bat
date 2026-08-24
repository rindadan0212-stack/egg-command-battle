@echo off
chcp 65001 > nul
cd /d "%~dp0"

REM ---------------------------------------------------------------
REM  Egg Command Battle - Layout (skeleton) editor launcher
REM
REM  Starts the Blazor dev server (dotnet, port 5817) in its own
REM  window and opens the editor at /edit. If a server is already
REM  running on 5817 it is reused.
REM
REM  NOTE: keep this file ASCII only. Japanese characters make
REM  cmd.exe fail silently (it reads the file in the console code page).
REM ---------------------------------------------------------------

set "PORT=5817"
set "URL=http://localhost:%PORT%/edit"
set "PROJ=unity-port\EggCommand.Web\EggCommand.Web.csproj"

REM --- Reuse a server that is already listening on this port. ---
netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 (
  echo Server already running on %PORT%. Opening the editor...
  start "" "%URL%"
  exit /b 0
)

echo.
echo   Egg Command Battle - Layout editor
echo   Starting the dev server (the first build takes a few seconds)...
echo.

REM --- Start dotnet in its own minimized window so it keeps running. ---
start "ECB Editor Server (port %PORT%)" /min cmd /c "dotnet run --project %PROJ% --urls http://localhost:%PORT%"

REM --- Wait until the port is listening (max ~90s), then open the browser. ---
set /a TRIES=0
:waitloop
timeout /t 1 /nobreak > nul 2>&1 || ping -n 2 127.0.0.1 > nul
netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 goto ready
set /a TRIES+=1
if %TRIES% geq 90 (
  echo.
  echo Server did not come up within 90 seconds.
  echo Check the "ECB Editor Server" window for build errors.
  pause
  exit /b 1
)
goto waitloop

:ready
start "" "%URL%"
echo.
echo Editor opened: %URL%
echo.
echo   * Use Chrome or Edge: picking a folder and saving needs the
echo     File System Access API (Firefox/Safari cannot save).
echo   * In the editor click the folder button and choose:
echo       unity\Assets\Resources\Layouts
echo   * Close the "ECB Editor Server" window to stop the server.
echo.
