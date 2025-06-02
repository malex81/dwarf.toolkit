@set progName=Dwarf.Toolkit.Maui

dotnet pack "../%progName%/%progName%.csproj" -c Release
@rd /S /Q "%UserProfile%/.nuget/packages/%progName%" /S

@pause