﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.ConvertNamespace;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.ConvertNamespace;

using VerifyCS = CSharpCodeFixVerifier<ConvertToBlockScopedNamespaceDiagnosticAnalyzer, ConvertNamespaceCodeFixProvider>;

public sealed class ConvertToBlockScopedNamespaceAnalyzerTests
{
    public static IEnumerable<object[]> EndOfDocumentSequences
    {
        get
        {
            yield return new object[] { "" };
            yield return new object[] { "\r\n" };
        }
    }

    #region Convert To Block Scoped

    [Fact]
    public Task TestConvertToBlockScopedInCSharp9()
        => new VerifyCS.Test
        {
            TestCode = """
            [|{|CS8773:namespace|} N;|]
            """,
            FixedCode = """
            namespace N
            {
            }
            """,
            LanguageVersion = LanguageVersion.CSharp9,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedInCSharp9_NotSilent()
        => new VerifyCS.Test
        {
            TestCode = """
            {|CS8773:namespace|} [|N|];
            """,
            FixedCode = """
            namespace N
            {
            }
            """,
            LanguageVersion = LanguageVersion.CSharp9,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped, NotificationOption2.Suggestion }
            }
        }.RunAsync();

    [Fact]
    public Task TestNoConvertToBlockScopedInCSharp10WithBlockScopedPreference()
        => new VerifyCS.Test
        {
            TestCode = """
            namespace N {}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockScopedInCSharp10WithFileScopedPreference(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            [|namespace N;|]{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockScopedInCSharp10WithDirectives1(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            [|namespace N;|]

            #if true
            #endif{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
            #if true
            #endif
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockScopedInCSharp10WithDirectives2(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            [|namespace N;|]

            #region Text
            #endregion{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                #region Text
                #endregion
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithMultipleNamespaces(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            [|namespace N;|]

            namespace {|CS8955:N2|}
            {
            }{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace N2
                {
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithNestedNamespaces1(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            [|namespace N;|]

            [|namespace {|CS8954:N2|};|]{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace N2
                {
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            NumberOfFixAllIterations = 2,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithNestedNamespaces2(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            namespace N
            {
                [|namespace {|CS8955:N2|};|]
            }{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace $$N2
                {
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithNestedNamespaces3(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            namespace N
            {
                [|namespace {|CS8955:N2|};|]
            }{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace $$N2 {
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped },
                { CSharpFormattingOptions2.NewLineBeforeOpenBrace, CSharpFormattingOptions2.NewLineBeforeOpenBrace.DefaultValue & ~NewLineBeforeOpenBracePlacement.Types },
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithNestedNamespaces4(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            namespace N
            {
                [|namespace {|CS8955:N2|};|]

            #if true
            #endif
            }{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace $$N2
                {
            #if true
            #endif
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Theory]
    [MemberData(nameof(EndOfDocumentSequences))]
    public Task TestConvertToBlockWithNestedNamespaces5(string endOfDocumentSequence)
        => new VerifyCS.Test
        {
            TestCode = $$"""
            namespace N
            {
                [|namespace {|CS8955:N2|};|]

                #region Text
                #endregion
            }{{endOfDocumentSequence}}
            """,
            FixedCode = $$"""
            namespace N
            {
                namespace $$N2
                {
                    #region Text
                    #endregion
                }
            }{{endOfDocumentSequence}}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockWithTopLevelStatement1()
        => new VerifyCS.Test
        {
            TestCode = """
            {|CS8805:int i = 0;|}

            [|namespace {|CS8956:N|};|]
            """,
            FixedCode = """
            {|CS8805:int i = 0;|}

            namespace N
            {
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockWithTopLevelStatement2()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|]

            int {|CS0116:i|} = 0;
            """,
            FixedCode = """
            namespace N
            {
                int {|CS0116:i|} = 0;
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithUsing1()
        => new VerifyCS.Test
        {
            TestCode = """
            using System;

            [|namespace N;|]
            """,
            FixedCode = """
            using System;

            namespace N
            {
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithUsing2()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|]

            using System;
            """,
            FixedCode = """
            namespace $$N
            {
                using System;
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithClass()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|]

            class C
            {
            }
            """,
            FixedCode = """
            namespace $$N
            {
                class C
                {
                }
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithClassWithDocComment()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|]

            /// <summary/>
            class C
            {
            }
            """,
            FixedCode = """
            namespace N
            {
                /// <summary/>
                class C
                {
                }
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithMissingCloseBrace()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|]

            /// <summary/>
            class C
            {{|CS1513:|}
            """,
            FixedCode = """
            namespace N
            {
                /// <summary/>
                class C
                {
            }{|CS1513:|}
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            CodeActionValidationMode = CodeActionValidationMode.None,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithCommentOnSemicolon()
        => new VerifyCS.Test
        {
            TestCode = """
            [|namespace N;|] // comment

            class C
            {
            }
            """,
            FixedCode = """
            namespace N
            { // comment
                class C
                {
                }
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    [Fact]
    public Task TestConvertToBlockScopedWithLeadingComment()
        => new VerifyCS.Test
        {
            TestCode = """
            // copyright
            [|namespace N;|]

            class C
            {
            }
            """,
            FixedCode = """
            // copyright
            namespace N
            {
                class C
                {
                }
            }
            """,
            LanguageVersion = LanguageVersion.CSharp10,
            Options =
            {
                { CSharpCodeStyleOptions.NamespaceDeclarations, NamespaceDeclarationPreference.BlockScoped }
            }
        }.RunAsync();

    #endregion
}
