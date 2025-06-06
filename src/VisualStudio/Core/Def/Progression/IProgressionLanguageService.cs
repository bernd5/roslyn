﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.Progression;

internal interface IProgressionLanguageService : ILanguageService
{
    string GetDescriptionForSymbol(ISymbol symbol, bool includeContainingSymbol);
    string GetLabelForSymbol(ISymbol symbol, bool includeContainingSymbol);
}
