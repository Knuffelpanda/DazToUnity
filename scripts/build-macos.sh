#!/usr/bin/env bash
# ============================================================
#  DazToUnity - macOS Local Build Script
# ============================================================
#  Configurable via environment variables (or edit defaults):
#
#    DAZ_SDK_DIR   Path to Daz Studio SDK root (REQUIRED)
#    FBX_SDK_DIR   Path to FBX SDK            (default: /opt/fbxsdk)
#    OSD_DIR       Path to OpenSubdiv root     (default: /opt/opensubdiv)
#    BUILD_DIR     CMake build folder           (default: build)
#
#  Example:
#    export DAZ_SDK_DIR=~/DazStudio4.5+SDK
#    bash scripts/build-macos.sh
# ============================================================
set -euo pipefail

# ---- Validate required variables ----
if [ -z "${DAZ_SDK_DIR:-}" ]; then
    echo "ERROR: DAZ_SDK_DIR is not set."
    echo "Set it to the root of the Daz Studio SDK, for example:"
    echo "  export DAZ_SDK_DIR=~/DazStudio4.5+SDK"
    exit 1
fi

if [ ! -d "$DAZ_SDK_DIR" ]; then
    echo "ERROR: DAZ_SDK_DIR does not exist: $DAZ_SDK_DIR"
    exit 1
fi

# ---- Apply defaults ----
: "${FBX_SDK_DIR:=/opt/fbxsdk}"
: "${OSD_DIR:=/opt/opensubdiv}"
: "${BUILD_DIR:=build}"
# RELEASE_TAG (optional) — if set, uploads the .dylib to that GitHub release
# Example: export RELEASE_TAG=v1.2.0

echo ""
echo "=== DazToUnity macOS Build ==="
echo "DAZ_SDK_DIR : $DAZ_SDK_DIR"
echo "FBX_SDK_DIR : $FBX_SDK_DIR"
echo "OSD_DIR     : $OSD_DIR"
echo "BUILD_DIR   : $BUILD_DIR"
echo ""

# ---- Check / Install FBX SDK ----
FBX_LIB="$FBX_SDK_DIR/lib/clang/release/libfbxsdk.a"
if [ ! -f "$FBX_LIB" ]; then
    echo "[1/3] FBX SDK not found at $FBX_SDK_DIR."
    echo "      Download FBX SDK 2020.3.9 (clang, macOS) from:"
    echo "      https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-3-4"
    echo "      Install the .pkg and set FBX_SDK_DIR to the install root, e.g.:"
    echo "        export FBX_SDK_DIR=\"/Applications/Autodesk/FBX SDK/2020.3.9\""
    echo ""
    # Try to auto-detect if installed via pkg
    FBX_DETECTED=$(find "/Applications/Autodesk" -name "libfbxsdk.a" -path "*/clang/release/*" 2>/dev/null | head -1 || true)
    if [ -n "$FBX_DETECTED" ]; then
        FBX_SDK_DIR="$(cd "$(dirname "$FBX_DETECTED")/../../.." && pwd)"
        echo "      Auto-detected FBX SDK at: $FBX_SDK_DIR"
        FBX_LIB="$FBX_SDK_DIR/lib/clang/release/libfbxsdk.a"
    else
        echo "ERROR: FBX SDK not found. Install it first, then re-run this script."
        exit 1
    fi
else
    echo "[1/3] FBX SDK found at $FBX_SDK_DIR"
fi

# ---- Check / Build OpenSubdiv ----
OSD_LIB="$OSD_DIR/install/lib/libosdCPU.a"
if [ ! -f "$OSD_LIB" ]; then
    echo "[2/3] OpenSubdiv not found. Building from source..."
    OSD_SRC="$OSD_DIR/src"
    OSD_BUILD="$OSD_DIR/build_cmake"

    if [ ! -d "$OSD_SRC" ]; then
        git clone --depth 1 --branch v3_6_0 \
            https://github.com/PixarAnimationStudios/OpenSubdiv.git "$OSD_SRC"
    fi

    cmake -B "$OSD_BUILD" -S "$OSD_SRC" \
        -DNO_PTEX=ON -DNO_DOC=ON -DNO_OMP=ON -DNO_TBB=ON -DNO_CUDA=ON \
        -DNO_OPENCL=ON -DNO_CLEW=ON -DNO_GLEW=ON -DNO_GLFW=ON \
        -DNO_GLFW_X11=ON -DNO_EXAMPLES=ON -DNO_TUTORIALS=ON \
        -DNO_REGRESSION=ON -DNO_TESTS=ON \
        -DCMAKE_OSX_ARCHITECTURES=x86_64 \
        -DCMAKE_INSTALL_PREFIX="$OSD_DIR/install"

    cmake --build "$OSD_BUILD" --config Release --target install
    echo "OpenSubdiv built and installed to $OSD_DIR/install"
else
    echo "[2/3] OpenSubdiv found at $OSD_DIR/install"
fi

# ---- Configure DazToUnity ----
echo "[3/3] Configuring DazToUnity..."
cmake -B "$BUILD_DIR" -S . \
    "-DDAZ_SDK_DIR=$DAZ_SDK_DIR" \
    "-DFBX_SDK_DIR=$FBX_SDK_DIR" \
    "-DOPENSUBDIV_DIR=$OSD_DIR/install/include" \
    "-DOPENSUBDIV_LIB=$OSD_DIR/install/lib/libosdCPU.a" \
    -DCMAKE_OSX_ARCHITECTURES=x86_64 \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5

# ---- Build DazToUnity ----
echo "Building..."
cmake --build "$BUILD_DIR" --config Release

echo ""
echo "============================================================"
echo " Build complete!"
echo " Output: $BUILD_DIR/lib/libdzunitybridge.dylib"
echo "============================================================"

# ---- Upload to GitHub Release (optional) ----
if [ -n "${RELEASE_TAG:-}" ]; then
    echo "Uploading libdzunitybridge.dylib to release $RELEASE_TAG..."
    gh release upload "$RELEASE_TAG" "$BUILD_DIR/lib/libdzunitybridge.dylib" --clobber
    echo "Upload complete."
else
    echo ""
    echo "To upload to a GitHub release, set RELEASE_TAG before running:"
    echo "  RELEASE_TAG=v1.2.0 bash scripts/build-macos.sh"
fi
