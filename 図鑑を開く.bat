@echo off
chcp 65001 > nul
rem 図鑑を作り直してから開く。
rem ⭐ 毎回作り直すので、表を書き換えた直後でも中身が古くならない。
cd /d "%~dp0game"
"C:\Program Files\dotnet\dotnet.exe" run --project EggCommand.Sim -- book
if errorlevel 1 (
  echo.
  echo 書き出しに失敗しました。中身の検査が落ちている可能性があります。
  pause
  exit /b 1
)
start "" "%~dp0図鑑.html"
