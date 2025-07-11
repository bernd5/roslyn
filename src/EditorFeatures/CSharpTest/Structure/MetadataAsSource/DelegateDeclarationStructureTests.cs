﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Structure;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Structure;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.Structure.MetadataAsSource;

[Trait(Traits.Feature, Traits.Features.MetadataAsSource)]
public sealed class DelegateDeclarationStructureTests : AbstractCSharpSyntaxNodeStructureTests<DelegateDeclarationSyntax>
{
    protected override string WorkspaceKind => CodeAnalysis.WorkspaceKind.MetadataAsSource;
    internal override AbstractSyntaxStructureProvider CreateProvider() => new DelegateDeclarationStructureProvider();

    [Fact]
    public Task NoCommentsOrAttributes()
        => VerifyNoBlockSpansAsync("""
                public delegate TResult $$Blah<in T, out TResult>(T arg);
                """);

    [Fact]
    public Task WithAttributes()
        => VerifyBlockSpansAsync("""
                {|hint:{|textspan:[Goo]
                |}public delegate TResult $$Blah<in T, out TResult>(T arg);|}
                """,
            Region("textspan", "hint", CSharpStructureHelpers.Ellipsis, autoCollapse: true));

    [Fact]
    public Task WithCommentsAndAttributes()
        => VerifyBlockSpansAsync("""
                {|hint:{|textspan:// Summary:
                //     This is a summary.
                [Goo]
                |}delegate TResult $$Blah<in T, out TResult>(T arg);|}
                """,
            Region("textspan", "hint", CSharpStructureHelpers.Ellipsis, autoCollapse: true));

    [Fact]
    public Task WithCommentsAttributesAndModifiers()
        => VerifyBlockSpansAsync("""
                {|hint:{|textspan:// Summary:
                //     This is a summary.
                [Goo]
                |}public delegate TResult $$Blah<in T, out TResult>(T arg);|}
                """,
            Region("textspan", "hint", CSharpStructureHelpers.Ellipsis, autoCollapse: true));
}
