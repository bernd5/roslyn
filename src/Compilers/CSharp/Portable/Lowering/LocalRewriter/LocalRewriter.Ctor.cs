// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.RuntimeMembers;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    partial class LocalRewriter
    {
        public LocalRewriter(
            Compilation compilation,
            MethodSymbol containingMethod,
            int containingMethodOrdinal,
            BoundStatement rootStatement,
            NamedTypeSymbol? containingType,
            SyntheticBoundNodeFactory factory,
            SynthesizedSubmissionFields previousSubmissionFields,
            bool allowOmissionOfConditionalCalls,
            BindingDiagnosticBag diagnostics
            ) : this((CSharpCompilation)compilation,
             containingMethod,
             containingMethodOrdinal,
             rootStatement,
             containingType,
             factory,
             previousSubmissionFields,
             allowOmissionOfConditionalCalls,
             diagnostics)
        {
            //
        }
    }
}
