@echo off
REM ---------------------------------------------------------------
REM  Shared server starter. Called by the launchers with %URL% set.
REM
REM  Starts the Blazor dev server (dotnet, port 5817) in its own
REM  window and opens %URL%. If a server is already listening on
REM  5817 it is reused.
REM
REM  NOTE: keep this file ASCII only. Japanese characters make
REM  cmd.exe fail silently (it reads the file in the console code page).
REM ---------------------------------------------------------------

set "PORT=5817"
set "PROJ=game\EggCommand.Web\EggCommand.Web.csproj"

netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 (
  echo Server already running on %PORT%. Opening...
  start "" "%URL%"
  exit /b 0
)

echo.
echo   Egg Command Battle
echo   Starting the server (the first build takes a few seconds)...
echo.

start "ECB Server (port %PORT%)" /min cmd /c "dotnet run --project %PROJ%"

set /a TRIES=0
:waitloop
timeout /t 1 /nobreak > nul 2>&1 || ping -n 2 127.0.0.1 > nul
netstat -ano | findstr /c:":%PORT% " | findstr "LISTENING" > nul
if not errorlevel 1 goto ready
set /a TRIES+=1
if %TRIES% geq 90 (
  echo.
  echo Server did not come up within 90 seconds.
  echo Check the "ECB Server" window for build errors.
  pause
  exit /b 1
)
goto waitloop

:ready
start "" "%URL%"
echo.
echo Opened: %URL%
echo.
echo   * Close the "ECB Server" window to stop the server.
echo   * After a rebuild you MUST restart the server, or the browser
echo     gets 404 + SRI failures and the page goes blank.
echo.
