@echo off
set "SCRIPT_DIR=%~dp0"
set "LIBS_DIR=%SCRIPT_DIR%Libs"
set "OUTPUT_DIR=%SCRIPT_DIR%1.6\Assemblies"
set "SOURCE_DIR=%SCRIPT_DIR%1.6\Source"

if not exist "%LIBS_DIR%" mkdir "%LIBS_DIR%"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

echo Compiling from "%SOURCE_DIR%" to "%OUTPUT_DIR%"...
echo Libs used from "%LIBS_DIR%"...

"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:library /debug+ /out:"%OUTPUT_DIR%\GestaltEngineUnlimited.dll" ^
  /r:"%LIBS_DIR%\Assembly-CSharp.dll" ^
  /r:"%LIBS_DIR%\UnityEngine.CoreModule.dll" ^
  /r:"%LIBS_DIR%\UnityEngine.dll" ^
  /r:"%LIBS_DIR%\netstandard.dll" ^
  /r:"%LIBS_DIR%\Unity.Mathematics.dll" ^
  /r:"%LIBS_DIR%\UnityEngine.TextRenderingModule.dll" ^
  /r:"%LIBS_DIR%\UnityEngine.IMGUIModule.dll" ^
  /r:"%LIBS_DIR%\0Harmony.dll" ^
  /r:"%LIBS_DIR%\GestaltEngine.dll" ^
  /r:"%LIBS_DIR%\ReinforcedMechanoids.dll" ^
  /recurse:"%SOURCE_DIR%\*.cs"

if %errorlevel% neq 0 (
    echo Compilation FAILED!
    exit /b 1
)

echo Compilation SUCCESS!
exit /b 0
