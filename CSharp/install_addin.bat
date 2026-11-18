@echo off
setlocal EnableExtensions

rem ============================================================
rem  Deploy FamilyNameAuditor.addin into Revit 2024 Addins folder
rem  Run this AFTER build_plugin.bat succeeds.
rem  Requires administrator rights (writes to ProgramData).
rem ============================================================

set "SRC=D:\FamilyNameAuditor\CSharp\FamilyNameAuditor.addin"
set "DEST=C:\ProgramData\Autodesk\Revit\Addins\2024\FamilyNameAuditor.addin"
set "DLL=D:\FamilyNameAuditor\CSharp\FamilyNameAuditor\bin\Debug\FamilyNameAuditor.dll"

if not exist "%SRC%" (
  echo [ERROR] addin file not found: %SRC%
  goto :end
)
if not exist "%DLL%" (
  echo [ERROR] DLL not found. Run build_plugin.bat first.
  goto :end
)

copy /Y "%SRC%" "%DEST%" >nul 2>&1
if errorlevel 1 (
  echo [FAILED] Cannot write to %DEST%
  echo Right-click this bat - Run as administrator.
  goto :end
)

echo.
echo === INSTALLED ===
echo %DEST%
echo.
echo Now: close Revit completely, reopen, open a test project,
echo then Add-ins tab - External Tools - FNA - Audit (Read Only).

:end
echo.
pause
