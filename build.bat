ECHO OFF

SET OutputDirectory="%1
SET TempDirectory=".temp"

if "%1"=="" SET OutputDirectory=".output"

if exist %OutputDirectory% rmdir /S /Q %OutputDirectory%

ECHO Building project...
"%programfiles(x86)%\MSBuild\14.0\Bin\msbuild" "AzureDirectory\AzureDirectory.csproj" /t:Rebuild /p:Configuration=Release;OutputPath=..\\%TempDirectory%

ECHO Copying result to %OutputDirectory%...
xcopy %TempDirectory%\AzureDirectory.dll %OutputDirectory%\

rmdir /S /Q %TempDirectory%

