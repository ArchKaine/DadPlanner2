# Dad-Planner 2 (DP2)

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


```

The launch scripts are intended to be run from a checkout of this repository. They resolve their own location, so they also work when started from another current directory.

---

## ✨ Key Features

### 📊 Clinical Telemetry & Tracking

* **Dynamic Telemetry HUD:** Real-time calculation of time elapsed since your last event, rolling averages, and maximum endurance gaps. Models recovery through 4 distinct biological phases: Recharging, Optimal Viability, Extended Reserve, and Viability Fading.
* **Configurable Boundary Thresholds:** Set your own clinical "Floor" (minimum refractory period to avoid volume depletion) and "Ceiling" (maximum hours between routine maintenance).
* **Senescence Klaxon:** Hardware-level behavioral alert. If the recovery gap extends into dangerous senescence (default >144 hours), an un-ignorable, flashing full-screen klaxon overlays the application, preventing UI interaction until dismissed.
* **Quad-State Logging:** Distinguish between Maintenance (solo/blue), Playtime (recreational/purple), Baby-Making (conception/green), and Clinical-Lab (medical baselines/slate) with dedicated UI routing.
* **Granular Clinical Metrics:** Dedicated numerical inputs for formal semen analysis parameters, capturing Clinical Volume (mL), Concentration (M), Total Motility (%), Progressive Motility (%), Morphology (%), and pH Level.
* **Lab Report PDF Vault:** Attach, store (as SQLite BLOBs), and launch original laboratory PDF results directly from the Avalonia dashboard via your native OS document viewer.
* **Pre-Log Modifiers:** Track crucial biological variables like subjective volume, release count, volume confidence, a 4-level Thermal Stress Index, and context/symptom notes.
* **Supplement Tracking:** Persisted tracking for specific dietary stacks (Zinc, Maca, Vitamin D3, Vitamin C).

### 📈 Interactive Analytics & Dashboards

* **Pharmacokinetic Decay Overlay:** Calculates and graphs the mathematical elimination curves (C = C₀ · e⁻ᵏᵗ) of tracked supplements directly beneath the macro-trend graph, mapping specific biological half-lives (e.g., Zinc: 12 days, Maca: 20 hours) to visualize overlapping blood-serum saturation.
* **365-Day Activity Matrix:** GitHub-style density heatmap built natively in XAML, plotting year-round event frequency and maximum daily volume yields. Click any day's tile to immediately locate and scroll to that session in the data grid.
* **Macro-Trend Enthusiasm:** A 14-day rolling expenditure graph that compares cumulative biological output against an optimal mathematical baseline derived from your calibrated recovery thresholds.
* **Interactive Charting:** Hardware-accelerated LiveCharts dashboards featuring Cartesian timelines, Pie distributions, and Stacked Row profiles.
* **Strict Spatial UI Processing:** Engineered with strict quadrant math and spatial constraints, custom chart tooltips dynamically calculate their position to render inward, mathematically preventing UI bleed into adjacent controls or data grids. Strict pointer-event gatekeeping prevents background charts from calculating or projecting overlays when modal dialogs are open.

### 🏥 Medical Workflows & Reporting

* **Clinical Delta Matrix:** A dedicated diagnostic view that directly compares any two stored Clinical-Lab records. Calculates absolute changes and relative percentage improvement across Volume, Concentration, Motility, Progressive Motility, and Morphology, while cross-referencing behavioral changes (average gap differences, thermal events, and zinc saturation) during the preceding 74-day spermatogenesis window.
* **74-Day Thermal Shadow Engine:** Maps the delayed biological impact of severe heat events (>101°F fever or prolonged hot tub/sauna exposure) on spermatogenesis. Automatically flags the system as compromised for a full 74-day cycle to prevent corrupting statistical baselines or wasting money on premature clinical testing.
* **Ground-Truth Clinical Override:** Dynamically breaks an active Thermal Shadow if a subsequent formal lab test returns normal WHO baseline metrics (≥ 15M/mL Concentration, ≥ 40% Motility), proving system health and restoring analytical tracking.
* **Biological Saturation Analysis:** Evaluates physiological buildup by mapping a rolling window to determine supplement saturation. Runs statistical comparisons on contiguous, uncompromised datasets (excluding Thermal Shadows) to prove whether specific supplements mathematically increase volume yield or accelerate recovery speed.
* **Auto-Calibration Engine:** Mathematically analyzes historical recovery gaps to automatically recommend personalized Floor and Ceiling thresholds based on your standard deviation.
* **90-Day Retrospective Report:** Instantly synthesize your last three months of data into a formatted, printable PDF. Utilizes headless Skia engine routing to generate crisp, print-ready data charts directly into the document structure. Includes a toggle to cleanly strip personal session notes from the final clinical export.
* **Clinical Blackout Mode:** Locks in mandatory abstinence windows and visually suppresses data interference prior to scheduled medical baseline testing. Tracks WHO 2021 Clinical Abstinence Compliance (Ideal: 48-72h, Acceptable: 48-168h).

### 🔒 Security, Audit & Privacy

* **Fully Air-Gapped:** Zero external API calls, no telemetry, no cloud sync. All data is written exclusively to a local `inventory.db` file located in your secure user AppData directory.
* **Immutable Edit History:** Editing an existing log does not overwrite the past. Every modification (via the UI) creates an immutable snapshot in the `LogEditHistory` table detailing exactly what changed, when, and what the previous values were. Snapshots can be inspected and instantly restored from the edit dialog.
* **Zero-Corruption Auto-Backups:** Configured with SQLite WAL-mode and busy-timeout protection. Bypasses naive file-copying lock issues by utilizing SQLite's native `VACUUM INTO` command. Triggers transactionally consistent, defragmented backups on application exit (only if data was mutated during the session) and automatically maintains a rolling 10-file retention policy.
* **Pre-Migration Safety Snapshots:** Automatically generates a targeted database snapshot before running schema migrations or column injections to prevent catastrophic update failures.
* **Stealth Mode (Panic Button):** Hardware-level keybinding (Press `Escape`) instantly applies a hardware-accelerated Avalonia `BlurEffect` across the entire UI grid, obfuscating all sensitive data from screen-lookers on demand.
* **Non-Destructive Sandbox Mode:** Safely swaps your live SQLite database into a backup partition, seeds the UI with 300 records of procedurally generated, biologically weighted fake data for visual testing, and seamlessly restores your real data when toggled off.

---

## 🛠️ Installation & Setup

**Prerequisites:**

* .NET SDK 10.0+ installed on your system.
* *Note for Linux users: By moving to Avalonia, WebKit2GTK is no longer required. The app renders natively via Skia.*

Clone the repository and enter its root directory:

```bash
git clone [https://github.com/ArchKaine/DadPlanner2.git](https://github.com/ArchKaine/DadPlanner2.git)
cd DadPlanner2


```

**Running the Application in Dev Mode:**

To launch the desktop UI directly from the source code, run the launcher corresponding to your operating system. The scripts are stored beside `DadPlanner2.csproj` at the repository root and run from any working directory.

**For Linux (Fedora/Nobara):**

```bash
./dadplanner-2-run.sh

```

**For macOS:**
Apple's Gatekeeper flags unsigned scripts by default. Grant execution permissions and clear the quarantine flag before running:

```bash
chmod +x dadplanner-2-run.command
xattr -c dadplanner-2-run.command
./dadplanner-2-run.command


```

**For Windows:**

```cmd
dadplanner-2-run.bat


```

**Fedora/Nobara application-menu launcher:**

From the repository root, install a user-local `.desktop` entry:

```bash
./dadplanner-2-install-desktop.sh


```

The entry is written to `${XDG_DATA_HOME:-~/.local/share}/applications`, so no root access is required. It launches the repository's existing pre-flight script in a terminal, which checks for the .NET 10 SDK, restores dependencies, and starts the application. If the repository is moved, rerun the installer to refresh the stored path.

**Data and backup locations:**

The database is created as `inventory.db` beneath the platform-specific application-data directory selected by .NET. On Fedora/Nobara this is normally under `~/.local/share/PIMS/`; on Windows it is under the current user's local application-data directory; on macOS it resides in `~/Library/Application Support/PIMS/`. Backups are stored alongside the application data in the configured backup directory and retain the newest 10 snapshots.

**Run tests:**

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

Published files are written under `bin/Release/` and distribute as a complete publish output. The repository launchers serve as development launchers: they require the .NET 10 SDK and restore NuGet packages before starting the project.

---

## ⚠️ Interpretation and Safety

Dad Planner 2 organizes personal observations; it does not diagnose infertility, assess medical
risk, or establish that a supplement caused an outcome. Recovery gaps, saturation percentages,
thermal-shadow filtering, pharmacokinetic curves, and supplement comparisons are observational calculations whose
quality depends on consistent logging and adequate samples.

Clinical values and attached reports require review with a qualified medical professional.
The local database contains sensitive health information: protect the operating-system account,
backups, exported files, and any published artifacts accordingly.
