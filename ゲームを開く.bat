@echo off
chcp 65001 > nul
cd /d "%~dp0"

REM ---------------------------------------------------------------
REM  Egg Command Battle - play the game.
REM  Opens http://localhost:5817/app (the Blazor WebAssembly build).
REM
REM  NOTE: "/" is the encyclopedia, not the game. The game is "/app".
REM  NOTE: the old TypeScript/Vite version (port 5815) now lives in
REM        old/ts and is NOT what this opens.
REM ---------------------------------------------------------------

set "URL=http://localhost:5817/app"
call "%~dp0.server.bat"
