using Dwarf.Toolkit.Maui.SourceGenerators.Diagnostics.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace Dwarf.Toolkit.Tests.Analyzers;

using static CodegenTestHelpers;

[TestFixture]
internal sealed class InvalidProperyTest
{
	[TestCase]
	public async Task InvalidPartialPropertyAnalyzer_OnImplementedProperty_Warns()
	{
		const string source = """
			using Microsoft.Maui.Controls;
			using Dwarf.Toolkit.Maui;
			
			namespace ImplementedPropertyWarns
			{
				public partial class SampleViewModel : BindableObject
				{            
					[{|DTKM0010:BindableProperty|}]            
					public partial string Name { get; set; }

					public partial string Name
					{
						get => "something";
						set { }
					}
				}
			}
			""";

		await VerifyAnalyzerDiagnosticsAndSuccessfulGeneration<InvalidPartialPropertyBindablePropertyAttributeAnalyzer>(source);
	}
}