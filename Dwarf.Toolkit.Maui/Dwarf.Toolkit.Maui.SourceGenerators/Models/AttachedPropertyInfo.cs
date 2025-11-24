// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Dwarf.Toolkit.Maui.SourceGenerators.Constants;
using Dwarf.Toolkit.SourceGenerators.Helpers;
using Dwarf.Toolkit.SourceGenerators.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dwarf.Toolkit.Maui.SourceGenerators.Models;


/// <summary>
/// A model representing an generated property
/// </summary>
/// <param name="TypeNameWithNullabilityAnnotations">The type name for the generated property, including nullability annotations.</param>
/// <param name="PropertyName">The generated property name.</param>
/// <param name="PropertyModifers">The list of additional modifiers for the property (they are <see cref="SyntaxKind"/> values).</param>
/// <param name="GetMethodAccessibility">The accessibility of the property.</param>
internal sealed record AttachedPropertyInfo(
	string TypeNameWithNullabilityAnnotations,
	string RealTypeName,
	string TargetTypeName,
	string PropertyName,
	EquatableArray<ushort> GetMethodModifers,
	Accessibility GetMethodAccessibility,
	AttributeInfo BindableAttribute,
	ChangeMethodInfo ChangingMethodInfo,
	ChangeMethodInfo ChangedMethodInfo,
	bool NeedGeneratePartialValidation,
	bool NeedGeneratePartialCoerce)
{

	public string? ValidateMethodName => BindableAttribute.GetNamedTextArgumentValue(BindableAttributeNaming.ValidateMethodArg);
	public string? CoerceMethodName => BindableAttribute.GetNamedTextArgumentValue(BindableAttributeNaming.CoerceMethodArg);
	public string Srv_PropertyChanging => string.Format(ServiceMembers.ChangingMethodFormat, PropertyName);
	public string Srv_PropertyChanged => string.Format(ServiceMembers.ChangedMethodFormat, PropertyName);
	public string Srv_ValidateValue => string.Format(ServiceMembers.ValidateMethodFormat, PropertyName);
	public string Srv_CoerceValue => string.Format(ServiceMembers.CoerceMethodFormat, PropertyName);
}
