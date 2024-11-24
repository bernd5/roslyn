// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    partial class PEModuleBuilder
    {
        public bool HasCustomOnFlowAnalysisPass => OnFlowAnalysisPass != null;

        public delegate BoundBlock OnFlowAnalysisPassDelegate(ref FlowAnalysisEventData data);

        public event OnFlowAnalysisPassDelegate? OnFlowAnalysisPass;

        public record struct FlowAnalysisEventData(
            MethodSymbol Method,
            BoundBlock Block,
            TypeCompilationState CompilationState,
            BindingDiagnosticBag diagnostics,
            bool HasTrailingExpression,
            bool OriginalBodyNested
        )
        {
        }

        public BoundBlock? RaiseOnFlowAnalysisPass(
            MethodSymbol method,
            BoundBlock block,
            TypeCompilationState compilationState,
            BindingDiagnosticBag diagnostics,
            bool hasTrailingExpression,
            bool originalBodyNested)
        {
            if (OnFlowAnalysisPass is { } e)
            {
                var data = new FlowAnalysisEventData(method,
                    block, compilationState, diagnostics, hasTrailingExpression,
                    originalBodyNested);

                return OnFlowAnalysisPass(ref data);
            }
            return block;
        }
    }
}
