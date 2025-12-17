@echo off
setlocal EnableDelayedExpansion

set "projects="
set projects=%projects% ./Dwarf.Toolkit.Maui/Dwarf.Toolkit.Maui/Dwarf.Toolkit.Maui.csproj
set projects=%projects% ./Dwarf.Toolkit.Basic/Dwarf.Toolkit.Basic.csproj

for %%p in (%projects%) do (
	echo .
	echo .
	echo +++ %%p +++
	dotnet pack "%%p" -c Release
    if !ERRORLEVEL! neq 0 (
		rem echo Error: %%p
		pause
		exit /b 1
    )
	
	set "fileName=%%~nxp"
	set "projName=!fileName:.csproj=!"
	echo Remove from NuGet cache: !projName!
	rd /s /q "%UserProfile%\.nuget\packages\!projName!"
)

pause