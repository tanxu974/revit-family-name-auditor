@echo off
setlocal EnableExtensions

rem ============================================================
rem  Build Revit 2024 plugin DLL - one click, no Visual Studio
rem  Uses .NET Framework 4.8 csc.exe + Revit 2024 API assemblies
rem ============================================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "ROOT=D:\FamilyNameAuditor\CSharp"
set "LIB=%ROOT%\FamilyNameAuditor"
set "RVT=D:\Revit2024\Revit 2024"
set "OUTDIR=%LIB%\bin\Debug"
set "DLL=%OUTDIR%\FamilyNameAuditor.dll"

if not exist "%CSC%" (
  echo [ERROR] Compiler not found: %CSC%
  goto :end
)
if not exist "%RVT%\RevitAPI.dll" (
  echo [ERROR] RevitAPI.dll not found at %RVT%
  goto :end
)

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo.
echo === Compiling Revit plugin DLL (no Visual Studio needed) ===
echo.

"%CSC%" /nologo /target:library /langversion:5 /out:"%DLL%" ^
 /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll ^
 /r:"%RVT%\RevitAPI.dll" /r:"%RVT%\RevitAPIUI.dll" ^
 "%LIB%\Config.cs" ^
 "%LIB%\Models\NamingRules.cs" ^
 "%LIB%\Services\NamingAuditor.cs" ^
 "%LIB%\Services\AuditReportWriter.cs" ^
 "%LIB%\Commands\AuditFamiliesCommand.cs" ^
 "%LIB%\Commands\RenameFamiliesCommand.cs" ^
 "%LIB%\Properties\AssemblyInfo.cs"

if errorlevel 1 (
  echo.
  echo [BUILD FAILED] Copy the error lines above and send them.
  goto :end
)

echo.
echo === BUILD OK ===
echo DLL: %DLL%
echo.
echo Next: copy FamilyNameAuditor.addin into Revit Addins folder ^(script below^)

:end
echo.
pause
