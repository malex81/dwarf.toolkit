namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;

enum MethodExist { No, ExistPartial, ExistNoPartial }

sealed record ChangeMethodInfo(string Name, MethodExist Exist1, MethodExist Exist2);