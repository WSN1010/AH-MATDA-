@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo .NET SDK was not found. Install the .NET 10 SDK first.
    pause
    exit /b 1
)

where npm >nul 2>&1
if errorlevel 1 (
    echo npm was not found. Install Node.js first.
    pause
    exit /b 1
)

if not exist "src\Ajure.Web\node_modules" (
    echo Installing web dependencies...
    call npm ci --prefix "src\Ajure.Web"
    if errorlevel 1 (
        echo Failed to install web dependencies.
        pause
        exit /b 1
    )
)

if not exist "src\Ajure.AppHost\.data" mkdir "src\Ajure.AppHost\.data"

set "AJURE_DATA_PATH=%CD%\src\Ajure.AppHost\.data\ajure.db"
set "ASPNETCORE_ENVIRONMENT=Development"
set "DOTNET_ENVIRONMENT=Development"
if not defined AJURE_FAKE_MODEL set "AJURE_FAKE_MODEL=false"
set "API_HTTP=http://127.0.0.1:5037"
set "VITE_AJURE_API_MODE=live"

echo Starting Ajure API, Worker, and Web without Aspire...
echo Data: %AJURE_DATA_PATH%
echo Fake model: %AJURE_FAKE_MODEL%

start "Ajure API" /D "%CD%" cmd /k "dotnet run --project src\Ajure.Api --no-launch-profile --urls http://127.0.0.1:5037"
start "Ajure Worker" /D "%CD%" cmd /k "dotnet run --project src\Ajure.Worker --no-launch-profile"
start "Ajure Web" /D "%CD%" cmd /k "npm run dev --prefix src\Ajure.Web -- --host 127.0.0.1"

ping 127.0.0.1 -n 4 >nul
start "" "http://127.0.0.1:5173"
exit /b 0
