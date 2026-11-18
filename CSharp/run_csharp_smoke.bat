@echo off
setlocal EnableExtensions
chcp 65001 >nul

rem ============================================================
rem  C# Logic Smoke Test - one click compile and run
rem  No Visual Studio needed. Uses .NET Framework 4.8 csc.exe.
rem ============================================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "ROOT=D:\FamilyNameAuditor\CSharp"
set "LIB=%ROOT%\FamilyNameAuditor"
set "TEST=%ROOT%\LogicSmokeTest"
set "OUTDIR=%TEST%\bin\Debug"
set "EXE=%OUTDIR%\LogicSmokeTest.exe"

if not exist "%CSC%" (
  echo [ERROR] Compiler not found: %CSC%
  goto :end
)

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo.
echo === Compiling C# smoke test (no Visual Studio needed) ===
echo.

"%CSC%" /nologo /target:exe /langversion:5 /out:"%EXE%" ^
 /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll ^
 "%LIB%\Config.cs" ^
 "%LIB%\Models\NamingRules.cs" ^
 "%LIB%\Services\NamingAuditor.cs" ^
 "%LIB%\Services\AuditReportWriter.cs" ^
 "%TEST%\Program.cs"

if errorlevel 1 (
  echo.
  echo [COMPILE FAILED] Copy the error lines above and send them.
  goto :end
)

echo.
echo === Compile OK. Running smoke test... ===
echo.

"%EXE%"

echo.
echo === Done. Copy the summary line above and send it to me. ===

:end
echo.
pause
