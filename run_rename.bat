@echo off
chcp 65001 >nul
title W3 Rename Preview
echo ============================================
echo  W3 Batch Rename Preview - 15 Test Families
echo ============================================
echo.
"D:\DynamoSandbox\python311\python.exe" "D:\FamilyNameAuditor\engine\apply_rename.py" "D:\FamilyNameAuditor\rules\default_rules.json" "D:\FamilyNameAuditor\testdata\family_names.txt" "D:\FamilyNameAuditor\rename_preview.csv"
echo.
echo Report saved: D:\FamilyNameAuditor\rename_preview.csv
echo.
pause
