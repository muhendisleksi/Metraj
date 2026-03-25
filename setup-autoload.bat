@echo off
chcp 1254 >nul 2>&1
echo ========================================
echo  Metraj Asistani - Otomatik Yukleme Kurulumu
echo ========================================
echo.

set "PROJECT_DIR=%~dp0"
set "DLL_PATH=%PROJECT_DIR%bin\Debug\net8.0-windows\Metraj.dll"
set "LSP_FILE=%PROJECT_DIR%metraj-autoload.lsp"

echo [1/3] Dosyalar kontrol ediliyor...
echo   DLL : %DLL_PATH%
echo   LSP : %LSP_FILE%
echo.

if not exist "%DLL_PATH%" (
    echo [HATA] Metraj.dll bulunamadi!
    echo   Once projeyi derleyin: dotnet build
    echo.
    pause
    exit /b 1
)

if not exist "%LSP_FILE%" (
    echo [HATA] metraj-autoload.lsp bulunamadi!
    echo.
    pause
    exit /b 1
)

echo [2/3] AutoCAD Support klasoru araniyor...
echo.

REM Try common AutoCAD 2025 support paths
set "ACAD_SUPPORT="

REM AutoCAD 2025 English
if exist "%APPDATA%\Autodesk\AutoCAD 2025\R25.0\enu\Support" (
    set "ACAD_SUPPORT=%APPDATA%\Autodesk\AutoCAD 2025\R25.0\enu\Support"
    goto :found
)

REM Civil 3D 2025 English
if exist "%APPDATA%\Autodesk\C3D 2025\enu\Support" (
    set "ACAD_SUPPORT=%APPDATA%\Autodesk\C3D 2025\enu\Support"
    goto :found
)

REM AutoCAD 2025 Turkish
if exist "%APPDATA%\Autodesk\AutoCAD 2025\R25.0\trk\Support" (
    set "ACAD_SUPPORT=%APPDATA%\Autodesk\AutoCAD 2025\R25.0\trk\Support"
    goto :found
)

REM Civil 3D 2025 Turkish
if exist "%APPDATA%\Autodesk\C3D 2025\trk\Support" (
    set "ACAD_SUPPORT=%APPDATA%\Autodesk\C3D 2025\trk\Support"
    goto :found
)

REM Try to find any AutoCAD 2025 support path
for /d %%D in ("%APPDATA%\Autodesk\AutoCAD 2025\R25.0\*") do (
    if exist "%%D\Support" (
        set "ACAD_SUPPORT=%%D\Support"
        goto :found
    )
)

REM Try any C3D 2025 path
for /d %%D in ("%APPDATA%\Autodesk\C3D 2025\*") do (
    if exist "%%D\Support" (
        set "ACAD_SUPPORT=%%D\Support"
        goto :found
    )
)

echo [HATA] AutoCAD 2025 Support klasoru bulunamadi!
echo   Beklenen konumlar:
echo     %%APPDATA%%\Autodesk\AutoCAD 2025\R25.0\enu\Support
echo     %%APPDATA%%\Autodesk\C3D 2025\enu\Support
echo.
echo   Manuel kurulum icin:
echo     1. AutoCAD'da APPLOAD yazin
echo     2. Startup Suite'e tiklayin
echo     3. metraj-autoload.lsp dosyasini ekleyin
echo.
pause
exit /b 1

:found
echo   Bulunan: %ACAD_SUPPORT%
echo.

echo [3/3] acaddoc.lsp guncelleniyor...

REM Create acaddoc.lsp if it doesn't exist
if not exist "%ACAD_SUPPORT%\acaddoc.lsp" (
    echo ; AutoCAD Document Initialization > "%ACAD_SUPPORT%\acaddoc.lsp"
    echo   acaddoc.lsp olusturuldu.
)

REM Check if already registered
findstr /C:"metraj-autoload.lsp" "%ACAD_SUPPORT%\acaddoc.lsp" >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   Metraj otomatik yukleme zaten kayitli.
    goto :done
)

REM Convert LSP path to forward slashes for LISP
set "LSP_LISP=%LSP_FILE:\=/%"

REM Append load command to acaddoc.lsp
echo.>> "%ACAD_SUPPORT%\acaddoc.lsp"
echo ; Metraj Asistani otomatik yukleme>> "%ACAD_SUPPORT%\acaddoc.lsp"
echo (load "%LSP_LISP%" nil)>> "%ACAD_SUPPORT%\acaddoc.lsp"

echo   Otomatik yukleme eklendi.

:done
echo.
echo ========================================
echo  [BASARILI] Kurulum tamamlandi!
echo ========================================
echo.
echo AutoCAD'i yeniden baslattiginizda:
echo   - Metraj.dll otomatik yuklenecek
echo   - Ribbon menude "Metraj" sekmesi gorunecek
echo.
echo Not: SECURELOAD uyarisi cikarsa, AutoCAD komut satirina
echo   SECURELOAD yazin ve degeri 0 yapin.
echo.
pause
