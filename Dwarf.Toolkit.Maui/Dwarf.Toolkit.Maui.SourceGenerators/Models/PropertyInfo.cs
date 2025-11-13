// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Dwarf.Toolkit.Maui.SourceGenerators.Constants;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Dwarf.Toolkit.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;

enum MethodExist { No, ExistPartial, ExistNoPartial }
sealed record ChangeMethodInfo(string Name, MethodExist Exist1, MethodExist Exist2);

/// <summary>
/// A model representing an generated property
/// </summary>
/// <param name="AnnotatedMemberKind">The syntax kind of the annotated member that triggered this property generation.</param>
/// <param name="TypeNameWithNullabilityAnnotations">The type name for the generated property, including nullability annotations.</param>
/// <param name="PropertyName">The generated property name.</param>
/// <param name="PropertyModifers">The list of additional modifiers for the property (they are <see cref="SyntaxKind"/> values).</param>
/// <param name="PropertyAccessibility">The accessibility of the property.</param>
internal sealed record PropertyInfo(
	SyntaxKind AnnotatedMemberKind,
	string TypeNameWithNullabilityAnnotations,
	string PropertyName,
	EquatableArray<ushort> PropertyModifers,
	Accessibility PropertyAccessibility,
	AttributeInfo BindableAttribute,
	ChangeMethodInfo ChangingMethodInfo,
	ChangeMethodInfo ChangedMethodInfo)
{

	public string? ValidateMethodName => BindableAttribute.GetNamedTextArgumentValue(BindableAttributeNaming.ValidateMethodArg);
	public string? CoerceMethodName => BindableAttribute.GetNamedTextArgumentValue(BindableAttributeNaming.CoerceMethodArg);
}
