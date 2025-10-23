@set progName=Dwarf.Toolkit.Maui

dotnet pack "../%progName%/%progName%.csproj" -c Release
@rd /s /q "%UserProfile%/.nuget/packages/%progName%"

@pause