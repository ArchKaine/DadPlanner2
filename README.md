# Dad-Planner 2 (DP2)

<img width="1287" height="935" alt="DP2" src="https://github.com/user-attachments/assets/b54460a6-311c-47df-bdf0-8daafe4cab7a" />


This software is intended for guys like me who want to be a dad, and who are interested in taking full control of their data and their telemetry.

Standard calendar apps aren't built for clinical reproductive health. When you need to manage strict medical testing requirements (like OHSU semen analysis protocols), maintain baseline prostate health with rigid turnover limits, and track supplement efficacy, you need precise telemetry. More importantly, you need that data kept completely offline.

Even if I can't become a dad (Which at time of writing is still up in the air), this is my contribution to those who want to. To you guys, I wish the best of luck and more than a few 'swimmers' jokes :)

Dad-Planner 2 is a highly over-engineered, offline-first sexual frequency, biological baseline, and clinical tracker. It enforces routine health cycles, captures specific clinical variables (thermal stress, subjective volume, biological saturation), and provides interactive local statistical analysis—all while ensuring your most private biological data never leaves your machine.

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
* **CI/CD:** Automated GitHub Actions pipeline for multi-OS binary compilation

## 📦 Dependencies & Libraries

**Core NuGet Packages:**

* **Avalonia & Avalonia.Desktop** - The core cross-platform UI framework replacing the old HTML/JS frontend.
* **LiveChartsCore.SkiaSharpView.Avalonia (v2.0.0-rc3)** - Highly interactive, Skia-rendered UI charting for the timeline, event distribution, and yield profiles.
* **QuestPDF** - Native C# vector graphics engine for synthesizing the 90-Day PDF reports, combined with LiveCharts headless Skia rendering for offline chart generation.
* **Microsoft.Data.Sqlite** - Lightweight, local database driver for telemetry storage.

---

## ✨ Key Features

### 📊 Clinical Telemetry & Tracking

* **Dynamic Telemetry HUD:** Real-time calculation of time elapsed since your last event, rolling averages, and maximum endurance gaps.
* **Configurable Boundary Thresholds:** Set your own clinical "Floor" (minimum refractory period to avoid volume depletion) and "Ceiling" (maximum hours between routine maintenance).
* **Quad-State Logging:** Distinguish between Maintenance (solo/blue), Playtime (recreational/purple), Baby-Making (conception/green), and Clinical-Lab (medical baselines/slate) with dedicated UI routing.
* **Granular Clinical Metrics:** Dedicated numerical inputs for formal semen analysis parameters, capturing Clinical Volume (mL), Concentration (M), Total Motility (%), Progressive Motility (%), Morphology (%), and pH Level.
* **Lab Report PDF Vault:** Attach, store (as SQLite BLOBs), and launch original laboratory PDF results directly from the Avalonia dashboard via your native OS document viewer.
* **Pre-Log Modifiers:** Track crucial biological variables like subjective volume (Dry/Low/Normal/High), a 4-level Thermal Stress Index, and dietary supplement stacks (Zinc, Maca, Vitamin D3, Vitamin C).
* **365-Day Activity Matrix:** GitHub-style density heatmap built natively in XAML, plotting year-round event frequency and maximum daily volume yields.
* **Interactive Charting:** Hardware-accelerated LiveCharts dashboards featuring Cartesian timelines, Pie distributions, and Stacked Row profiles.
* **Strict Spatial UI Processing:** Engineered with strict quadrant math and spatial constraints, custom chart tooltips dynamically calculate their position to render inward, mathematically preventing UI bleed into adjacent controls or data grids. Strict pointer-event gatekeeping prevents background charts from calculating or projecting overlays when modal dialogs are open.

### 🏥 Medical Workflows & Analytics

* **74-Day Thermal Shadow Engine:** Maps the delayed biological impact of severe heat events (>101°F fever or prolonged hot tub/sauna exposure) on spermatogenesis. Automatically flags the system as compromised for a full 74-day cycle to prevent corrupting statistical baselines or wasting money on premature clinical testing.
* **Ground-Truth Clinical Override:** Dynamically breaks an active Thermal Shadow if a subsequent formal lab test returns normal WHO baseline metrics (≥ 15M/mL Concentration, ≥ 40% Motility), proving system health and restoring analytical tracking.
* **Biological Saturation Analysis:** Evaluates physiological buildup by mapping a rolling window to determine supplement saturation. Runs statistical comparisons on contiguous, uncompromised datasets (excluding Thermal Shadows) to prove whether specific supplements mathematically increase volume yield or accelerate recovery speed.
* **Auto-Calibration Engine:** Mathematically analyzes historical recovery gaps to automatically recommend personalized Floor and Ceiling thresholds based on your standard deviation.
* **90-Day Retrospective Report:** Instantly synthesize your last three months of data into a formatted, printable PDF. Utilizes headless Skia engine routing to generate crisp, print-ready data charts directly into the document structure without requiring screen capture.
* **Clinical Blackout Mode:** Locks in mandatory abstinence windows and visually suppresses data interference prior to scheduled medical baseline testing (e.g., OHSU Andrology Lab protocols).
* **Inline Data Correction:** Retroactively fix timestamps, event modes, volume metrics, or update attached lab PDFs via the native Avalonia DataGrid.

### 🔒 Security & Privacy

* **Fully Air-Gapped:** Zero external API calls, no telemetry, no cloud sync. All data is written exclusively to a local `inventory.db` file located in your secure user AppData directory.
* **Zero-Corruption Auto-Backups:** Bypasses naive file-copying lock issues by utilizing SQLite's native `VACUUM INTO` command. Triggers transactionally consistent, defragmented backups on application exit (only if data was mutated during the session) and automatically maintains a rolling 10-file retention policy in an OS-safe local directory.
* **Stealth Mode (Panic Button):** Hardware-level keybinding (Press `Escape`) instantly applies a hardware-accelerated Avalonia `BlurEffect` across the entire UI grid, obfuscating all sensitive data from screen-lookers on demand.
* **Non-Destructive Sandbox Mode:** Safely swaps your live SQLite database into a backup partition, seeds the UI with 300 records of procedurally generated, biologically weighted fake data for visual testing, and seamlessly restores your real data when toggled off.

---

## 🛠️ Installation & Setup

**Prerequisites:**

* .NET SDK 10.0+ installed on your system.
* *Note for Linux users: By moving to Avalonia, WebKit2GTK is no longer required. The app renders natively via Skia.*

**Running the Application in Dev Mode:**
To launch the desktop UI directly from the source code:

```bash
./dadplanner-2-run.sh
```

The launcher scripts are stored beside `DadPlanner2.csproj` at the repository root and can be run from any working directory. To run the test project:

```bash
dotnet test Tests/DadPlanner2.Tests.csproj

```

---

## 🚀 Compilation & Deployment

Dad-Planner 2 is configured to compile into standalone executables via standard .NET publish commands.

**Compile for Linux (Primary Target - Nobara/KDE Tested):**

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

```

**Compile for Windows:**

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

```

**Compile for macOS:**

```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true

```
