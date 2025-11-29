using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dwarf.Toolkit.SourceGenerators.Extensions;

internal static class ExpressionSyntaxExtensions
{
	public static ExpressionSyntax CastIfNeed(this ExpressionSyntax expression, string needType, string hasType)
		=> needType == hasType ? expression : CastExpression(IdentifierName(needType), expression);
}