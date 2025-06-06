﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Composition
Imports System.Diagnostics.CodeAnalysis
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Editor.Host
Imports Microsoft.CodeAnalysis.Editor.Shared.Utilities
Imports Microsoft.CodeAnalysis.Host.Mef
Imports Microsoft.CodeAnalysis.Navigation

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.Navigation
    <ExportLanguageService(GetType(IDefinitionLocationService), LanguageNames.VisualBasic), [Shared]>
    Friend Class VisualBasicDefinitionLocationService
        Inherits AbstractDefinitionLocationService

        <ImportingConstructor>
        <SuppressMessage("RoslynDiagnosticsReliability", "RS0033:Importing constructor should be [Obsolete]", Justification:="Used in test code: https://github.com/dotnet/roslyn/issues/42814")>
        Public Sub New(threadingContext As IThreadingContext,
                       streamingPresenter As IStreamingFindUsagesPresenter)
            MyBase.New(threadingContext, streamingPresenter)
        End Sub
    End Class
End Namespace
