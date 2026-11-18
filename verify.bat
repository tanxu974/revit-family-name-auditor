@echo off
chcp 65001 >nul
title W1 Env Verify (Standalone)
echo ============================================
echo  W1 Verify - Read Rules JSON (no Revit/Dynamo)
echo ============================================
echo.
"D:\DynamoSandbox\python311\python.exe" -c "src=open('D:/FamilyNameAuditor/scripts/hello_world.py','r',encoding='utf-8').read(); g={'__name__':'__main__'}; exec(compile(src,'hello_world.py','exec'),g); print(); print('RESULT >>>',g.get('OUT'))"
echo.
echo PASS if you see 'version=1.0, 5 segments' above.
echo.
pause
