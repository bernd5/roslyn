// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    partial class PEModuleBuilder
    {
        public bool HasCustomOnLowerMethod => OnLowerMethodBody != null;

        public delegate void OnCustomLower(ref LowerEventData data);

        public event OnCustomLower? OnLowerMethodBody;

        public record struct LowerEventData(MethodSymbol method,
            SourceExtensionImplementationMethodSymbol extensionImplementationMethod,
            int methodOrdinal,
            BoundStatement body,
            SynthesizedSubmissionFields previousSubmissionFields,
            TypeCompilationState compilationState,
            MethodInstrumentation instrumentation,
            DebugDocumentProvider debugDocumentProvider,
            ImmutableArray<SourceSpan> codeCoverageSpans,
            BindingDiagnosticBag diagnostics,
            VariableSlotAllocator lazyVariableSlotAllocator,
            ArrayBuilder<EncLambdaInfo>? lambdaDebugInfoBuilder,
            ArrayBuilder<LambdaRuntimeRudeEditInfo>? lambdaRuntimeRudeEditsBuilder,
            ArrayBuilder<EncClosureInfo>? closureDebugInfoBuilder,
            ArrayBuilder<StateMachineStateDebugInfo> stateMachineStateDebugInfoBuilder,
            StateMachineTypeSymbol? stateMachineTypeOpt,
            bool isSynthesizedMethod
        )
        {
            public BoundStatement? loweredBody;
        }

        public BoundStatement? RaiseOnLowerMethodBody(
            MethodSymbol method,
            SourceExtensionImplementationMethodSymbol extensionImplementationMethod,
            int methodOrdinal,
            BoundStatement body,
            SynthesizedSubmissionFields previousSubmissionFields,
            TypeCompilationState compilationState,
            MethodInstrumentation instrumentation,
            DebugDocumentProvider debugDocumentProvider,
            out ImmutableArray<SourceSpan> codeCoverageSpans,
            BindingDiagnosticBag diagnostics,
            ref VariableSlotAllocator lazyVariableSlotAllocator,
            ArrayBuilder<EncLambdaInfo>? lambdaDebugInfoBuilder,
            ArrayBuilder<LambdaRuntimeRudeEditInfo>? lambdaRuntimeRudeEditsBuilder,
            ArrayBuilder<EncClosureInfo>? closureDebugInfoBuilder,
            ArrayBuilder<StateMachineStateDebugInfo> stateMachineStateDebugInfoBuilder,
            out StateMachineTypeSymbol? stateMachineTypeOpt,
            bool isSynthesizedMethod = false)
        {
            if (OnLowerMethodBody is { } e)
            {
                codeCoverageSpans = default;
                stateMachineTypeOpt = null;
                var state = new LowerEventData(method, extensionImplementationMethod, methodOrdinal, body, previousSubmissionFields,
                    compilationState, instrumentation, debugDocumentProvider, codeCoverageSpans, diagnostics,
                    lazyVariableSlotAllocator, lambdaDebugInfoBuilder, lambdaRuntimeRudeEditsBuilder,
                    closureDebugInfoBuilder, stateMachineStateDebugInfoBuilder, stateMachineTypeOpt, isSynthesizedMethod);

                e.Invoke(ref state);
                if (state.loweredBody is { } lowered)
                {
                    codeCoverageSpans = state.codeCoverageSpans;
                    lazyVariableSlotAllocator = state.lazyVariableSlotAllocator;
                    stateMachineTypeOpt = state.stateMachineTypeOpt;

                    return lowered;
                }
            }
            codeCoverageSpans = default;
            stateMachineTypeOpt = null;
            return null;
        }
    }
}
