using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Dwarf.Toolkit.SourceGenerators.Extensions;

internal static class ExpressionSyntaxExtensions
{
	static string TrimStart(this string src, string start) => src.StartsWith(start) ? src[start.Length..] : src;
	static string TrimGlobalPrefix(this string src) => src.TrimStart("global::");

	public static ExpressionSyntax CastIfNeed(this ExpressionSyntax expression, string needType, string hasType)
		=> needType.TrimGlobalPrefix() == hasType.TrimGlobalPrefix() ? expression : CastExpression(IdentifierName(needType), expression);
}