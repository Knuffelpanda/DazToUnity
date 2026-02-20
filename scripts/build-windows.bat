@echo off
setlocal EnableDelayedExpansion

REM ============================================================
REM  DazToUnity - Windows Local Build Script
REM ============================================================
REM  Override any default via environment variable, e.g.:
REM    set DAZ_SDK_DIR=C:\MySDK && scripts\build-windows.bat
REM
REM  Variables:
REM    DAZ_SDK_DIR   Path to Daz Studio SDK root
REM    FBX_SDK_DIR   Path to FBX SDK
REM    OSD_DIR       Path to OpenSubdiv root (must have install\ subfolder)
REM    BUILD_DIR     CMake build folder
REM    RELEASE_TAG   If set, uploads .dll to that GitHub release (e.g. v1.0.0)
REM ============================================================

REM ---- Defaults ----
if "%DAZ_SDK_DIR%"=="" set "DAZ_SDK_DIR=D:\DAZ 3D\Library\DAZStudio4.5+ SDK"
if "%FBX_SDK_DIR%"=="" set "FBX_SDK_DIR=C:\Program Files\Autodesk\FBX\FBX SDK\2020.3.9"
if "%OSD_DIR%"==""     set "OSD_DIR=D:\Github\OpenSubdiv"
if "%BUILD_DIR%"==""   set "BUILD_DIR=build"

echo.
echo === DazToUnity Windows Build ===
echo DAZ_SDK_DIR : %DAZ_SDK_DIR%
echo FBX_SDK_DIR : %FBX_SDK_DIR%
echo OSD_DIR     : %OSD_DIR%
echo BUILD_DIR   : %BUILD_DIR%
if not "%RELEASE_TAG%"=="" echo RELEASE_TAG : %RELEASE_TAG%
echo.

REM ---- Validate DAZ SDK ----
if not exist "%DAZ_SDK_DIR%\" (
    echo ERROR: DAZ_SDK_DIR does not exist: %DAZ_SDK_DIR%
    echo Override with: set DAZ_SDK_DIR=^<path^>
    exit /b 1
)

REM ---- Check / Install FBX SDK ----
if not exist "%FBX_SDK_DIR%\lib\x64\release\libfbxsdk-md.lib" (
    echo [1/3] FBX SDK not found at %FBX_SDK_DIR%. Downloading...
    curl -L "https://damassets.autodesk.net/content/dam/autodesk/www/files/fbx202039_fbxsdk_vs2022_win.exe" -o "%TEMP%\fbxsdk_installer.exe"
    if errorlevel 1 (
        echo ERROR: Failed to download FBX SDK.
        echo Download manually from: https://www.autodesk.com/developer-network/platform-technologies/fbx-sdk-2020-3-4
        exit /b 1
    )
    echo Installing FBX SDK to %FBX_SDK_DIR% ^(silent install^)...
    start /wait "" "%TEMP%\fbxsdk_installer.exe" /S /D=%FBX_SDK_DIR%
    if not exist "%FBX_SDK_DIR%\lib\x64\release\libfbxsdk-md.lib" (
        echo ERROR: FBX SDK install completed but library not found.
        echo Check install path or set FBX_SDK_DIR manually.
        exit /b 1
    )
    echo FBX SDK installed successfully.
) else (
    echo [1/3] FBX SDK found at %FBX_SDK_DIR%
)

REM ---- Check / Build OpenSubdiv ----
if not exist "%OSD_DIR%\install\lib\osdCPU.lib" (
    echo [2/3] OpenSubdiv not found. Building from source...

    if not exist "%OSD_DIR%\src" (
        git clone --depth 1 --branch v3_6_0 ^
            https://github.com/PixarAnimationStudios/OpenSubdiv.git "%OSD_DIR%\src"
        if errorlevel 1 (
            echo ERROR: Failed to clone OpenSubdiv.
            exit /b 1
        )
    )

    cmake -B "%OSD_DIR%\build_cmake" -S "%OSD_DIR%\src" ^
        -DNO_PTEX=ON -DNO_DOC=ON -DNO_OMP=ON -DNO_TBB=ON -DNO_CUDA=ON ^
        -DNO_OPENCL=ON -DNO_CLEW=ON -DNO_GLEW=ON -DNO_GLFW=ON ^
        -DNO_GLFW_X11=ON -DNO_EXAMPLES=ON -DNO_TUTORIALS=ON ^
        -DNO_REGRESSION=ON -DNO_TESTS=ON ^
        -DCMAKE_INSTALL_PREFIX="%OSD_DIR%\install"
    if errorlevel 1 (
        echo ERROR: CMake configure for OpenSubdiv failed.
        exit /b 1
    )

    cmake --build "%OSD_DIR%\build_cmake" --config Release --target install
    if errorlevel 1 (
        echo ERROR: OpenSubdiv build failed.
        exit /b 1
    )
    echo OpenSubdiv built and installed to %OSD_DIR%\install
) else (
    echo [2/3] OpenSubdiv found at %OSD_DIR%\install
)

REM ---- Configure DazToUnity ----
echo [3/3] Configuring DazToUnity...
cmake -B %BUILD_DIR% -S . ^
    "-DDAZ_SDK_DIR=%DAZ_SDK_DIR%" ^
    "-DFBX_SDK_DIR=%FBX_SDK_DIR%" ^
    "-DFBX_SDK_LIB=%FBX_SDK_DIR%\lib\x64\release\libfbxsdk-md.lib" ^
    "-DFBX_SDK_XMLLIB=%FBX_SDK_DIR%\lib\x64\release\libxml2-md.lib" ^
    "-DOPENSUBDIV_DIR=%OSD_DIR%\install\include" ^
    "-DOPENSUBDIV_LIB=%OSD_DIR%\install\lib\osdCPU.lib" ^
    "-DCMAKE_POLICY_VERSION_MINIMUM=3.5"
if errorlevel 1 (
    echo ERROR: CMake configure failed.
    exit /b 1
)

REM ---- Build DazToUnity ----
echo Building...
cmake --build %BUILD_DIR% --config Release
if errorlevel 1 (
    echo ERROR: Build failed.
    exit /b 1
)

echo.
echo ============================================================
echo  Build complete!
echo  Output: %BUILD_DIR%\bin\Release\dzunitybridge.dll
echo ============================================================

REM ---- Upload to GitHub Release (optional) ----
if not "%RELEASE_TAG%"=="" (
    echo Uploading dzunitybridge.dll to release %RELEASE_TAG%...
    gh release upload "%RELEASE_TAG%" "%BUILD_DIR%\bin\Release\dzunitybridge.dll" --clobber
    if errorlevel 1 (
        echo ERROR: Upload failed. Make sure 'gh' is installed and authenticated.
        exit /b 1
    )
    echo Upload complete.
) else (
    echo.
    echo To upload to a GitHub release, set RELEASE_TAG before running:
    echo   set RELEASE_TAG=v1.2.0 ^&^& scripts\build-windows.bat
)
endlocal
