﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.EditAndContinue;

[DataContract]
internal readonly struct RudeEditDiagnostic
{
    [DataMember(Order = 0)]
    public readonly RudeEditKind Kind;

    /// <summary>
    /// Span in the new document. May be <c>default</c> if the document (or its entire content) has been deleted.
    /// </summary>
    [DataMember(Order = 1)]
    public readonly TextSpan Span;

    [DataMember(Order = 2)]
    public readonly ushort SyntaxKind;

    [DataMember(Order = 3)]
    public readonly string?[] Arguments;

    internal RudeEditDiagnostic(RudeEditKind kind, TextSpan span, ushort syntaxKind, string?[] arguments)
    {
        Kind = kind;
        Span = span;
        SyntaxKind = syntaxKind;
        Arguments = arguments;
    }

    internal RudeEditDiagnostic(RudeEditKind kind, TextSpan span, SyntaxNode? node = null, string?[]? arguments = null)
        : this(kind, span, (ushort)(node != null ? node.RawKind : 0), arguments ?? [])
    {
    }

    internal Diagnostic ToDiagnostic(SyntaxTree? tree)
    {
        var descriptor = EditAndContinueDiagnosticDescriptors.GetDescriptor(Kind);
        return Diagnostic.Create(descriptor, tree?.GetLocation(Span) ?? Location.None, Arguments);
    }
}

internal static class RudeEditExtensions
{
    internal static DiagnosticSeverity GetSeverity(this RudeEditKind kind)
        => EditAndContinueDiagnosticDescriptors.GetDescriptor(kind).DefaultSeverity;

    internal static bool IsBlocking(this RudeEditKind kind)
        => kind.GetSeverity() == DiagnosticSeverity.Error;

    internal static bool IsBlockingRudeEdit(this Diagnostic diagnostic)
        => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error;

    internal static bool IsNoEffectRudeEdit(this Diagnostic diagnostic)
        => EditAndContinueDiagnosticDescriptors.GetRudeEditKind(diagnostic.Id) == RudeEditKind.UpdateMightNotHaveAnyEffect;

    public static bool HasBlockingRudeEdits(this ImmutableArray<Diagnostic> diagnostics)
        => diagnostics.Any(IsBlockingRudeEdit);

    public static bool HasNoEffectRudeEdits(this ImmutableArray<Diagnostic> diagnostics)
        => diagnostics.Any(IsNoEffectRudeEdit);

    public static bool HasBlockingRudeEdits(this ImmutableArray<RudeEditDiagnostic> diagnostics)
        => diagnostics.Any(static e => e.Kind.IsBlocking());
}
