// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;

namespace Microsoft.CodeAnalysis
{
    internal static class ISymbolExtension_Access
    {
        public static Accessibility GetMinAccessibility(this ISymbol symbol, bool includeTypeArguments = true)
        {
            return GetMinAccessibility(symbol, null,
                includeTypeArguments, true);
        }

        public static Accessibility GetMinAccessibility(this ISymbol symbol, Func<ITypeSymbol, bool>? checkType,
            bool includeTypeArguments = true, bool checkContainingTypes = true)
        {
            var visitor = new MinAccessVisitor(includeTypeArguments, checkContainingTypes, checkType);
            visitor.Visit(symbol);
            return visitor.MinAccessibility;
        }

        private class MinAccessVisitor(bool includeTypeArguments, bool checkContainingTypes,
            Func<ITypeSymbol, bool>? checkType) : SymbolVisitor
        {
            public Accessibility MinAccessibility = Accessibility.Public;
            private readonly Func<ITypeSymbol, bool>? checkType = checkType;
            private readonly bool includeTypeArguments = includeTypeArguments;
            private readonly bool checkContainingTypes = checkContainingTypes;

            public override void VisitNamedType(INamedTypeSymbol symbol)
            {
                var cur = symbol;
                if (checkType?.Invoke(symbol) ?? true)
                {
                    while (cur != null)
                    {
                        var curAccess = cur.DeclaredAccessibility;
                        if (curAccess != Accessibility.NotApplicable)
                        {
                            if ((int)curAccess < ((int)MinAccessibility))
                            {
                                MinAccessibility = curAccess;
                            }

                            //if we know all containing types are ignored we can skip this
                            if (checkContainingTypes)
                            {
                                cur = cur.ContainingType;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }

                if (includeTypeArguments)
                {
                    foreach (var a in symbol.TypeArguments)
                    {
                        Visit(a);
                    }
                }
            }
            public override void VisitArrayType(IArrayTypeSymbol symbol)
            {
                Visit(symbol.ElementType);
            }

            public override void VisitPointerType(IPointerTypeSymbol symbol)
            {
                Visit(symbol.PointedAtType);
            }

            public override void VisitDynamicType(IDynamicTypeSymbol symbol)
            {
            }

            public override void VisitEvent(IEventSymbol symbol)
            {
                Visit(symbol.Type);
                Visit(symbol.AddMethod);
                Visit(symbol.RemoveMethod);
            }

            public override void VisitField(IFieldSymbol symbol)
            {
                Visit(symbol.Type);
            }

            public override void VisitFunctionPointerType(IFunctionPointerTypeSymbol symbol)
            {
                Visit(symbol.Signature);
            }

            public override void VisitMethod(IMethodSymbol symbol)
            {
                Visit(symbol.ReturnType);
                foreach (var p in symbol.Parameters)
                {
                    Visit(p);
                }
            }

            public override void VisitParameter(IParameterSymbol symbol)
            {
                Visit(symbol.Type);
            }

            public override void VisitTypeParameter(ITypeParameterSymbol symbol)
            {
            }

            public override void VisitProperty(IPropertySymbol symbol)
            {
                Visit(symbol.Type);
            }
        }
    }
}
