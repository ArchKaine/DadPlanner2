#!/usr/bin/env bash
set -euo pipefail

echo "🤖 [SYSTEM PROTOCOL: BASH BUILD FIXER INITIALIZED]"

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "$ROOT_DIR"

# Detect macOS vs Linux for sed -i compatibility
SED_INPLACE=(-i)
if [[ "$OSTYPE" == "darwin"* ]]; then
    SED_INPLACE=(-i '')
fi

# 1. Fix DadPlanner2.csproj to exclude the Tests directory from the main build
CSPROJ="DadPlanner2.csproj"
if [ -f "$CSPROJ" ]; then
    if ! grep -q "Tests\\\\\\*\\*" "$CSPROJ" && ! grep -q "Tests/\*\*" "$CSPROJ"; then
        # Use awk to insert the ItemGroup exclusion right before </Project>
        awk -v block='  <ItemGroup>\n    <Compile Remove="Tests\\**" />\n    <EmbeddedResource Remove="Tests\\**" />\n    <None Remove="Tests\\**" />\n  </ItemGroup>\n' '{
            if ($0 ~ /<\/Project>/) print block;
            print;
        }' "$CSPROJ" > "${CSPROJ}.tmp" && mv "${CSPROJ}.tmp" "$CSPROJ"
        echo "✅ [FIXED] DadPlanner2.csproj updated to ignore the Tests directory."
    else
        echo "✨ [SKIPPED] DadPlanner2.csproj already ignores Tests."
    fi
else
    echo "❌ [ERROR] DadPlanner2.csproj not found. Are you in the repository root?"
fi

# 2. Fix missing using statements for VolumeConfidence
FILES=(
    "Models/LogEditHistory.cs"
    "services/SupplementAnalysisService.cs"
    "services/LogValidationService.cs"
)

for file in "${FILES[@]}"; do
    if [ -f "$file" ]; then
        if ! grep -q "using DadPlanner2.Models;" "$file"; then
            # Prepend using statement safely
            tmp_file=$(mktemp)
            echo "using DadPlanner2.Models;" > "$tmp_file"
            cat "$file" >> "$tmp_file"
            mv "$tmp_file" "$file"
            echo "✅ [FIXED] Added missing namespace reference to $file."
        else
            echo "✨ [SKIPPED] $file already has the using statement."
        fi
    else
        echo "⚠️ [WARNING] File not found: $file"
    fi
done

# 3. Clean --no-restore from dadplanner-2-run.command
RUN_CMD="dadplanner-2-run.command"
if [ -f "$RUN_CMD" ]; then
    if grep -q "--no-restore" "$RUN_CMD"; then
        sed "${SED_INPLACE[@]}" 's/--no-restore//g' "$RUN_CMD"
        echo "✅ [FIXED] dadplanner-2-run.command cleaned of --no-restore."
    else
        echo "✨ [SKIPPED] dadplanner-2-run.command is already pristine."
    fi
else
    echo "⚠️ [WARNING] $RUN_CMD not found."
fi

echo -e "\n🚀 [STATUS: ALL SYSTEMS GO] Run ./dadplanner-2-run.command again and watch it fly!"
