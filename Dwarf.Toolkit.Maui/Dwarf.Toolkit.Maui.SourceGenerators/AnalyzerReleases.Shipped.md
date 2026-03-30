; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 0.1.1

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DTKM0001 | BindablePropertyGenerator | Warning | `DefaultValueExprassion used with DefaultValue`
DTKM0002 | BindablePropertyGenerator | Error | `Invalid method notation used with [AttachedProperty] attribute`
DTKM0010 | BindablePropertyGenerator | Error | `Invalid property declaration (not incomplete partial definition)`
DTKM0011 | BindablePropertyGenerator | Error | `Using [BindableProperty] on a property that returns byref`
DTKM0012 | BindablePropertyGenerator | Error | `Using [BindableProperty] on a property that returns byref-like`
