@echo off
setlocal
pushd "%~dp0"

echo ======================================
echo  Dad Planner 2 - Pre-Flight Check
echo ======================================

rem 1. Check for .NET 10 SDK specifically
dotnet --version 2>nul | findstr /R "^10\." >nul
IF %ERRORLEVEL% NEQ 0 (
    echo [X] .NET 10 SDK is missing or not the active version.
    echo Please install it from https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b
)

set PROJECT_FILE=DadPlanner2.csproj

IF NOT EXIST "%PROJECT_FILE%" (
    echo [X] Could not find %PROJECT_FILE%. Are you in the right directory?
    pause
    exit /b
)

rem 2. Check if project is fully populated with strict versions
findstr /I "CommunityToolkit.Mvvm" "%PROJECT_FILE%" >nul
IF %ERRORLEVEL% NEQ 0 (
    echo [!] Missing required strict-version NuGet dependencies.
    set /p INSTALL="Would you like to install them now? (y/n): "
    
    IF /I "%INSTALL%"=="y" (
        echo Locking in Avalonia UI v11.0.11...
        dotnet add "%PROJECT_FILE%" package Avalonia --version 11.0.11
        dotnet add "%PROJECT_FILE%" package Avalonia.Controls.DataGrid --version 11.0.11
        dotnet add "%PROJECT_FILE%" package Avalonia.Desktop --version 11.0.11
        dotnet add "%PROJECT_FILE%" package Avalonia.Themes.Fluent --version 11.0.11
        dotnet add "%PROJECT_FILE%" package Avalonia.Fonts.Inter --version 11.0.11
        dotnet add "%PROJECT_FILE%" package Avalonia.Diagnostics --version 11.0.11
        
        echo Locking in MVVM Toolkit v8.4.2...
        dotnet add "%PROJECT_FILE%" package CommunityToolkit.Mvvm --version 8.4.2
        
        echo Locking in LiveCharts Engine v2.0.0-rc3...
        dotnet add "%PROJECT_FILE%" package LiveChartsCore.SkiaSharpView.Avalonia --version 2.0.0-rc3
        
        echo Locking in SQLite and QuestPDF...
        dotnet add "%PROJECT_FILE%" package Microsoft.Data.Sqlite --version 10.0.11
        dotnet add "%PROJECT_FILE%" package QuestPDF --version 2026.8.0
        
        echo [OK] Dependencies successfully locked and added.
    ) ELSE (
        echo Cannot run without dependencies. Exiting.
        pause
        exit /b
    )
) ELSE (
    echo Verifying local dependency cache...
    dotnet restore "%PROJECT_FILE%"
)

echo [OK] All dependencies verified.
echo Launching Dad Planner 2...
dotnet run --project "%PROJECT_FILE%" --no-restore

popd
endlocal