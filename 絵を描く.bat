@echo off
chcp 65001 > nul
cd /d "%~dp0tools\draw"

REM ---------------------------------------------------------------
REM  Egg Command Battle - dot art editor (forked from pixelizer)
REM
REM  Serves tools\draw over http://localhost:5818 and opens it.
REM  A real HTTP origin is required: the File System Access API
REM  (folder picking / saving) does not work from file://.
REM
REM  NOTE: keep this file ASCII only. Japanese characters make
REM  cmd.exe fail silently (it reads the file in the console code page).
REM ---------------------------------------------------------------

set "PORT=5818"
set "URL=http://localhost:%PORT%/index.html"

netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 (
  echo Already serving on %PORT%. Opening...
  start "" "%URL%"
  exit /b 0
)

echo.
echo   Egg Command Battle - dot art editor
echo   Canvas: 270x480 ("9:16 screen") matches the game screen.
echo.

start "ECB Draw (port %PORT%)" /min cmd /c "python -m http.server %PORT% --bind 127.0.0.1"

set /a TRIES=0
:waitloop
timeout /t 1 /nobreak > nul 2>&1 || ping -n 2 127.0.0.1 > nul
netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 goto ready
set /a TRIES+=1
if %TRIES% geq 20 (
  echo.
  echo Could not start the server. Is python on PATH?
  pause
  exit /b 1
)
goto waitloop

:ready
start "" "%URL%"
echo.
echo Opened: %URL%
echo   * Close the "ECB Draw" window to stop the server.
echo   * Export: sonohoka -^> write to file -^> art\screens\NAME.pixelizer.json
echo.
