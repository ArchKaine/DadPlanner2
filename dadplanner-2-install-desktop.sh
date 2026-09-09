#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
APPLICATIONS_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
DESKTOP_FILE="$APPLICATIONS_DIR/dadplanner-2.desktop"

mkdir -p "$APPLICATIONS_DIR"

cat > "$DESKTOP_FILE" <<EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Dad Planner 2
Comment=Offline personal event and recovery tracking
Exec=/bin/bash "$SCRIPT_DIR/dadplanner-2-run.sh"
Path=$SCRIPT_DIR
Terminal=true
Categories=Utility;Office;
StartupNotify=true
TryExec=/bin/bash
EOF

chmod 644 "$DESKTOP_FILE"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$APPLICATIONS_DIR" >/dev/null 2>&1 || true
fi

echo "Installed Dad Planner 2 in the application menu:"
echo "  $DESKTOP_FILE"
echo "You can launch it from the desktop environment or with:"
echo "  gtk-launch dadplanner-2"
