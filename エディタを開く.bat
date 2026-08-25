@echo off
chcp 65001 > nul
cd /d "%~dp0"

REM ---------------------------------------------------------------
REM  Egg Command Battle - layout (skeleton) editor.
REM  Opens http://localhost:5817/edit
REM
REM  * Use Chrome or Edge: picking a folder and saving needs the
REM    File System Access API (Firefox/Safari cannot save).
REM  * In the editor click the folder button and choose:  assets\layouts
REM ---------------------------------------------------------------

set "URL=http://localhost:5817/edit"
call "%~dp0.server.bat"
