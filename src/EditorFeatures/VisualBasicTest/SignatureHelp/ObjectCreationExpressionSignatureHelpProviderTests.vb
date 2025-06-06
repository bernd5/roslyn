﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Editor.UnitTests.SignatureHelp
Imports Microsoft.CodeAnalysis.VisualBasic.SignatureHelp

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.SignatureHelp

    Public Class ObjectCreationExpressionSignatureHelpProviderTests
        Inherits AbstractVisualBasicSignatureHelpProviderTests

        Friend Overrides Function GetSignatureHelpProviderType() As Type
            Return GetType(ObjectCreationExpressionSignatureHelpProvider)
        End Function

#Region "Regular tests"

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithoutParameters() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub Goo()
        Dim obj = [|new C($$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C()", String.Empty, Nothing, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem("https://github.com/dotnet/roslyn/issues/25830")>
        Public Async Function PickCorrectOverload_PickString() As Task

            Dim markup = <Text><![CDATA[
Public Class C
    Sub M()
        Dim obj = [|new C(i:="Hello"$$|])
    End Sub

    Public Sub New(i As String)
    End Sub
    Public Sub New(i As Integer)
    End Sub
    Public Sub New(filtered As Byte)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(i As Integer)", String.Empty, Nothing, currentParameterIndex:=0))
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(i As String)", String.Empty, Nothing, currentParameterIndex:=0, isSelected:=True))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.Completion)>
        <WorkItem("https://github.com/dotnet/roslyn/issues/25830")>
        Public Async Function PickCorrectOverload_PickInteger() As Task

            Dim markup = <Text><![CDATA[
Public Class C
    Sub M()
        Dim obj = [|new C(i:=1$$|])
    End Sub

    Public Sub New(i As String)
    End Sub
    Public Sub New(i As Integer)
    End Sub
    Public Sub New(filtered As Byte)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(i As Integer)", String.Empty, Nothing, currentParameterIndex:=0, isSelected:=True))
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(i As String)", String.Empty, Nothing, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithoutParametersMethodXmlComments() As Task
            Dim markup = <a><![CDATA[
Class C

    ''' <summary>
    ''' Summary for Goo. See <see cref="System.Object"/>
    ''' </summary>
    Sub New()
    End Sub

    Sub Goo()
        Dim obj = [|new C($$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C()", "Summary for Goo. See Object", Nothing, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithParametersOn1() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C($$2, 4|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithParametersXmlCommentsOn1() As Task
            Dim markup = <a><![CDATA[
Class C
    ''' <summary>
    ''' Summary for Goo
    ''' </summary>
    ''' <param name="a">Param a</param>
    ''' <param name="b">Param b</param>
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C($$2, 4|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", "Summary for Goo", "Param a", currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        <WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/545931")>
        Public Async Function TestUnsupportedParameters() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub Goo()
        Dim obj = [|new String($$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("String(value As Char())", currentParameterIndex:=0))
            expectedOrderedItems.Add(New SignatureHelpTestItem("String(c As Char, count As Integer)", currentParameterIndex:=0))
            expectedOrderedItems.Add(New SignatureHelpTestItem("String(value As Char(), startIndex As Integer, length As Integer)", currentParameterIndex:=0))

            ' All the unsafe pointer overloads should be missing in VB
            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithParametersOn2() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C(2, $$4|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", String.Empty, String.Empty, currentParameterIndex:=1))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithParametersXmlComentsOn2() As Task
            Dim markup = <a><![CDATA[
Imports System
Class C
    ''' <summary>
    ''' Summary for Goo
    ''' </summary>
    ''' <param name="a">Param a</param>
    ''' <param name="b">Param b. See <see cref="System.IAsyncResult"/></param>
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C(2, $$4|])
    End Sub
End Class]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", "Summary for Goo", "Param b. See IAsyncResult", currentParameterIndex:=1))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithoutClosingParen() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub Goo()
        Dim obj = [|new C($$
    |]End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C()", String.Empty, Nothing, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithoutClosingParenWithParameters() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C($$2, 4
    |]End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationWithoutClosingParenWithParametersOn2() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub

    Sub Goo()
        Dim obj = [|new C(2, $$4
    |]End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", String.Empty, String.Empty, currentParameterIndex:=1))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationOnLambda() As Task
            Dim markup = <a><![CDATA[
Imports System

Class C
    Sub Goo()
        Dim obj = [|new Action(Of Integer, Integer)($$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("Action(Of Integer, Integer)(Sub (Integer, Integer))", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems)
        End Function

#End Region

#Region "Current Parameter Name"

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestCurrentParameterName() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(int a, string b)
    End Sub

    Sub Goo()
        Dim obj = [|new C(b:=String.Empty, $$a:=2|])
    End Sub
End Class
]]></a>.Value

            Await VerifyCurrentParameterNameAsync(markup, "a")
        End Function

#End Region

#Region "Trigger tests"

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationOnTriggerParens() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub Goo()
        Dim obj = [|new C($$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C()", String.Empty, Nothing, currentParameterIndex:=0))

            Await TestAsync(markup, expectedOrderedItems, usePreviousCharAsTrigger:=True)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestInvocationOnTriggerComma() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub
    Sub Goo()
        Dim obj = [|new C(2,$$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(a As Integer, b As Integer)", String.Empty, String.Empty, currentParameterIndex:=1))

            Await TestAsync(markup, expectedOrderedItems, usePreviousCharAsTrigger:=True)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Async Function TestNoInvocationOnSpace() As Task
            Dim markup = <a><![CDATA[
Class C
    Sub New(a As Integer, b As Integer)
    End Sub
    Sub Goo()
        Dim obj = [|new C(2, $$|])
    End Sub
End Class
]]></a>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()

            Await TestAsync(markup, expectedOrderedItems, usePreviousCharAsTrigger:=True)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        Public Sub TestTriggerCharacters()
            Dim expectedCharacters() As Char = {","c, "("c}
            Dim unexpectedCharacters() As Char = {" "c, "["c, "<"c}

            VerifyTriggerCharacters(expectedCharacters, unexpectedCharacters)
        End Sub

#End Region

#Region "EditorBrowsable tests"
        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        <WorkItem(7336, "DevDiv_Projects/Roslyn")>
        Public Async Function TestEditorBrowsable_ObjectCreation_BrowsableAlways() As Task
            Dim markup = <Text><![CDATA[
Class Program
    Sub Main(args As String())
        Dim x = New C($$
    End Sub
End Class
]]></Text>.Value

            Dim referencedCode = <Text><![CDATA[
Public Class C
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)>
    Public Sub New(x As Integer)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(x As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestSignatureHelpInEditorBrowsableContextsAsync(markup:=markup,
                                                       referencedCode:=referencedCode,
                                                       expectedOrderedItemsMetadataReference:=expectedOrderedItems,
                                                       expectedOrderedItemsSameSolution:=expectedOrderedItems,
                                                       sourceLanguage:=LanguageNames.VisualBasic,
                                                       referencedLanguage:=LanguageNames.VisualBasic)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        <WorkItem(7336, "DevDiv_Projects/Roslyn")>
        Public Async Function TestEditorBrowsable_ObjectCreation_BrowsableNever() As Task
            Dim markup = <Text><![CDATA[
Class Program
    Sub Main(args As String())
        Dim x = New C($$
    End Sub
End Class
]]></Text>.Value

            Dim referencedCode = <Text><![CDATA[
Public Class C
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Sub New(x As Integer)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(x As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestSignatureHelpInEditorBrowsableContextsAsync(markup:=markup,
                                                       referencedCode:=referencedCode,
                                                       expectedOrderedItemsMetadataReference:=New List(Of SignatureHelpTestItem)(),
                                                       expectedOrderedItemsSameSolution:=expectedOrderedItems,
                                                       sourceLanguage:=LanguageNames.VisualBasic,
                                                       referencedLanguage:=LanguageNames.VisualBasic)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        <WorkItem(7336, "DevDiv_Projects/Roslyn")>
        Public Async Function TestEditorBrowsable_ObjectCreation_BrowsableAdvanced() As Task
            Dim markup = <Text><![CDATA[
Class Program
    Sub Main(args As String())
        Dim x = New C($$
    End Sub
End Class
]]></Text>.Value

            Dim referencedCode = <Text><![CDATA[
Public Class C
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)>
    Public Sub New(x As Integer)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItems = New List(Of SignatureHelpTestItem)()
            expectedOrderedItems.Add(New SignatureHelpTestItem("C(x As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestSignatureHelpInEditorBrowsableContextsAsync(markup:=markup,
                                                       referencedCode:=referencedCode,
                                                       expectedOrderedItemsMetadataReference:=New List(Of SignatureHelpTestItem)(),
                                                       expectedOrderedItemsSameSolution:=expectedOrderedItems,
                                                       sourceLanguage:=LanguageNames.VisualBasic,
                                                       referencedLanguage:=LanguageNames.VisualBasic,
                                                       hideAdvancedMembers:=True)

            Await TestSignatureHelpInEditorBrowsableContextsAsync(markup:=markup,
                                                       referencedCode:=referencedCode,
                                                       expectedOrderedItemsMetadataReference:=expectedOrderedItems,
                                                       expectedOrderedItemsSameSolution:=expectedOrderedItems,
                                                       sourceLanguage:=LanguageNames.VisualBasic,
                                                       referencedLanguage:=LanguageNames.VisualBasic,
                                                       hideAdvancedMembers:=False)
        End Function

        <Fact, Trait(Traits.Feature, Traits.Features.SignatureHelp)>
        <WorkItem(7336, "DevDiv_Projects/Roslyn")>
        Public Async Function TestEditorBrowsable_ObjectCreation_BrowsableMixed() As Task
            Dim markup = <Text><![CDATA[
Class Program
    Sub Main(args As String())
        Dim x = New C($$
    End Sub
End Class
]]></Text>.Value

            Dim referencedCode = <Text><![CDATA[
Public Class C
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)>
    Public Sub New(x As Integer)
    End Sub

    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Sub New(x As Integer, y As Integer)
    End Sub
End Class
]]></Text>.Value

            Dim expectedOrderedItemsMetadataReference = New List(Of SignatureHelpTestItem)()
            expectedOrderedItemsMetadataReference.Add(New SignatureHelpTestItem("C(x As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Dim expectedOrderedItemsSameSolution = New List(Of SignatureHelpTestItem)()
            expectedOrderedItemsSameSolution.Add(New SignatureHelpTestItem("C(x As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))
            expectedOrderedItemsSameSolution.Add(New SignatureHelpTestItem("C(x As Integer, y As Integer)", String.Empty, String.Empty, currentParameterIndex:=0))

            Await TestSignatureHelpInEditorBrowsableContextsAsync(markup:=markup,
                                                       referencedCode:=referencedCode,
                                                       expectedOrderedItemsMetadataReference:=expectedOrderedItemsMetadataReference,
                                                       expectedOrderedItemsSameSolution:=expectedOrderedItemsSameSolution,
                                                       sourceLanguage:=LanguageNames.VisualBasic,
                                                       referencedLanguage:=LanguageNames.VisualBasic)
        End Function
#End Region
    End Class
End Namespace
