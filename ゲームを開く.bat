@echo off
REM ---------------------------------------------------------------
REM  Egg Command Battle - launcher
REM
REM  Starts the dev server, opens the browser, and shuts the server
REM  down again as soon as the browser tab is closed.
REM
REM  NOTE: keep this file ASCII only. Japanese characters make cmd.exe
REM  fail silently (it reads the file in the console code page).
REM ---------------------------------------------------------------

chcp 65001 > nul
cd /d "%~dp0"

REM Picked up by vite-plugins/live.ts. Without it the server stays up.
set ECB_AUTOCLOSE=1

if not exist "node_modules" (
  echo First run - installing dependencies...
  call npm install
  if errorlevel 1 (
    echo.
    echo npm install failed.
    pause
    exit /b 1
  )
)

echo.
echo   Egg Command Battle
echo   Close the browser tab to stop the server.
echo.

call npm run dev -- --open
