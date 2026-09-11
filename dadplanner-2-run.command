#!/usr/bin/env bash

set -e

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "======================================"
echo " Dad Planner 2 - macOS Pre-Flight Check"
echo "======================================"

# 1. Check for .NET 10 SDK specifically
if ! command -v dotnet &> /dev/null || ! dotnet --version | grep -q "^10\."; then
    echo "❌ .NET 10 SDK is missing or not the active version."
    echo "Please install it from https://dotnet.microsoft.com/download/dotnet/10.0"
    echo "Press any key to exit..."
    read -n 1 -s
    exit 1
fi

PROJECT_FILE="DadPlanner2.csproj"
if [ ! -f "$PROJECT_FILE" ]; then
    echo "❌ Could not find $PROJECT_FILE. Are you in the right directory?"
    echo "Press any key to exit..."
    read -n 1 -s
    exit 1
fi

# 2. Check if packages need to be restored or injected
# We check for a core package to see if the csproj is fully populated
if ! grep -qi "Include=\"CommunityToolkit.Mvvm\"" "$PROJECT_FILE"; then
    echo "⚠️  Missing required strict-version NuGet packages."
    read -p "Would you like to install them now? (y/n) " -n 1 -r
    echo
    
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Locking in dependencies..."
        dotnet add "$PROJECT_FILE" package Avalonia --version 11.0.11
        dotnet add "$PROJECT_FILE" package Avalonia.Controls.DataGrid --version 11.0.11
        dotnet add "$PROJECT_FILE" package Avalonia.Desktop --version 11.0.11
        dotnet add "$PROJECT_FILE" package Avalonia.Themes.Fluent --version 11.0.11
        dotnet add "$PROJECT_FILE" package Avalonia.Fonts.Inter --version 11.0.11
        dotnet add "$PROJECT_FILE" package Avalonia.Diagnostics --version 11.0.11
        dotnet add "$PROJECT_FILE" package CommunityToolkit.Mvvm --version 8.4.2
        dotnet add "$PROJECT_FILE" package LiveChartsCore.SkiaSharpView.Avalonia --version 2.0.0-rc3
        dotnet add "$PROJECT_FILE" package Microsoft.Data.Sqlite --version 10.0.11
        dotnet add "$PROJECT_FILE" package QuestPDF --version 2026.8.0
        
        # macOS specific native asset for Skia rendering
        dotnet add "$PROJECT_FILE" package SkiaSharp.NativeAssets.macOS
        
        echo "✅ Packages successfully locked and added."
    else
        echo "Cannot run without dependencies. Exiting."
        echo "Press any key to exit..."
        read -n 1 -s
        exit 1
    fi
else
    # If the csproj is populated, run a clean restore just to be safe
    echo "Verifying local dependency cache..."
    dotnet restore "$PROJECT_FILE"
fi

echo "✅ All dependencies verified."
echo "Building and launching Dad Planner 2..."
dotnet run --project "$PROJECT_FILE" --no-restore
