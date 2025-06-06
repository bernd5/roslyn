﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Composition;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.VisualStudio.LanguageServices.Implementation.Progression;

namespace Microsoft.VisualStudio.LanguageServices.CSharp.Progression;

[ExportLanguageService(typeof(IProgressionLanguageService), LanguageNames.CSharp), Shared]
internal sealed partial class CSharpProgressionLanguageService : IProgressionLanguageService
{
    private static readonly SymbolDisplayFormat s_descriptionFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeContainingType,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeParamsRefOut |
                          SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat s_labelFormat = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType |
                          SymbolDisplayParameterOptions.IncludeParamsRefOut |
                          SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    [ImportingConstructor]
    [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
    public CSharpProgressionLanguageService()
    {
    }

    public string GetDescriptionForSymbol(ISymbol symbol, bool includeContainingSymbol)
        => GetSymbolText(symbol, includeContainingSymbol, s_descriptionFormat);

    public string GetLabelForSymbol(ISymbol symbol, bool includeContainingSymbol)
        => GetSymbolText(symbol, includeContainingSymbol, s_labelFormat);

    private static string GetSymbolText(ISymbol symbol, bool includeContainingSymbol, SymbolDisplayFormat displayFormat)
    {
        var label = symbol.ToDisplayString(displayFormat);

        var typeToShow = GetType(symbol);

        if (typeToShow != null)
        {
            label += " : " + typeToShow.ToDisplayString(s_labelFormat);
        }

        if (includeContainingSymbol && symbol.ContainingSymbol != null)
        {
            label += " (" + symbol.ContainingSymbol.ToDisplayString(s_labelFormat) + ")";
        }

        return label;
    }

    private static ITypeSymbol GetType(ISymbol symbol)
    {
        switch (symbol)
        {
            case IEventSymbol f: return f.Type;
            case IFieldSymbol f: return f.ContainingType.TypeKind == TypeKind.Enum ? null : f.Type;
            case IMethodSymbol m: return IncludeReturnType(m) ? m.ReturnType : null;
            case IPropertySymbol p: return p.Type;
            case INamedTypeSymbol n: return n.IsDelegateType() ? n.DelegateInvokeMethod.ReturnType : null;
            default: return null;
        }
    }

    private static bool IncludeReturnType(IMethodSymbol f)
        => f.MethodKind is MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation;
}
