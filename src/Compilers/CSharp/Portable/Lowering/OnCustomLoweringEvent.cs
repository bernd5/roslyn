// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using OnCustomLoweringDelegate = System.Func<
        Microsoft.CodeAnalysis.CSharp.BoundTreeRewriter,
        Microsoft.CodeAnalysis.CSharp.BoundNode,
        Microsoft.CodeAnalysis.Optional<Microsoft.CodeAnalysis.CSharp.BoundNode?>
    >;

namespace Microsoft.CodeAnalysis.CSharp.Compiler
{
    internal static class OnCustomLoweringEvent
    {
        private static event OnCustomLoweringDelegate? OnCustomLowering;

        public static Optional<BoundNode?> RaiseOnCustomLowering(BoundTreeRewriter rewriter, BoundNode node)
        {
            if (OnCustomLowering is { } e)
            {
                return e.Invoke(rewriter, node);
            }
            return new Optional<BoundNode?>();
        }

        public static void DoOnCustomLowering(this Compilation compilation,
            Action action, OnCustomLoweringDelegate onCustomLowering)
        {
            OnCustomLowering += onCustomLowering;
            try
            {
                action();
            }
            finally
            {
                OnCustomLowering -= onCustomLowering;
            }
        }
    }
}

