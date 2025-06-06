﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    using Microsoft.CodeAnalysis.Syntax.InternalSyntax;

    internal sealed class DirectiveParser : SyntaxParser
    {
        private const int MAX_DIRECTIVE_IDENTIFIER_WIDTH = 128;

        private DirectiveStack _context;

        internal DirectiveParser(Lexer lexer)
            : base(lexer, LexerMode.Directive, oldTree: null, changes: null, allowModeReset: false)
        {
        }

        public void ReInitialize(DirectiveStack context)
        {
            base.ReInitialize();
            _context = context;
        }

        public CSharpSyntaxNode ParseDirective(
            bool isActive,
            bool endIsActive,
            bool isFollowingToken)
        {
            var hashPosition = lexer.TextWindow.Position;
            var hash = this.EatToken(SyntaxKind.HashToken, false);

            // The behavior of these directives when isActive is false is somewhat complicated.
            // The key functions in the native compiler are ScanPreprocessorIfSection and
            // ScanAndIgnoreDirective in CSourceData::CPreprocessor.
            // Key points:
            //   1) #error, #warning, #line, and #pragma have no effect and produce no diagnostics.
            //   2) #if, #else, #elif, #endif, #region, and #endregion must still nest correctly.
            //   3) #define and #undef produce diagnostics but have no effect.
            // #reference, #load and #! are new, but they do not require nesting behavior, so we'll
            // ignore their diagnostics (as in (1) above).

            CSharpSyntaxNode result;
            SyntaxKind contextualKind = this.CurrentToken.ContextualKind;
            switch (contextualKind)
            {
                case SyntaxKind.IfKeyword:
                    result = this.ParseIfDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                case SyntaxKind.ElifKeyword:
                    result = this.ParseElifDirective(hash, this.EatContextualToken(contextualKind), isActive, endIsActive);
                    break;

                case SyntaxKind.ElseKeyword:
                    result = this.ParseElseDirective(hash, this.EatContextualToken(contextualKind), isActive, endIsActive);
                    break;

                case SyntaxKind.EndIfKeyword:
                    result = this.ParseEndIfDirective(hash, this.EatContextualToken(contextualKind), isActive, endIsActive);
                    break;

                case SyntaxKind.RegionKeyword:
                    result = this.ParseRegionDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                case SyntaxKind.EndRegionKeyword:
                    result = this.ParseEndRegionDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                case SyntaxKind.DefineKeyword:
                case SyntaxKind.UndefKeyword:
                    result = this.ParseDefineOrUndefDirective(hash, this.EatContextualToken(contextualKind), isActive, isFollowingToken);
                    break;

                case SyntaxKind.ErrorKeyword:
                case SyntaxKind.WarningKeyword:
                    result = this.ParseErrorOrWarningDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                case SyntaxKind.LineKeyword:
                    var lineKeyword = this.EatContextualToken(contextualKind);
                    result = (this.CurrentToken.Kind == SyntaxKind.OpenParenToken) ?
                        this.ParseLineSpanDirective(hash, lineKeyword, isActive) :
                        this.ParseLineDirective(hash, lineKeyword, isActive);
                    break;

                case SyntaxKind.PragmaKeyword:
                    result = this.ParsePragmaDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                case SyntaxKind.ReferenceKeyword:
                    result = this.ParseReferenceDirective(hash, this.EatContextualToken(contextualKind), isActive, isFollowingToken);
                    break;

                case SyntaxKind.LoadKeyword:
                    result = this.ParseLoadDirective(hash, this.EatContextualToken(contextualKind), isActive, isFollowingToken);
                    break;

                case SyntaxKind.NullableKeyword:
                    result = this.ParseNullableDirective(hash, this.EatContextualToken(contextualKind), isActive);
                    break;

                default:
                    if (contextualKind == SyntaxKind.ExclamationToken)
                    {
                        // Always parse as a shebang directive, but report an error if not at position 0
                        if (hashPosition != 0 || hash.HasTrailingTrivia)
                        {
                            hash = this.AddError(hash, ErrorCode.ERR_BadDirectivePlacement);
                        }

                        result = this.ParseShebangDirective(hash, this.EatToken(SyntaxKind.ExclamationToken), isActive);
                    }
                    else if (contextualKind == SyntaxKind.ColonToken && !hash.HasTrailingTrivia)
                    {
                        result = this.ParseIgnoredDirective(hash, this.EatToken(SyntaxKind.ColonToken), isActive, isFollowingToken);
                    }
                    else
                    {
                        var id = this.EatToken(SyntaxKind.IdentifierToken, false);
                        var end = this.ParseEndOfDirective(ignoreErrors: true);
                        if (!id.IsMissing)
                        {
                            id = this.AddError(id, ErrorCode.ERR_PPDirectiveExpected);
                        }
                        else
                        {
                            hash = this.AddError(hash, ErrorCode.ERR_PPDirectiveExpected);
                        }

                        result = SyntaxFactory.BadDirectiveTrivia(hash, id, end, isActive);
                    }

                    break;
            }

            return result;
        }

        private DirectiveTriviaSyntax ParseIfDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive)
        {
            var expr = this.ParseExpression();
            var eod = this.ParseEndOfDirective(ignoreErrors: false);
            var isTrue = this.EvaluateBool(expr);
            var branchTaken = isActive && isTrue;
            return SyntaxFactory.IfDirectiveTrivia(hash, keyword, expr, eod, isActive, branchTaken, isTrue);
        }

        private DirectiveTriviaSyntax ParseElifDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive)
        {
            var expr = this.ParseExpression();
            var eod = this.ParseEndOfDirective(ignoreErrors: false);
            if (_context.HasPreviousIfOrElif())
            {
                var isTrue = this.EvaluateBool(expr);
                var branchTaken = endIsActive && isTrue && !_context.PreviousBranchTaken();
                return SyntaxFactory.ElifDirectiveTrivia(hash, keyword, expr, eod, endIsActive, branchTaken, isTrue);
            }
            else
            {
                eod = eod.TokenWithLeadingTrivia(SyntaxList.Concat(SyntaxFactory.DisabledText(expr.ToFullString()), eod.GetLeadingTrivia()));
                if (_context.HasUnfinishedRegion())
                {
                    return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndRegionDirectiveExpected);
                }
                else if (_context.HasUnfinishedIf())
                {
                    return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndifDirectiveExpected);
                }
                else
                {
                    return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_UnexpectedDirective);
                }
            }
        }

        private DirectiveTriviaSyntax ParseElseDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive)
        {
            var eod = this.ParseEndOfDirective(ignoreErrors: false);
            if (_context.HasPreviousIfOrElif())
            {
                var branchTaken = endIsActive && !_context.PreviousBranchTaken();
                return SyntaxFactory.ElseDirectiveTrivia(hash, keyword, eod, endIsActive, branchTaken);
            }
            else if (_context.HasUnfinishedRegion())
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndRegionDirectiveExpected);
            }
            else if (_context.HasUnfinishedIf())
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndifDirectiveExpected);
            }
            else
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_UnexpectedDirective);
            }
        }

        private DirectiveTriviaSyntax ParseEndIfDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool endIsActive)
        {
            var eod = this.ParseEndOfDirective(ignoreErrors: false);
            if (_context.HasUnfinishedIf())
            {
                return SyntaxFactory.EndIfDirectiveTrivia(hash, keyword, eod, endIsActive);
            }
            else if (_context.HasUnfinishedRegion())
            {
                // CONSIDER: dev10 actually pops the region off the directive stack here.
                // See if (tok == PPT_ENDIF) in CSourceData::CPreprocessor::ScanPreprocessorIfSection.
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndRegionDirectiveExpected);
            }
            else
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_UnexpectedDirective);
            }
        }

        private DirectiveTriviaSyntax ParseRegionDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive)
        {
            return SyntaxFactory.RegionDirectiveTrivia(hash, keyword, this.ParseEndOfDirectiveWithOptionalPreprocessingMessage(), isActive);
        }

        private DirectiveTriviaSyntax ParseEndRegionDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive)
        {
            var eod = this.ParseEndOfDirectiveWithOptionalPreprocessingMessage();
            if (_context.HasUnfinishedRegion())
            {
                return SyntaxFactory.EndRegionDirectiveTrivia(hash, keyword, eod, isActive);
            }
            else if (_context.HasUnfinishedIf())
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_EndifDirectiveExpected);
            }
            else
            {
                return this.AddError(SyntaxFactory.BadDirectiveTrivia(hash, keyword, eod, isActive), ErrorCode.ERR_UnexpectedDirective);
            }
        }

        private DirectiveTriviaSyntax ParseDefineOrUndefDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool isFollowingToken)
        {
            if (isFollowingToken)
            {
                keyword = this.AddError(keyword, ErrorCode.ERR_PPDefFollowsToken);
            }

            var name = this.EatToken(SyntaxKind.IdentifierToken, ErrorCode.ERR_IdentifierExpected);
            name = TruncateIdentifier(name);
            var end = this.ParseEndOfDirective(ignoreErrors: name.IsMissing);
            if (keyword.Kind == SyntaxKind.DefineKeyword)
            {
                return SyntaxFactory.DefineDirectiveTrivia(hash, keyword, name, end, isActive);
            }
            else
            {
                return SyntaxFactory.UndefDirectiveTrivia(hash, keyword, name, end, isActive);
            }
        }

        /// <summary>
        /// An error/warning directive tells the compiler to indicate a syntactic error/warning
        /// at the current location.
        /// 
        /// Format: #error Error message string
        /// Resulting message: from the first non-whitespace character after the directive
        /// keyword until the end of the directive (aka EOD) at the line break or EOF.
        /// Resulting span: [first non-whitespace char, EOD)
        /// 
        /// Examples (pipes indicate span):
        /// #error |goo|
        /// #error  |goo|
        /// #error |goo |
        /// #error |goo baz|
        /// #error |//goo|
        /// #error |/*goo*/|
        /// #error |/*goo|
        /// </summary>
        /// <param name="hash">The '#' token.</param>
        /// <param name="keyword">The 'error' or 'warning' token.</param>
        /// <param name="isActive">True if the error/warning should be recorded.</param>
        /// <returns>An ErrorDirective or WarningDirective node.</returns>
        private DirectiveTriviaSyntax ParseErrorOrWarningDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive)
        {
            var eod = this.ParseEndOfDirectiveWithOptionalPreprocessingMessage();
            bool isError = keyword.Kind == SyntaxKind.ErrorKeyword;
            if (isActive)
            {
                var triviaBuilder = new System.IO.StringWriter(System.Globalization.CultureInfo.InvariantCulture);
                int triviaWidth = 0;

                // whitespace and single line comments are trailing trivia on the keyword, the rest
                // of the error message is leading trivia on the eod.
                //
                bool skipping = true;
                foreach (var t in keyword.TrailingTrivia)
                {
                    if (skipping)
                    {
                        if (t.Kind == SyntaxKind.WhitespaceTrivia)
                        {
                            continue;
                        }

                        skipping = false;
                    }

                    t.WriteTo(triviaBuilder, leading: true, trailing: true);
                    triviaWidth += t.FullWidth;
                }

                foreach (var node in eod.LeadingTrivia)
                {
                    node.WriteTo(triviaBuilder, leading: true, trailing: true);
                    triviaWidth += node.FullWidth;
                }

                //relative to leading trivia of eod
                //could be negative if part of the error text comes from the trailing trivia of the keyword token
                int triviaOffset = eod.GetLeadingTriviaWidth() - triviaWidth;

                string errorText = triviaBuilder.ToString();
                eod = this.AddError(eod, triviaOffset, triviaWidth, isError ? ErrorCode.ERR_ErrorDirective : ErrorCode.WRN_WarningDirective, errorText);

                if (isError)
                {
                    if (errorText.Equals("version", StringComparison.Ordinal))
                    {
                        string version = CommonCompiler.GetProductVersion(typeof(CSharpCompiler));
                        var specified = this.Options.SpecifiedLanguageVersion;
                        var effective = specified.MapSpecifiedToEffectiveVersion();

                        var displayLanguageVersion = specified == effective ? specified.ToDisplayString() : $"{specified.ToDisplayString()} ({effective.ToDisplayString()})";

                        eod = this.AddError(eod, triviaOffset, triviaWidth, ErrorCode.ERR_CompilerAndLanguageVersion, version,
                            displayLanguageVersion);
                    }
                    else
                    {
                        const string versionMarker = "version:";
                        if (this.Options.LanguageVersion != LanguageVersion.Preview &&
                            errorText.StartsWith(versionMarker, StringComparison.Ordinal) &&
                            LanguageVersionFacts.TryParse(errorText.Substring(versionMarker.Length), out var languageVersion))
                        {
                            ErrorCode error = this.Options.LanguageVersion.GetErrorCode();
                            eod = this.AddError(eod, triviaOffset, triviaWidth, error, "version", new CSharpRequiredLanguageVersion(languageVersion));
                        }
                    }
                }
            }

            if (isError)
            {
                return SyntaxFactory.ErrorDirectiveTrivia(hash, keyword, eod, isActive);
            }
            else
            {
                return SyntaxFactory.WarningDirectiveTrivia(hash, keyword, eod, isActive);
            }
        }

        private const int MaxLineValue = 0xfeefed;
        private const int MaxCharacterValue = 0x10000;

        private DirectiveTriviaSyntax ParseLineDirective(SyntaxToken hash, SyntaxToken id, bool isActive)
        {
            SyntaxToken line;
            SyntaxToken file = null;
            bool sawLineButNotFile = false;
            switch (this.CurrentToken.Kind)
            {
                case SyntaxKind.DefaultKeyword:
                case SyntaxKind.HiddenKeyword:
                    line = this.EatToken();
                    break;
                default:
                    line = this.EatToken(SyntaxKind.NumericLiteralToken, ErrorCode.ERR_InvalidLineNumber, reportError: isActive);
                    sawLineButNotFile = true; //assume this is the case until we (potentially) see the file name below
                    if (isActive && !line.IsMissing && line.Kind == SyntaxKind.NumericLiteralToken)
                    {
                        if ((int)line.Value < 1)
                        {
                            line = this.AddError(line, ErrorCode.ERR_InvalidLineNumber);
                        }
                        else if ((int)line.Value > MaxLineValue)
                        {
                            line = this.AddError(line, ErrorCode.WRN_TooManyLinesForDebugger);
                        }
                    }

                    if (this.CurrentToken.Kind == SyntaxKind.StringLiteralToken &&
                        (line.IsMissing || line.GetTrailingTriviaWidth() > 0 || this.CurrentToken.GetLeadingTriviaWidth() > 0)) //require separation between line number and file name
                    {
                        file = this.EatToken();
                        sawLineButNotFile = false;
                    }

                    break;
            }

            var end = this.ParseEndOfDirective(ignoreErrors: line.IsMissing || !isActive, afterLineNumber: sawLineButNotFile);
            return SyntaxFactory.LineDirectiveTrivia(hash, id, line, file, end, isActive);
        }

        private LineSpanDirectiveTriviaSyntax ParseLineSpanDirective(SyntaxToken hash, SyntaxToken lineKeyword, bool isActive)
        {
            Debug.Assert(CurrentToken.Kind == SyntaxKind.OpenParenToken);

            bool reportError = isActive;
            var start = ParseLineDirectivePosition(ref reportError, out int startLine, out int startCharacter);
            if (noTriviaBetween(lineKeyword, start.GetFirstToken()))
            {
                start = this.AddError(start, ErrorCode.ERR_LineSpanDirectiveRequiresSpace);
            }

            var minus = EatToken(SyntaxKind.MinusToken, reportError: reportError);
            if (minus.IsMissing) reportError = false;

            var end = ParseLineDirectivePosition(ref reportError, out int endLine, out int endCharacter);
            if (reportError &&
                (endLine < startLine || (endLine == startLine && endCharacter < startCharacter)))
            {
                end = this.AddError(end, ErrorCode.ERR_LineSpanDirectiveEndLessThanStart);
            }

            var characterOffset = (CurrentToken.Kind == SyntaxKind.NumericLiteralToken) ?
                ParseLineDirectiveNumericLiteral(ref reportError, minValue: 1, maxValue: MaxCharacterValue, out _) :
                null;

            if (noTriviaBetween(end.GetLastToken(), characterOffset))
            {
                characterOffset = this.AddError(characterOffset, ErrorCode.ERR_LineSpanDirectiveRequiresSpace);
            }

            var file = EatToken(SyntaxKind.StringLiteralToken, ErrorCode.ERR_MissingPPFile, reportError: reportError);
            if (file.IsMissing) reportError = false;

            if (noTriviaBetween(characterOffset ?? end.GetLastToken(), file))
            {
                file = this.AddError(file, ErrorCode.ERR_LineSpanDirectiveRequiresSpace);
            }

            var endOfDirective = this.ParseEndOfDirective(ignoreErrors: !reportError);
            return SyntaxFactory.LineSpanDirectiveTrivia(hash, lineKeyword, start, minus, end, characterOffset, file, endOfDirective, isActive);

            static bool noTriviaBetween(SyntaxToken token1, SyntaxToken token2)
            {
                return token1 is { IsMissing: false }
                    && token2 is { IsMissing: false }
                    && LanguageParser.NoTriviaBetween(token1, token2);
            }
        }

        private LineDirectivePositionSyntax ParseLineDirectivePosition(ref bool reportError, out int line, out int character)
        {
            var openParen = EatToken(SyntaxKind.OpenParenToken, reportError);
            if (openParen.IsMissing) reportError = false;

            var lineToken = ParseLineDirectiveNumericLiteral(ref reportError, minValue: 1, maxValue: MaxLineValue, out line);

            var comma = EatToken(SyntaxKind.CommaToken, reportError);
            if (comma.IsMissing) reportError = false;

            var characterToken = ParseLineDirectiveNumericLiteral(ref reportError, minValue: 1, maxValue: MaxCharacterValue, out character);

            var closeParen = EatToken(SyntaxKind.CloseParenToken, reportError);
            if (closeParen.IsMissing) reportError = false;

            return SyntaxFactory.LineDirectivePosition(openParen, lineToken, comma, characterToken, closeParen);
        }

        private SyntaxToken ParseLineDirectiveNumericLiteral(ref bool reportError, int minValue, int maxValue, out int value)
        {
            var token = this.EatToken(SyntaxKind.NumericLiteralToken, ErrorCode.ERR_LineSpanDirectiveInvalidValue, reportError: reportError);
            value = 0;
            if (token.IsMissing)
            {
                reportError = false;
            }
            else if (token.Kind == SyntaxKind.NumericLiteralToken)
            {
                value = (int)token.Value;
                if (value < minValue || value > maxValue)
                {
                    token = this.AddError(token, ErrorCode.ERR_LineSpanDirectiveInvalidValue);
                    reportError = false;
                }
            }
            return token;
        }

        private DirectiveTriviaSyntax ParseReferenceDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool isFollowingToken)
        {
            if (isActive)
            {
                if (Options.Kind == SourceCodeKind.Regular)
                {
                    keyword = this.AddError(keyword, ErrorCode.ERR_ReferenceDirectiveOnlyAllowedInScripts);
                }
                else if (isFollowingToken)
                {
                    keyword = this.AddError(keyword, ErrorCode.ERR_PPReferenceFollowsToken);
                }
            }

            SyntaxToken file = this.EatToken(SyntaxKind.StringLiteralToken, ErrorCode.ERR_ExpectedPPFile, reportError: isActive);

            var end = this.ParseEndOfDirective(ignoreErrors: file.IsMissing || !isActive);
            return SyntaxFactory.ReferenceDirectiveTrivia(hash, keyword, file, end, isActive);
        }

        private DirectiveTriviaSyntax ParseLoadDirective(SyntaxToken hash, SyntaxToken keyword, bool isActive, bool isFollowingToken)
        {
            if (isActive)
            {
                if (Options.Kind == SourceCodeKind.Regular)
                {
                    keyword = this.AddError(keyword, ErrorCode.ERR_LoadDirectiveOnlyAllowedInScripts);
                }
                else if (isFollowingToken)
                {
                    keyword = this.AddError(keyword, ErrorCode.ERR_PPLoadFollowsToken);
                }
            }

            SyntaxToken file = this.EatToken(SyntaxKind.StringLiteralToken, ErrorCode.ERR_ExpectedPPFile, reportError: isActive);

            var end = this.ParseEndOfDirective(ignoreErrors: file.IsMissing || !isActive);
            return SyntaxFactory.LoadDirectiveTrivia(hash, keyword, file, end, isActive);
        }

        private DirectiveTriviaSyntax ParseNullableDirective(SyntaxToken hash, SyntaxToken token, bool isActive)
        {
            if (isActive)
            {
                token = CheckFeatureAvailability(token, MessageID.IDS_FeatureNullableReferenceTypes);
            }

            SyntaxToken setting = this.CurrentToken.Kind switch
            {
                SyntaxKind.EnableKeyword => EatToken(),
                SyntaxKind.DisableKeyword => EatToken(),
                SyntaxKind.RestoreKeyword => EatToken(),
                _ => EatToken(SyntaxKind.DisableKeyword, ErrorCode.ERR_NullableDirectiveQualifierExpected, reportError: isActive)
            };

            SyntaxToken target = this.CurrentToken.Kind switch
            {
                SyntaxKind.WarningsKeyword => EatToken(),
                SyntaxKind.AnnotationsKeyword => EatToken(),
                SyntaxKind.EndOfDirectiveToken => null,
                SyntaxKind.EndOfFileToken => null,
                _ => EatToken(SyntaxKind.WarningsKeyword, ErrorCode.ERR_NullableDirectiveTargetExpected, reportError: !setting.IsMissing && isActive)
            };

            var end = this.ParseEndOfDirective(ignoreErrors: setting.IsMissing || target?.IsMissing == true || !isActive);
            return SyntaxFactory.NullableDirectiveTrivia(hash, token, setting, target, end, isActive);
        }

        private DirectiveTriviaSyntax ParsePragmaDirective(SyntaxToken hash, SyntaxToken pragma, bool isActive)
        {
            if (isActive)
            {
                pragma = CheckFeatureAvailability(pragma, MessageID.IDS_FeaturePragma);
            }

            bool hasError = false;
            if (this.CurrentToken.ContextualKind == SyntaxKind.WarningKeyword)
            {
                var warning = this.EatContextualToken(SyntaxKind.WarningKeyword);
                SyntaxToken style;
                if (this.CurrentToken.Kind == SyntaxKind.DisableKeyword || this.CurrentToken.Kind == SyntaxKind.RestoreKeyword)
                {
                    style = this.EatToken();

                    var ids = new SeparatedSyntaxListBuilder<ExpressionSyntax>(10);
                    while (this.CurrentToken.Kind != SyntaxKind.EndOfDirectiveToken)
                    {
                        SyntaxToken id;
                        ExpressionSyntax idExpression;

                        if (this.CurrentToken.Kind == SyntaxKind.NumericLiteralToken)
                        {
                            // Previous versions of the compiler used to report a warning (CS1691)
                            // whenever an unrecognized warning code was supplied in a #pragma directive
                            // (or via /nowarn /warnaserror flags on the command line).
                            // Going forward, we won't generate any warning in such cases. This will make
                            // maintenance of backwards compatibility easier (we no longer need to worry
                            // about breaking existing projects / command lines if we deprecate / remove
                            // an old warning code).
                            id = this.EatToken();
                            idExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, id);
                        }
                        else if (this.CurrentToken.Kind == SyntaxKind.IdentifierToken)
                        {
                            // Lexing / parsing of identifiers inside #pragma warning directives is identical
                            // to that inside #define directives except that very long identifiers inside #define
                            // are truncated to 128 characters to maintain backwards compatibility with previous
                            // versions of the compiler. (See TruncateIdentifier() below.)
                            // Since support for identifiers inside #pragma warning directives is new, 
                            // we don't have any backwards compatibility constraints. So we can preserve the
                            // identifier exactly as it appears in source.
                            id = this.EatToken();
                            idExpression = SyntaxFactory.IdentifierName(id);
                        }
                        else
                        {
                            id = this.EatToken(SyntaxKind.NumericLiteralToken, ErrorCode.WRN_IdentifierOrNumericLiteralExpected, reportError: isActive);
                            idExpression = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, id);
                        }

                        hasError = hasError || id.ContainsDiagnostics;
                        ids.Add(idExpression);

                        if (this.CurrentToken.Kind != SyntaxKind.CommaToken)
                        {
                            break;
                        }

                        ids.AddSeparator(this.EatToken());
                    }

                    var end = this.ParseEndOfDirective(hasError || !isActive, afterPragma: true);
                    return SyntaxFactory.PragmaWarningDirectiveTrivia(hash, pragma, warning, style, ids.ToList(), end, isActive);
                }
                else
                {
                    style = this.EatToken(SyntaxKind.DisableKeyword, ErrorCode.WRN_IllegalPPWarning, reportError: isActive);
                    var end = this.ParseEndOfDirective(ignoreErrors: true, afterPragma: true);
                    return SyntaxFactory.PragmaWarningDirectiveTrivia(hash, pragma, warning, style, default(SeparatedSyntaxList<ExpressionSyntax>), end, isActive);
                }
            }
            else if (this.CurrentToken.Kind == SyntaxKind.ChecksumKeyword)
            {
                var checksum = this.EatToken();
                var file = this.EatToken(SyntaxKind.StringLiteralToken, ErrorCode.WRN_IllegalPPChecksum, reportError: isActive);
                var guid = this.EatToken(SyntaxKind.StringLiteralToken, ErrorCode.WRN_IllegalPPChecksum, reportError: isActive && !file.IsMissing);
                if (isActive && !guid.IsMissing)
                {
                    Guid tmp;
                    if (!Guid.TryParse(guid.ValueText, out tmp))
                    {
                        guid = this.AddError(guid, ErrorCode.WRN_IllegalPPChecksum);
                    }
                }

                var bytes = this.EatToken(SyntaxKind.StringLiteralToken, ErrorCode.WRN_IllegalPPChecksum, reportError: isActive && !guid.IsMissing);
                if (isActive && !bytes.IsMissing)
                {
                    if (bytes.ValueText.Length % 2 != 0)
                    {
                        bytes = this.AddError(bytes, ErrorCode.WRN_IllegalPPChecksum);
                    }
                    else
                    {
                        foreach (char c in bytes.ValueText)
                        {
                            if (!SyntaxFacts.IsHexDigit(c))
                            {
                                bytes = this.AddError(bytes, ErrorCode.WRN_IllegalPPChecksum);
                                break;
                            }
                        }
                    }
                }

                hasError = file.ContainsDiagnostics | guid.ContainsDiagnostics | bytes.ContainsDiagnostics;
                var eod = this.ParseEndOfDirective(ignoreErrors: hasError, afterPragma: true);
                return SyntaxFactory.PragmaChecksumDirectiveTrivia(hash, pragma, checksum, file, guid, bytes, eod, isActive);
            }
            else
            {
                var warning = this.EatToken(SyntaxKind.WarningKeyword, ErrorCode.WRN_IllegalPragma, reportError: isActive);
                var style = this.EatToken(SyntaxKind.DisableKeyword, reportError: false);
                var eod = this.ParseEndOfDirective(ignoreErrors: true, afterPragma: true);
                return SyntaxFactory.PragmaWarningDirectiveTrivia(hash, pragma, warning, style, default(SeparatedSyntaxList<ExpressionSyntax>), eod, isActive);
            }
        }

        private DirectiveTriviaSyntax ParseShebangDirective(SyntaxToken hash, SyntaxToken exclamation, bool isActive)
        {
            if (lexer.Options.Kind != SourceCodeKind.Script && !lexer.Options.FileBasedProgram)
            {
                exclamation = this.AddError(exclamation, ErrorCode.ERR_PPShebangInProjectBasedProgram);
            }

            return SyntaxFactory.ShebangDirectiveTrivia(hash, exclamation, this.ParseEndOfDirectiveWithOptionalPreprocessingMessage(), isActive);
        }

        private DirectiveTriviaSyntax ParseIgnoredDirective(SyntaxToken hash, SyntaxToken colon, bool isActive, bool isFollowingToken)
        {
            if (isActive)
            {
                if (!lexer.Options.FileBasedProgram)
                {
                    colon = this.AddError(colon, ErrorCode.ERR_PPIgnoredNeedsFileBasedProgram);
                }

                if (isFollowingToken)
                {
                    colon = this.AddError(colon, ErrorCode.ERR_PPIgnoredFollowsToken);
                }

                if (_context.SeenAnyIfDirectives)
                {
                    colon = this.AddError(colon, ErrorCode.ERR_PPIgnoredFollowsIf);
                }
            }

            SyntaxToken endOfDirective = this.lexer.LexEndOfDirectiveWithOptionalContent(out SyntaxToken content);
            return SyntaxFactory.IgnoredDirectiveTrivia(hash, colon, content, endOfDirective, isActive);
        }

        private SyntaxToken ParseEndOfDirectiveWithOptionalPreprocessingMessage()
            => this.lexer.LexEndOfDirectiveWithOptionalPreprocessingMessage();

        private SyntaxToken ParseEndOfDirective(bool ignoreErrors, bool afterPragma = false, bool afterLineNumber = false)
        {
            var skippedTokens = new SyntaxListBuilder<SyntaxToken>();

            // Consume all extraneous tokens as leading SkippedTokens trivia.
            if (this.CurrentToken.Kind != SyntaxKind.EndOfDirectiveToken &&
                this.CurrentToken.Kind != SyntaxKind.EndOfFileToken)
            {
                skippedTokens = new SyntaxListBuilder<SyntaxToken>(10);

                if (!ignoreErrors)
                {
                    var errorCode = ErrorCode.ERR_EndOfPPLineExpected;
                    if (afterPragma)
                    {
                        errorCode = ErrorCode.WRN_EndOfPPLineExpected;
                    }
                    else if (afterLineNumber)
                    {
                        errorCode = ErrorCode.ERR_MissingPPFile;
                    }

                    skippedTokens.Add(this.AddError(this.EatToken().WithoutDiagnosticsGreen(), errorCode));
                }

                while (this.CurrentToken.Kind != SyntaxKind.EndOfDirectiveToken &&
                       this.CurrentToken.Kind != SyntaxKind.EndOfFileToken)
                {
                    skippedTokens.Add(this.EatToken().WithoutDiagnosticsGreen());
                }
            }

            // attach text from extraneous tokens as trivia to EndOfDirective token
            SyntaxToken endOfDirective = this.CurrentToken.Kind == SyntaxKind.EndOfDirectiveToken
                                         ? this.EatToken()
                                         : SyntaxFactory.Token(SyntaxKind.EndOfDirectiveToken);

            if (!skippedTokens.IsNull)
            {
                endOfDirective = endOfDirective.TokenWithLeadingTrivia(
                    SyntaxFactory.SkippedTokensTrivia(skippedTokens.ToList()));
            }

            return endOfDirective;
        }

        private ExpressionSyntax ParseExpression()
        {
            return this.ParseLogicalOr();
        }

        private ExpressionSyntax ParseLogicalOr()
        {
            var left = this.ParseLogicalAnd();
            while (this.CurrentToken.Kind == SyntaxKind.BarBarToken)
            {
                var op = this.EatToken();
                var right = this.ParseLogicalAnd();
                left = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalOrExpression, left, op, right);
            }

            return left;
        }

        private ExpressionSyntax ParseLogicalAnd()
        {
            var left = this.ParseEquality();
            while (this.CurrentToken.Kind == SyntaxKind.AmpersandAmpersandToken)
            {
                var op = this.EatToken();
                var right = this.ParseEquality();
                left = SyntaxFactory.BinaryExpression(SyntaxKind.LogicalAndExpression, left, op, right);
            }

            return left;
        }

        private ExpressionSyntax ParseEquality()
        {
            var left = this.ParseLogicalNot();
            while (this.CurrentToken.Kind == SyntaxKind.EqualsEqualsToken || this.CurrentToken.Kind == SyntaxKind.ExclamationEqualsToken)
            {
                var op = this.EatToken();
                var right = this.ParseEquality();
                left = SyntaxFactory.BinaryExpression(SyntaxFacts.GetBinaryExpression(op.Kind), left, op, right);
            }

            return left;
        }

        private ExpressionSyntax ParseLogicalNot()
        {
            if (this.CurrentToken.Kind == SyntaxKind.ExclamationToken)
            {
                var op = this.EatToken();
                return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, op, this.ParseLogicalNot());
            }

            return this.ParsePrimary();
        }

        private ExpressionSyntax ParsePrimary()
        {
            var k = this.CurrentToken.Kind;
            switch (k)
            {
                case SyntaxKind.OpenParenToken:
                    var open = this.EatToken();
                    var expr = this.ParseExpression();
                    var close = this.EatToken(SyntaxKind.CloseParenToken);
                    return SyntaxFactory.ParenthesizedExpression(open, expr, close);
                case SyntaxKind.IdentifierToken:
                    var identifier = TruncateIdentifier(this.EatToken());
                    return SyntaxFactory.IdentifierName(identifier);
                case SyntaxKind.TrueKeyword:
                case SyntaxKind.FalseKeyword:
                    return SyntaxFactory.LiteralExpression(SyntaxFacts.GetLiteralExpression(k), this.EatToken());
                default:
                    return SyntaxFactory.IdentifierName(this.EatToken(SyntaxKind.IdentifierToken, ErrorCode.ERR_InvalidPreprocExpr));
            }
        }

        // Ignore everything after the 128th character by setting the value text of the token
        // from the prefix.  This is for backwards compatibility with Dev 10.
        private static SyntaxToken TruncateIdentifier(SyntaxToken identifier)
        {
            if (identifier.Width > MAX_DIRECTIVE_IDENTIFIER_WIDTH)
            {
                var leading = identifier.GetLeadingTrivia();
                var trailing = identifier.GetTrailingTrivia();

                string text = identifier.ToString();
                string identifierPart = text.Substring(0, MAX_DIRECTIVE_IDENTIFIER_WIDTH);

                identifier = SyntaxFactory.Identifier(SyntaxKind.IdentifierToken, leading, text, identifierPart, trailing);
            }
            return identifier;
        }

        private bool EvaluateBool(ExpressionSyntax expr)
        {
            var result = Evaluate(expr);
            if (result is bool)
            {
                return (bool)result;
            }

            return false;
        }

        private object Evaluate(ExpressionSyntax expr)
        {
            switch (expr.Kind)
            {
                case SyntaxKind.ParenthesizedExpression:
                    return Evaluate(((ParenthesizedExpressionSyntax)expr).Expression);
                case SyntaxKind.TrueLiteralExpression:
                case SyntaxKind.FalseLiteralExpression:
                    return ((LiteralExpressionSyntax)expr).Token.Value;
                case SyntaxKind.LogicalAndExpression:
                case SyntaxKind.BitwiseAndExpression:
                    return EvaluateBool(((BinaryExpressionSyntax)expr).Left) && EvaluateBool(((BinaryExpressionSyntax)expr).Right);
                case SyntaxKind.LogicalOrExpression:
                case SyntaxKind.BitwiseOrExpression:
                    return EvaluateBool(((BinaryExpressionSyntax)expr).Left) || EvaluateBool(((BinaryExpressionSyntax)expr).Right);
                case SyntaxKind.EqualsExpression:
                    return object.Equals(Evaluate(((BinaryExpressionSyntax)expr).Left), Evaluate(((BinaryExpressionSyntax)expr).Right));
                case SyntaxKind.NotEqualsExpression:
                    return !object.Equals(Evaluate(((BinaryExpressionSyntax)expr).Left), Evaluate(((BinaryExpressionSyntax)expr).Right));
                case SyntaxKind.LogicalNotExpression:
                    return !EvaluateBool(((PrefixUnaryExpressionSyntax)expr).Operand);
                case SyntaxKind.IdentifierName:
                    // For backwards compatibility, we want to evaluate any unicode escape sequences in
                    // the identifier name and then check again for boolean literals.  (This actually
                    // seems like a bug that we're retaining for back-compat, because section 2.4.1 of
                    // the spec says that escape sequences can only appear in identifiers, character
                    // literals, and regular string literals - not boolean literals.  In (non-directive)
                    // C#, tru\u0065 is equivalent to the identifier @true, not the boolean literal true.)
                    string id = ((IdentifierNameSyntax)expr).Identifier.ValueText;
                    bool constantValue;
                    if (bool.TryParse(id, out constantValue))
                    {
                        return constantValue;
                    }
                    return IsDefined(id);
            }

            return false;
        }

        private bool IsDefined(string id)
        {
            var defState = _context.IsDefined(id);
            switch (defState)
            {
                default:
                case DefineState.Unspecified:
                    return this.Options.PreprocessorSymbols.Contains(id);
                case DefineState.Defined:
                    return true;
                case DefineState.Undefined:
                    return false;
            }
        }
    }
}
