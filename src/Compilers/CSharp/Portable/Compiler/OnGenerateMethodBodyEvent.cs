// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal static class OnGenerateMethodBodyEvent
    {
        private static event Action<Compilation, MethodSymbol, BoundStatement>? OnGenerateMethodBody;

        public static void RaiseOnGenerateMethodBody(Compilation compilation, MethodSymbol method, BoundStatement block)
        {
            if (OnGenerateMethodBody is { } e)
            {
                e.Invoke(compilation, method, block);
            }
        }

        public static void DoWithOnGenerateMethodBody(this Compilation compilation,
            Action action, Action<IMethodSymbol, BoundStatement> onGenerateMethod)
        {
            OnGenerateMethodBody += handleEvent;
            try
            {
                action();
            }
            finally
            {
                OnGenerateMethodBody -= handleEvent;
            }

            void handleEvent(Compilation c, MethodSymbol method, BoundStatement block)
            {
                if (ReferenceEquals(compilation, c))
                {
                    onGenerateMethod(method.GetPublicSymbol(), block);
                }
            }
        }
    }
}
