// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal static class OnGenerateMethodBodyEvent
    {
        private static event Func<Compilation, MethodSymbol, BoundStatement, IMethodBody?>? OnGenerateMethodBody;

        /// <summary>
        /// allows to customize the method body generation
        /// </summary>
        /// <param name="compilation"></param>
        /// <param name="method"></param>
        /// <param name="block"></param>
        /// <returns>returning "false" means to not execute the default method body generation</returns>
        public static IMethodBody? RaiseOnGenerateMethodBody(Compilation compilation, MethodSymbol method, BoundStatement block)
        {
            if (OnGenerateMethodBody is { } e)
            {
                return e.Invoke(compilation, method, block);
            }
            return null;
        }

        public static void DoWithOnGenerateMethodBody(this Compilation compilation,
            Action action, Func<IMethodSymbol, BoundStatement, IMethodBody> onGenerateMethod)
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

            IMethodBody? handleEvent(Compilation c, MethodSymbol method, BoundStatement block)
            {
                if (ReferenceEquals(compilation, c))
                {
                    return onGenerateMethod(method.GetPublicSymbol(), block);
                }
                return null;
            }
        }
    }
}
