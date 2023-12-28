// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal static class OnLowerMethodBodyEvent
    {
        public static bool HasCustomOnLowerMethod => OnLowerMethodBody != null;

        public delegate void OnCustomLower(ref LowerEventData data);

        private static event OnCustomLower? OnLowerMethodBody;

        public record struct LowerEventData(MethodSymbol method,
            int methodOrdinal,
            BoundStatement body,
            SynthesizedSubmissionFields previousSubmissionFields,
            TypeCompilationState compilationState,
            MethodInstrumentation instrumentation,
            DebugDocumentProvider debugDocumentProvider,
            ImmutableArray<SourceSpan> codeCoverageSpans,
            BindingDiagnosticBag diagnostics,
            VariableSlotAllocator lazyVariableSlotAllocator,
            ArrayBuilder<EncLambdaInfo> lambdaDebugInfoBuilder,
            ArrayBuilder<LambdaRuntimeRudeEditInfo> lambdaRuntimeRudeEditsBuilder,
            ArrayBuilder<EncClosureInfo> closureDebugInfoBuilder,
            ArrayBuilder<StateMachineStateDebugInfo> stateMachineStateDebugInfoBuilder,
            StateMachineTypeSymbol? stateMachineTypeOpt
        )
        {
            public BoundStatement? loweredBody;
        }

        public static BoundStatement? RaiseOnLowerMethodBody(
            MethodSymbol method,
            int methodOrdinal,
            BoundStatement body,
            SynthesizedSubmissionFields previousSubmissionFields,
            TypeCompilationState compilationState,
            MethodInstrumentation instrumentation,
            DebugDocumentProvider debugDocumentProvider,
            out ImmutableArray<SourceSpan> codeCoverageSpans,
            BindingDiagnosticBag diagnostics,
            ref VariableSlotAllocator lazyVariableSlotAllocator,
            ArrayBuilder<EncLambdaInfo> lambdaDebugInfoBuilder,
            ArrayBuilder<LambdaRuntimeRudeEditInfo> lambdaRuntimeRudeEditsBuilder,
            ArrayBuilder<EncClosureInfo> closureDebugInfoBuilder,
            ArrayBuilder<StateMachineStateDebugInfo> stateMachineStateDebugInfoBuilder,
            out StateMachineTypeSymbol? stateMachineTypeOpt)
        {
            if (OnLowerMethodBody is { } e)
            {
                codeCoverageSpans = default;
                stateMachineTypeOpt = null;
                var state = new LowerEventData(method, methodOrdinal, body, previousSubmissionFields,
                    compilationState, instrumentation, debugDocumentProvider, codeCoverageSpans, diagnostics,
                    lazyVariableSlotAllocator, lambdaDebugInfoBuilder, lambdaRuntimeRudeEditsBuilder,
                    closureDebugInfoBuilder, stateMachineStateDebugInfoBuilder, stateMachineTypeOpt);

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

        public static void DoWithOnLowerMethodBody(Action action, OnCustomLower onLower)
        {
            OnLowerMethodBody += onLower;
            try
            {
                action();
            }
            finally
            {
                OnLowerMethodBody -= onLower;
            }
        }
    }
}
