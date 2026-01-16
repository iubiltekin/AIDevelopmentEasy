# AIDevelopmentEasy - Build & Package Script
# Creates a self-contained Windows Service with embedded React UI

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\publish",
    [switch]$SkipReactBuild
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  AIDevelopmentEasy Build Script" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build React UI
if (-not $SkipReactBuild) {
    Write-Host "[1/6] Building React UI..." -ForegroundColor Yellow
    Push-Location "src\AIDevelopmentEasy.Web"
    
    if (-not (Test-Path "node_modules")) {
        Write-Host "      Installing npm dependencies..."
        npm install
    }
    
    Write-Host "      Building production bundle..."
    npm run build
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] React build failed!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Pop-Location
    Write-Host "[OK] React UI built successfully!" -ForegroundColor Green
} else {
    Write-Host "[1/6] Skipping React build (-SkipReactBuild)" -ForegroundColor Gray
}

# Step 2: Build .NET API
Write-Host ""
Write-Host "[2/6] Building .NET API..." -ForegroundColor Yellow

$publishPath = Join-Path $OutputDir "AIDevelopmentEasy"

dotnet publish src\AIDevelopmentEasy.Api\AIDevelopmentEasy.Api.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -o $publishPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] .NET build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] .NET API built successfully!" -ForegroundColor Green

# Step 3: Copy React build to wwwroot
Write-Host ""
Write-Host "[3/6] Copying React UI to wwwroot..." -ForegroundColor Yellow

$reactDist = "src\AIDevelopmentEasy.Web\dist"
$wwwroot = Join-Path $publishPath "wwwroot"

if (Test-Path $reactDist) {
    if (Test-Path $wwwroot) {
        Remove-Item $wwwroot -Recurse -Force
    }
    Copy-Item $reactDist $wwwroot -Recurse
    Write-Host "[OK] React UI copied to wwwroot!" -ForegroundColor Green
} else {
    Write-Host "[WARN] React dist folder not found. Run without -SkipReactBuild" -ForegroundColor Yellow
}

# Step 4: Copy prompts to ProgramData (single source of truth)
Write-Host ""
Write-Host "[4/6] Copying prompts to ProgramData..." -ForegroundColor Yellow
$promptsSrc = "prompts"
if (Test-Path $promptsSrc) {
    # Copy directly to ProgramData - this is where the app reads from
    $programDataDir = "$env:ProgramData\AIDevelopmentEasy"
    $programDataPrompts = "$programDataDir\prompts"
    
    if (-not (Test-Path $programDataDir)) {
        New-Item -ItemType Directory -Path $programDataDir -Force | Out-Null
    }
    if (Test-Path $programDataPrompts) {
        Remove-Item $programDataPrompts -Recurse -Force
    }
    Copy-Item $promptsSrc $programDataPrompts -Recurse -Force
    
    Write-Host "[OK] Prompts copied to: $programDataPrompts" -ForegroundColor Green
} else {
    Write-Host "[WARN] Prompts folder not found at: $promptsSrc" -ForegroundColor Yellow
}

# Step 5: Create install/uninstall scripts
Write-Host ""
Write-Host "[5/6] Creating service management scripts..." -ForegroundColor Yellow

# Install script
@"
@echo off
:: Run as Administrator
echo ================================================================
echo   AIDevelopmentEasy - Windows Service Installer
echo ================================================================
echo.

:: Check admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Please run as Administrator!
    pause
    exit /b 1
)

set SERVICE_NAME=AIDevelopmentEasy
set SERVICE_PATH=%~dp0AIDevelopmentEasy.Api.exe
set SERVICE_DESC=AI-powered software development automation service

:: Create data directories
set DATA_DIR=%ProgramData%\AIDevelopmentEasy
mkdir "%DATA_DIR%\requirements" 2>nul
mkdir "%DATA_DIR%\output" 2>nul
mkdir "%DATA_DIR%\prompts" 2>nul
mkdir "%DATA_DIR%\codebases" 2>nul
mkdir "%DATA_DIR%\logs" 2>nul

:: Note: Prompts are managed via build-service.ps1 directly to ProgramData
:: If prompts folder exists in install dir (legacy), copy it
if exist "%~dp0prompts" (
    xcopy /s /y /i "%~dp0prompts" "%DATA_DIR%\prompts\"
) else (
    echo Note: Prompts should be copied via build-service.ps1
)

:: Copy appsettings if they exist
if exist "%~dp0appsettings.json" (
    copy /y "%~dp0appsettings.json" "%DATA_DIR%\appsettings.json"
)
if exist "%~dp0appsettings.Local.json" (
    copy /y "%~dp0appsettings.Local.json" "%DATA_DIR%\appsettings.Local.json"
)

:: Create the service
echo Creating service: %SERVICE_NAME%
sc create %SERVICE_NAME% binPath= "\"%SERVICE_PATH%\" --urls http://localhost:5000" start= auto DisplayName= "AIDevelopmentEasy"
sc description %SERVICE_NAME% "%SERVICE_DESC%"

:: Configure recovery options (restart on failure)
sc failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/60000/restart/60000

echo.
echo ================================================================
echo   Installation Complete!
echo ================================================================
echo.
echo   Service Name: %SERVICE_NAME%
echo   Data Directory: %DATA_DIR%
echo   URL: http://localhost:5000
echo.
echo   IMPORTANT: Make sure appsettings.Local.json has your API keys!
echo.
echo   Commands:
echo     Start:   net start %SERVICE_NAME%
echo     Stop:    net stop %SERVICE_NAME%
echo     Status:  sc query %SERVICE_NAME%
echo.
pause
"@ | Out-File (Join-Path $publishPath "install-service.cmd") -Encoding ASCII

# Uninstall script
@"
@echo off
:: Run as Administrator
echo ================================================================
echo   AIDevelopmentEasy - Windows Service Uninstaller
echo ================================================================
echo.

:: Check admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Please run as Administrator!
    pause
    exit /b 1
)

set SERVICE_NAME=AIDevelopmentEasy

:: Stop the service if running
echo Stopping service...
net stop %SERVICE_NAME% 2>nul

:: Delete the service
echo Removing service...
sc delete %SERVICE_NAME%

echo.
echo ================================================================
echo   Service Removed!
echo ================================================================
echo.
echo   Note: Data directory NOT removed: %ProgramData%\AIDevelopmentEasy
echo   Delete manually if no longer needed.
echo.
pause
"@ | Out-File (Join-Path $publishPath "uninstall-service.cmd") -Encoding ASCII

Write-Host "[OK] Service scripts created!" -ForegroundColor Green

# Step 6: Summary
Write-Host ""
Write-Host "[6/6] Build Summary" -ForegroundColor Yellow
Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Output: $publishPath" -ForegroundColor White
Write-Host ""
Write-Host "  Files:" -ForegroundColor White
Write-Host "    - AIDevelopmentEasy.Api.exe  (Main executable)" -ForegroundColor Gray
Write-Host "    - wwwroot/                   (React UI)" -ForegroundColor Gray
Write-Host "    - appsettings.json           (Config template)" -ForegroundColor Gray
Write-Host "    - install-service.cmd        (Install as service)" -ForegroundColor Gray
Write-Host "    - uninstall-service.cmd      (Remove service)" -ForegroundColor Gray
Write-Host ""
Write-Host "  Prompts: $env:ProgramData\AIDevelopmentEasy\prompts" -ForegroundColor White
Write-Host ""
Write-Host "  Next Steps:" -ForegroundColor Yellow
Write-Host "    1. Copy appsettings.Local.json with your API keys" -ForegroundColor White
Write-Host "    2. Run install-service.cmd as Administrator" -ForegroundColor White
Write-Host "    3. Open http://localhost:5000" -ForegroundColor White
Write-Host ""
