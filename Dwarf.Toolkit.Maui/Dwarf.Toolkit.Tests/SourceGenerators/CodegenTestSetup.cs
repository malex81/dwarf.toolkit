using NUnit.Framework;

namespace Dwarf.Toolkit.Tests.SourceGenerators;

[SetUpFixture]
internal sealed class CodegenTestSetup
{
	[OneTimeSetUp]
	public void RunBeforeAllTests()
	{
		CodegenTestHelpers.ClearOutputDirectory();
	}
}