@echo off
chcp 65001 >nul
title W2 Family Name Audit Engine
echo ============================================
echo  W2 Audit Engine - 15 Test Families
echo ============================================
echo.
"D:\DynamoSandbox\python311\python.exe" "D:\FamilyNameAuditor\engine\audit_core.py" "D:\FamilyNameAuditor\rules\default_rules.json" "D:\FamilyNameAuditor\testdata\family_names.txt" "D:\FamilyNameAuditor\audit_report.csv"
echo.
echo Report saved: D:\FamilyNameAuditor\audit_report.csv
echo Open it in Excel to view Chinese (BOM encoded).
echo.
pause
