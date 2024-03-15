// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Symbols;

namespace Microsoft.CodeAnalysis.CSharp.Emit
{
    partial class PEModuleBuilder
    {
        public event Func<MethodSymbol, BoundStatement, IMethodBody?>? OnGenerateMethodBody;

        public IMethodBody? RaiseOnGenerateMethodBody(MethodSymbol method, BoundStatement block)
        {
            if (OnGenerateMethodBody is { } e)
            {
                return e.Invoke(method, block);
            }
            return null;
        }
    }
}
