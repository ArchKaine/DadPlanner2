# Dad-Planner 2 (DP2)

This software is intended for guys like me who want to be a dad, and who are interested in taking full control of their data and their telemetry.

Standard calendar apps aren't built for clinical reproductive health. When you need to manage strict medical testing requirements (like OHSU semen analysis protocols), maintain baseline prostate health with rigid turnover limits, and track supplement efficacy, you need precise telemetry. More importantly, you need that data kept completely offline.

Even if I can't become a dad (Which at time of writing is still up in the air), this is my contribution to those who want to. To you guys, I wish the best of luck and more than a few 'swimmers' jokes :)

Dad-Planner 2 is a highly over-engineered, offline-first sexual frequency, biological baseline, and clinical tracker. It enforces routine health cycles, captures specific clinical variables (thermal stress, bespoke biometrics, diurnal saturation), and provides interactive local statistical analysis—all while ensuring your most private biological data never leaves your machine.

This second-generation build (DP2) discards the original web-wrapper approach for a pure, compiled C# Avalonia desktop architecture.

### 🛡️ The Open-Source Manifesto

If you find a bug, open an issue on GitHub. Want to contribute? Fork a copy and start coding. But know that my line in the sand for this project is absolute:

* **No ads or sponsored content.**
* **No paywalls, subscriptions, or "freemium" features.**
* **No telemetry, background tracking, or data harvesting.**
* **No forced logins or cloud dependencies.**
* **No support for proprietary, closed-source forks.**

This is true open-source, and the main repo will remain completely clean of modern software cruft. If you see a missing feature that genuinely improves men's health and fertility tracking—write the code, open a PR, and let's see it.

---

## 🏗️ Architecture Stack

* **Backend:** C# / .NET 10 (Native OS file-handlers, background processing, QuestPDF document generation)
* **Frontend:** Avalonia UI 11 (Native, hardware-accelerated cross-platform desktop UI using XAML and MVVM architecture)
* **Graphics Engine:** SkiaSharp (Headless rendering for PDF chart injection and high-performance UI drawing)
* **Database:** SQLite3 (Local Only, WAL-mode enabled, direct C# `SqliteConnection` bindings with BLOB storage for raw medical files)
* **Testing:** MSTest integration and service tests targeting .NET 10

## 📦 Dependencies & Libraries

**Core NuGet Packages:**

* **Avalonia & Avalonia.Desktop** - The core cross-platform UI framework replacing the old HTML/JS frontend.
* **LiveChartsCore.SkiaSharpView.Avalonia (v2.0.0-rc3)** - Highly interactive, Skia-rendered UI charting for the timeline, event distribution, and yield profiles.
* **QuestPDF** - Native C# vector graphics engine for synthesizing the 90-Day PDF reports, combined with LiveCharts headless Skia rendering for offline chart generation.
* **Microsoft.Data.Sqlite** - Lightweight, local database driver for telemetry storage.
* **SkiaSharp.NativeAssets.Linux / .macOS / .Win32** - Required platform-specific native binaries. Without these OS-specific libraries, headless PDF generation and hardware-accelerated charting fail to render.

---

## 📁 Repository Layout

The application project is at the repository root; tests are kept in the `Tests/` directory.

```text
DadPlanner2.csproj             Main Avalonia desktop application
App.axaml, MainWindow.axaml    Application and desktop UI definitions
ViewModels/                    MVVM presentation logic
Models/                        Persisted domain models
Services/                      SQLite, export, analysis, reporting, and validation services
Tests/                         MSTest project and integration coverage
dadplanner-2-run.sh            Fedora/Nobara and other Linux development launcher
dadplanner-2-run.bat           Windows development launcher
dadplanner-2-run.command       macOS development launcher
dadplanner-2-install-desktop.sh User-local Linux application-menu installer
