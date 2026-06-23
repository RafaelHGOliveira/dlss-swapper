#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
APPDIR="${SCRIPT_DIR}/AppDir"

# Check for appimagetool
if command -v appimagetool &>/dev/null; then
    HAS_APPIMAGETOOL=1
else
    echo "Warning: appimagetool not found in PATH. AppImage will not be produced." >&2
    echo "  Install it from https://github.com/AppImage/AppImageKit/releases" >&2
    HAS_APPIMAGETOOL=0
fi

# Publish the application
echo "Publishing DLSS Swapper Linux..."
dotnet publish "${REPO_ROOT}/src-linux/DLSS.Swapper.Linux.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained \
    -o "${APPDIR}/usr/bin/"

# AppRun
cat > "${APPDIR}/AppRun" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "${HERE}/usr/bin/DLSS.Swapper.Linux" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

# .desktop file
cat > "${APPDIR}/DLSS-Swapper.desktop" <<'EOF'
[Desktop Entry]
Name=DLSS Swapper
Comment=Swap DLSS DLLs for your games
Exec=DLSS-Swapper
Icon=DLSS-Swapper
Type=Application
Categories=Game;Utility;
EOF

# Icon placeholder (user should replace with actual icon)
if [ ! -f "${APPDIR}/DLSS-Swapper.png" ]; then
    touch "${APPDIR}/DLSS-Swapper.png"
    echo "Note: ${APPDIR}/DLSS-Swapper.png is a placeholder. Replace it with the actual icon."
fi

# Package as AppImage if tool is available
if [ "${HAS_APPIMAGETOOL}" -eq 1 ]; then
    OUTPUT="${REPO_ROOT}/DLSS-Swapper-x86_64.AppImage"
    echo "Building AppImage..."
    appimagetool "${APPDIR}" "${OUTPUT}"
    echo "AppImage created: ${OUTPUT}"
else
    echo ""
    echo "Build complete. To create the AppImage, install appimagetool and re-run this script:"
    echo "  https://github.com/AppImage/AppImageKit/releases"
fi
