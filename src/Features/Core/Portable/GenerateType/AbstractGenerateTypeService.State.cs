﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.LanguageService;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.GenerateType;

internal abstract partial class AbstractGenerateTypeService<TService, TSimpleNameSyntax, TObjectCreationExpressionSyntax, TExpressionSyntax, TTypeDeclarationSyntax, TArgumentSyntax>
{
    protected sealed class State
    {
        public string Name { get; private set; } = null!;
        public bool NameIsVerbatim { get; private set; }

        // The name node that we're on.  Will be used to the name the type if it's
        // generated.
        public TSimpleNameSyntax SimpleName { get; private set; } = null!;

        // The entire expression containing the name, not including the creation.  i.e. "X.Goo"
        // in "new X.Goo()".
        public TExpressionSyntax NameOrMemberAccessExpression { get; private set; } = null!;

        // The object creation node if we have one.  i.e. if we're on the 'Goo' in "new X.Goo()".
        public TObjectCreationExpressionSyntax? ObjectCreationExpressionOpt { get; private set; }

        // One of these will be non null.  It's also possible for both to be non null. For
        // example, if you have "class C { Goo f; }", then "Goo" can be generated inside C or
        // inside the global namespace.  The namespace can be null or the type can be null if the
        // user has something like "ExistingType.NewType" or "ExistingNamespace.NewType".  In
        // that case they're being explicit about what they want to generate into.
        public INamedTypeSymbol? TypeToGenerateInOpt { get; private set; }
        public string? NamespaceToGenerateInOpt { get; private set; }

        // If we can infer a base type or interface for this type. 
        // 
        // i.e.: "IList<int> goo = new MyList();"
        public INamedTypeSymbol? BaseTypeOrInterfaceOpt { get; private set; }
        public bool IsInterface { get; private set; }
        public bool IsStruct { get; private set; }
        public bool IsAttribute { get; private set; }
        public bool IsException { get; private set; }
        public bool IsMembersWithModule { get; private set; }
        public bool IsTypeGeneratedIntoNamespaceFromMemberAccess { get; private set; }
        public bool IsSimpleNameGeneric { get; private set; }
        public bool IsPublicAccessibilityForTypeGeneration { get; private set; }
        public bool IsInterfaceOrEnumNotAllowedInTypeContext { get; private set; }
        public IMethodSymbol? DelegateMethodSymbol { get; private set; }
        public bool IsDelegateAllowed { get; private set; }
        public bool IsEnumNotAllowed { get; private set; }
        public Compilation Compilation { get; }
        public bool IsDelegateOnly { get; private set; }
        public bool IsClassInterfaceTypes { get; private set; }
        public List<TSimpleNameSyntax> PropertiesToGenerate { get; private set; } = null!;

        private State(Compilation compilation)
            => Compilation = compilation;

        public static async ValueTask<State?> GenerateAsync(
            TService service,
            SemanticDocument document,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            var state = new State(document.SemanticModel.Compilation);
            return await state.TryInitializeAsync(service, document, node, cancellationToken).ConfigureAwait(false) ? state : (State?)null;
        }

        private async ValueTask<bool> TryInitializeAsync(
            TService service,
            SemanticDocument semanticDocument,
            SyntaxNode node,
            CancellationToken cancellationToken)
        {
            if (node is not TSimpleNameSyntax)
            {
                return false;
            }

            SimpleName = (TSimpleNameSyntax)node;
            var syntaxFacts = semanticDocument.Document.GetRequiredLanguageService<ISyntaxFactsService>();
            syntaxFacts.GetNameAndArityOfSimpleName(SimpleName, out var name, out _);

            Name = name;
            NameIsVerbatim = syntaxFacts.IsVerbatimIdentifier(SimpleName.GetFirstToken());
            if (string.IsNullOrWhiteSpace(Name))
            {
                return false;
            }
            // We only support simple names or dotted names.  i.e. "(some + expr).Goo" is not a
            // valid place to generate a type for Goo.
            if (!service.TryInitializeState(semanticDocument, SimpleName, cancellationToken, out var generateTypeServiceStateOptions))
            {
                return false;
            }

            if (char.IsLower(name[0]) && !semanticDocument.SemanticModel.Compilation.IsCaseSensitive)
            {
                // It's near universal in .NET that types start with a capital letter.  As such,
                // if this name starts with a lowercase letter, don't even bother to offer 
                // "generate type".  The user most likely wants to run 'Add Import' (which will
                // then fix up a case where they typed an existing type name in lowercase, 
                // intending the fix to case correct it).
                return false;
            }

            Contract.ThrowIfNull(generateTypeServiceStateOptions.NameOrMemberAccessExpression);
            NameOrMemberAccessExpression = generateTypeServiceStateOptions.NameOrMemberAccessExpression;
            ObjectCreationExpressionOpt = generateTypeServiceStateOptions.ObjectCreationExpressionOpt;

            var semanticModel = semanticDocument.SemanticModel;
            var info = semanticModel.GetSymbolInfo(SimpleName, cancellationToken);
            if (info.Symbol != null)
            {
                // This bound, so no need to generate anything.
                return false;
            }

            var semanticFacts = semanticDocument.Document.GetRequiredLanguageService<ISemanticFactsService>();
            if (!semanticFacts.IsTypeContext(semanticModel, NameOrMemberAccessExpression.SpanStart, cancellationToken) &&
                !semanticFacts.IsExpressionContext(semanticModel, NameOrMemberAccessExpression.SpanStart, cancellationToken) &&
                !semanticFacts.IsStatementContext(semanticModel, NameOrMemberAccessExpression.SpanStart, cancellationToken) &&
                !semanticFacts.IsInsideNameOfExpression(semanticModel, NameOrMemberAccessExpression, cancellationToken) &&
                !semanticFacts.IsNamespaceContext(semanticModel, NameOrMemberAccessExpression.SpanStart, cancellationToken))
            {
                return false;
            }

            // If this isn't something that can be created, then don't bother offering to create
            // it.
            if (info.CandidateReason == CandidateReason.NotCreatable)
            {
                return false;
            }

            if (info.CandidateReason is CandidateReason.Inaccessible or
                CandidateReason.NotReferencable or
                CandidateReason.OverloadResolutionFailure)
            {
                // We bound to something inaccessible, or overload resolution on a 
                // constructor call failed.  Don't want to offer GenerateType here.
                return false;
            }

            if (ObjectCreationExpressionOpt != null)
            {
                // If we're new'ing up something illegal, then don't offer generate type.
                var typeInfo = semanticModel.GetTypeInfo(ObjectCreationExpressionOpt, cancellationToken);
                if (typeInfo.Type.IsModuleType())
                {
                    return false;
                }
            }

            await DetermineNamespaceOrTypeToGenerateInAsync(service, semanticDocument, cancellationToken).ConfigureAwait(false);

            // Now, try to infer a possible base type for this new class/interface.
            InferBaseType(service, semanticDocument, cancellationToken);
            IsInterface = GenerateInterface(service);
            IsStruct = GenerateStruct(service, semanticModel, cancellationToken);
            IsAttribute = BaseTypeOrInterfaceOpt != null && BaseTypeOrInterfaceOpt.Equals(semanticModel.Compilation.AttributeType());
            IsException = BaseTypeOrInterfaceOpt != null && BaseTypeOrInterfaceOpt.Equals(semanticModel.Compilation.ExceptionType());
            IsMembersWithModule = generateTypeServiceStateOptions.IsMembersWithModule;
            IsTypeGeneratedIntoNamespaceFromMemberAccess = generateTypeServiceStateOptions.IsTypeGeneratedIntoNamespaceFromMemberAccess;
            IsInterfaceOrEnumNotAllowedInTypeContext = generateTypeServiceStateOptions.IsInterfaceOrEnumNotAllowedInTypeContext;
            IsDelegateAllowed = generateTypeServiceStateOptions.IsDelegateAllowed;
            IsDelegateOnly = generateTypeServiceStateOptions.IsDelegateOnly;
            IsEnumNotAllowed = generateTypeServiceStateOptions.IsEnumNotAllowed;
            DelegateMethodSymbol = generateTypeServiceStateOptions.DelegateCreationMethodSymbol;
            IsClassInterfaceTypes = generateTypeServiceStateOptions.IsClassInterfaceTypes;
            IsSimpleNameGeneric = service.IsGenericName(SimpleName);
            PropertiesToGenerate = generateTypeServiceStateOptions.PropertiesToGenerate;

            if (IsAttribute && TypeToGenerateInOpt.GetAllTypeParameters().Any())
            {
                TypeToGenerateInOpt = null;
            }

            return TypeToGenerateInOpt != null || NamespaceToGenerateInOpt != null;
        }

        private void InferBaseType(
            TService service,
            SemanticDocument document,
            CancellationToken cancellationToken)
        {
            // See if we can find a possible base type for the type being generated.
            // NOTE(cyrusn): I currently limit this to when we have an object creation node.
            // That's because that's when we would have an expression that could be converted to
            // something else.  i.e. if the user writes "IList<int> list = new Goo()" then we can
            // infer a base interface for 'Goo'.  However, if they write "IList<int> list = Goo"
            // then we don't really want to infer a base type for 'Goo'.

            // However, there are a few other cases were we can infer a base type.
            var syntaxFacts = document.Document.GetRequiredLanguageService<ISyntaxFactsService>();
            if (service.IsInCatchDeclaration(NameOrMemberAccessExpression))
            {
                SetBaseType(this.Compilation.ExceptionType());
            }
            else if (syntaxFacts.IsNameOfAttribute(NameOrMemberAccessExpression))
            {
                SetBaseType(this.Compilation.AttributeType());
            }
            else
            {
                var expr = ObjectCreationExpressionOpt ?? NameOrMemberAccessExpression;
                if (expr != null)
                {
                    var typeInference = document.Document.GetRequiredLanguageService<ITypeInferenceService>();
                    var baseTypes = typeInference.InferTypes(document.SemanticModel, expr, cancellationToken);
                    foreach (var baseType in baseTypes)
                    {
                        if (this.BaseTypeOrInterfaceOpt is not null)
                            break;

                        SetBaseType(baseType as INamedTypeSymbol);
                    }
                }
            }
        }

        private void SetBaseType(INamedTypeSymbol? baseType)
        {
            if (baseType == null)
                return;

            // A base type need to be non class or interface type.
            if (baseType.IsSealed || baseType.IsStatic)
                return;

            if (baseType.TypeKind is not TypeKind.Class and not TypeKind.Interface)
                return;

            // 'Object' is redundant to derive from (and we're prefer a more specialized typed if we can infer one).
            // We also filter out non-sealed types that a normal user type still isn't allow to explicit derive from.
            if (baseType.SpecialType is
                    SpecialType.System_Object or
                    SpecialType.System_Array or
                    SpecialType.System_Delegate or
                    SpecialType.System_MulticastDelegate or
                    SpecialType.System_ValueType or
                    SpecialType.System_Enum)
            {
                return;
            }

            // Strip off top-level nullability since we can't put top-level nullability into the base list. We will still include nested nullability
            // if you're deriving some interface like IEnumerable<string?>.
            BaseTypeOrInterfaceOpt = (INamedTypeSymbol)baseType.WithNullableAnnotation(NullableAnnotation.None);
        }

        private bool GenerateStruct(TService service, SemanticModel semanticModel, CancellationToken cancellationToken)
            => service.IsInValueTypeConstraintContext(semanticModel, NameOrMemberAccessExpression, cancellationToken);

        private bool GenerateInterface(TService service)
        {
            if (!IsAttribute &&
                !IsException &&
                Name.LooksLikeInterfaceName() &&
                ObjectCreationExpressionOpt == null &&
                (BaseTypeOrInterfaceOpt == null || BaseTypeOrInterfaceOpt.TypeKind == TypeKind.Interface))
            {
                return true;
            }

            return service.IsInInterfaceList(NameOrMemberAccessExpression);
        }

        private async ValueTask DetermineNamespaceOrTypeToGenerateInAsync(
            TService service,
            SemanticDocument document,
            CancellationToken cancellationToken)
        {
            DetermineNamespaceOrTypeToGenerateInWorker(service, document.SemanticModel, cancellationToken);

            // Can only generate into a type if it's a class and it's from source.
            if (TypeToGenerateInOpt != null)
            {
                if (TypeToGenerateInOpt.TypeKind is not TypeKind.Class and
                    not TypeKind.Module)
                {
                    TypeToGenerateInOpt = null;
                }
                else
                {
                    var symbol = await SymbolFinder.FindSourceDefinitionAsync(TypeToGenerateInOpt, document.Project.Solution, cancellationToken).ConfigureAwait(false);
                    if (symbol == null ||
                        !symbol.IsKind(SymbolKind.NamedType) ||
                        !symbol.Locations.Any(static loc => loc.IsInSource))
                    {
                        TypeToGenerateInOpt = null;
                        return;
                    }

                    var sourceTreeToBeGeneratedIn = symbol.Locations.First(loc => loc.IsInSource).SourceTree;
                    var documentToBeGeneratedIn = document.Project.Solution.GetDocument(sourceTreeToBeGeneratedIn);

                    if (documentToBeGeneratedIn == null)
                    {
                        TypeToGenerateInOpt = null;
                        return;
                    }

                    // If the 2 documents are in different project then we must have Public Accessibility.
                    // If we are generating in a website project, we also want to type to be public so the 
                    // designer files can access the type.
                    if (documentToBeGeneratedIn.Project != document.Project)
                    {
                        IsPublicAccessibilityForTypeGeneration = true;
                    }

                    TypeToGenerateInOpt = (INamedTypeSymbol)symbol;
                }
            }

            if (TypeToGenerateInOpt != null)
            {
                if (!CodeGenerator.CanAdd(document.Project.Solution, TypeToGenerateInOpt, cancellationToken))
                {
                    TypeToGenerateInOpt = null;
                }
            }
        }

        private bool DetermineNamespaceOrTypeToGenerateInWorker(
            TService service,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            // If we're on the right of a dot, see if we can figure out what's on the left.  If
            // it doesn't bind to a type or a namespace, then we can't proceed.
            if (SimpleName != NameOrMemberAccessExpression)
            {
                return DetermineNamespaceOrTypeToGenerateIn(
                    service, semanticModel,
                    service.GetLeftSideOfDot(SimpleName), cancellationToken);
            }
            else
            {
                // The name is standing alone.  We can either generate the type into our
                // containing type, or into our containing namespace.
                //
                // TODO(cyrusn): We need to make this logic work if the type is in the
                // base/interface list of a type.
                var format = SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);
                TypeToGenerateInOpt = service.DetermineTypeToGenerateIn(semanticModel, SimpleName, cancellationToken);
                if (TypeToGenerateInOpt != null)
                {
                    NamespaceToGenerateInOpt = TypeToGenerateInOpt.ContainingNamespace.ToDisplayString(format);
                }
                else
                {
                    var namespaceSymbol = semanticModel.GetEnclosingNamespace(SimpleName.SpanStart, cancellationToken);
                    if (namespaceSymbol != null)
                    {
                        NamespaceToGenerateInOpt = namespaceSymbol.ToDisplayString(format);
                    }
                }
            }

            return true;
        }

        private bool DetermineNamespaceOrTypeToGenerateIn(
            TService service,
            SemanticModel semanticModel,
            TExpressionSyntax leftSide,
            CancellationToken cancellationToken)
        {
            var leftSideInfo = semanticModel.GetSymbolInfo(leftSide, cancellationToken);

            if (leftSideInfo.Symbol != null)
            {
                var symbol = leftSideInfo.Symbol;

                if (symbol is INamespaceSymbol)
                {
                    NamespaceToGenerateInOpt = symbol.ToNameDisplayString();
                    return true;
                }
                else if (symbol is INamedTypeSymbol)
                {
                    // TODO: Code coverage
                    TypeToGenerateInOpt = (INamedTypeSymbol)symbol.OriginalDefinition;
                    return true;
                }

                // We bound to something other than a namespace or named type.  Can't generate a
                // type inside this.
                return false;
            }
            else
            {
                // If it's a dotted name, then perhaps it's a namespace.  i.e. the user wrote
                // "new Goo.Bar.Baz()".  In this case we want to generate a namespace for
                // "Goo.Bar".
                if (service.TryGetNameParts(leftSide, out var nameParts))
                {
                    NamespaceToGenerateInOpt = string.Join(".", nameParts);
                    return true;
                }
            }

            return false;
        }
    }

    protected sealed class GenerateTypeServiceStateOptions
    {
        public TExpressionSyntax? NameOrMemberAccessExpression { get; set; }
        public TObjectCreationExpressionSyntax? ObjectCreationExpressionOpt { get; set; }
        public IMethodSymbol? DelegateCreationMethodSymbol { get; set; }
        public List<TSimpleNameSyntax> PropertiesToGenerate { get; }
        public bool IsMembersWithModule { get; set; }
        public bool IsTypeGeneratedIntoNamespaceFromMemberAccess { get; set; }
        public bool IsInterfaceOrEnumNotAllowedInTypeContext { get; set; }
        public bool IsDelegateAllowed { get; set; }
        public bool IsEnumNotAllowed { get; set; }
        public bool IsDelegateOnly { get; internal set; }
        public bool IsClassInterfaceTypes { get; internal set; }

        public GenerateTypeServiceStateOptions()
        {
            IsMembersWithModule = false;
            PropertiesToGenerate = [];
            IsTypeGeneratedIntoNamespaceFromMemberAccess = false;
            IsInterfaceOrEnumNotAllowedInTypeContext = false;
            IsDelegateAllowed = true;
            IsEnumNotAllowed = false;
            IsDelegateOnly = false;
        }
    }
}
