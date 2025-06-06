﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.VisualBasic.Completion.Providers

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.UnitTests.Completion.CompletionProviders
    <Trait(Traits.Feature, Traits.Features.Completion)>
    Public Class CompletionListTagCompletionProviderTests
        Inherits AbstractVisualBasicCompletionProviderTests

        <Fact>
        Public Async Function TestEditorBrowsable_EnumTypeDotMemberAlways() As Task
            Dim markup = <Text><![CDATA[
Class P
    Sub S()
        Dim d As Color = $$
    End Sub
End Class</a>
]]></Text>.Value
            Dim referencedCode = <Text><![CDATA[
''' <completionlist cref="Color"/>
Public Class Color
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Always)>
    Public Shared X as Integer = 3
    Public Shared Y as Integer = 4
End Class

]]></Text>.Value
            Await VerifyItemInEditorBrowsableContextsAsync(
                markup:=markup,
                referencedCode:=referencedCode,
                item:="Color.X",
                expectedSymbolsSameSolution:=1,
                expectedSymbolsMetadataReference:=1,
                sourceLanguage:=LanguageNames.VisualBasic,
                referencedLanguage:=LanguageNames.VisualBasic)
        End Function

        <Fact>
        Public Async Function TestEditorBrowsable_EnumTypeDotMemberNever() As Task
            Dim markup = <Text><![CDATA[
Class P
    Sub S()
        Dim d As Color = $$
    End Sub
End Class</a>
]]></Text>.Value
            Dim referencedCode = <Text><![CDATA[
 ''' <completionlist cref="Color"/>
Public Class Color
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Shared X as Integer = 3
    Public Shared Y as Integer = 4
End Class
]]></Text>.Value
            Await VerifyItemInEditorBrowsableContextsAsync(
                markup:=markup,
                referencedCode:=referencedCode,
                item:="Color.X",
                expectedSymbolsSameSolution:=1,
                expectedSymbolsMetadataReference:=0,
                sourceLanguage:=LanguageNames.VisualBasic,
                referencedLanguage:=LanguageNames.VisualBasic)
        End Function

        <Fact>
        Public Async Function TestEditorBrowsable_EnumTypeDotMemberAdvanced() As Task
            Dim markup = <Text><![CDATA[
Class P
    Sub S()
        Dim d As Color = $$
    End Sub
End Class</a>
]]></Text>.Value
            Dim referencedCode = <Text><![CDATA[
''' <completionlist cref="Color"/>
Public Class Color
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)>
    Public Shared X as Integer = 3
    Public Shared Y as Integer = 4
End Class
]]></Text>.Value

            HideAdvancedMembers = True

            Await VerifyItemInEditorBrowsableContextsAsync(
                markup:=markup,
                referencedCode:=referencedCode,
                item:="Color.X",
                expectedSymbolsSameSolution:=1,
                expectedSymbolsMetadataReference:=0,
                sourceLanguage:=LanguageNames.VisualBasic,
                referencedLanguage:=LanguageNames.VisualBasic)

            HideAdvancedMembers = False

            Await VerifyItemInEditorBrowsableContextsAsync(
                markup:=markup,
                referencedCode:=referencedCode,
                item:="Color.X",
                expectedSymbolsSameSolution:=1,
                expectedSymbolsMetadataReference:=1,
                sourceLanguage:=LanguageNames.VisualBasic,
                referencedLanguage:=LanguageNames.VisualBasic)
        End Function

        <Fact>
        Public Async Function TestTriggeredOnOpenParen() As Task
            Dim markup = <Text><![CDATA[
Module Program
    Sub Main(args As String())
        ' type after this line
        Bar($$
    End Sub
 
    Sub Bar(f As Color)
    End Sub
End Module
 
''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class

]]></Text>.Value

            Await VerifyItemExistsAsync(markup, "Color.X", usePreviousCharAsTrigger:=True)
            Await VerifyItemExistsAsync(markup, "Color.Y", usePreviousCharAsTrigger:=True)
        End Function

        <Fact>
        Public Async Function TestRightSideOfAssignment() As Task
            Dim markup = <Text><![CDATA[
Module Program
    Sub Main(args As String())
        Dim x as Color
        x = $$
    End Sub
End Module
 
''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class
]]></Text>.Value

            Await VerifyItemExistsAsync(markup, "Color.X", usePreviousCharAsTrigger:=True)
            Await VerifyItemExistsAsync(markup, "Color.Y", usePreviousCharAsTrigger:=True)
        End Function

        <Fact>
        Public Async Function TestDoNotCrashInObjectInitializer() As Task
            Dim markup = <Text><![CDATA[
Module Program
    Sub Main(args As String())
        Dim z = New Goo() With {.z$$ }
    End Sub

    Class Goo
        Property A As Integer
            Get

            End Get
            Set(value As Integer)

            End Set
        End Property
    End Class
End Module
]]></Text>.Value

            Await VerifyNoItemsExistAsync(markup)
        End Function

        <Fact>
        Public Async Function TestInYieldReturn() As Task
            Dim markup = <Text><![CDATA[
Imports System
Imports System.Collections.Generic

''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class


Class C
    Iterator Function M() As IEnumerable(Of Color)
        Yield $$
    End Function
End Class
]]></Text>.Value

            Await VerifyItemExistsAsync(markup, "Color.X")
        End Function

        <Fact>
        Public Async Function TestInAsyncMethodReturnStatement() As Task
            Dim markup = <Text><![CDATA[
Imports System.Threading.Tasks

''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class
Class C
    Async Function M() As Task(Of Color)
        Await Task.Delay(1)
        Return $$
    End Function
End Class
]]></Text>.Value

            Await VerifyItemExistsAsync(markup, "Color.X")
        End Function

        <Fact>
        Public Async Function TestInIndexedProperty() As Task
            Dim markup = <Text><![CDATA[
Module Module1

''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class

    Public Class MyClass1
        Public WriteOnly Property MyProperty(ByVal val1 As Color) As Boolean
            Set(ByVal value As Boolean)

            End Set
        End Property

        Public Sub MyMethod(ByVal val1 As Color)

        End Sub
    End Class

    Sub Main()
        Dim var As MyClass1 = New MyClass1
        ' MARKER
        var.MyMethod(Color.X)
        var.MyProperty($$Color.Y) = True
    End Sub

End Module
]]></Text>.Value

            Await VerifyItemExistsAsync(markup, "Color.Y")
        End Function

        <Fact>
        Public Async Function TestFullyQualified() As Task
            Dim markup = <Text><![CDATA[
Namespace ColorNamespace
    ''' <completionlist cref="Color"/>
    Public Class Color
        Public Shared X as Integer = 3
        Public Shared Property Y as Integer = 4
    End Class
End Namespace

Class C
    Public Sub M(day As ColorNamespace.Color)
        M($$)
    End Sub

End Class
]]></Text>.Value
            Await VerifyItemExistsAsync(markup, "ColorNamespace.Color.X")
            Await VerifyItemExistsAsync(markup, "ColorNamespace.Color.Y")
        End Function

        <Fact>
        Public Async Function TestTriggeredForNamedArgument() As Task
            Dim markup = <Text><![CDATA[
Class C
    Public Sub M(day As Color)
        M(day:=$$)
    End Sub
''' <completionlist cref="Color"/>
Public Class Color
    Public Shared X as Integer = 3
    Public Shared Property Y as Integer = 4
End Class

End Class
]]></Text>.Value
            Await VerifyItemExistsAsync(markup, "Color.X", usePreviousCharAsTrigger:=True)
        End Function

        <Fact>
        Public Async Function TestNotInObjectCreation() As Task
            Dim markup = <Text><![CDATA[
''' <completionlist cref="Program"/>
Class Program
    Public Shared Goo As Integer

    Sub Main(args As String())
        Dim p As Program = New $$
    End Sub
End Class
]]></Text>.Value
            Await VerifyItemIsAbsentAsync(markup, "Program.Goo")
        End Function

        <Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/954694")>
        Public Async Function TestAnyAccessibleMember() As Task
            Dim markup = <Text><![CDATA[
Public Class Program
     Private Shared field1 As Integer
 
    ''' <summary>
    ''' </summary>
    ''' <completionList cref="Program"></completionList>
    Public Class Program2
Public Async Function TestM() As Task
            Dim obj As Program2 =$$
        End Sub
    End Class
End Class
]]></Text>.Value
            Await VerifyItemExistsAsync(markup, "Program.field1")
        End Function

        <Fact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/815963")>
        Public Async Function TestLocalNoAs() As Task
            Dim markup = <Text><![CDATA[
Enum E
    A
End Enum
 
Class C
    Sub M()
        Const e As E = e$$
    End Sub
End Class
]]></Text>.Value
            Await VerifyItemIsAbsentAsync(markup, "e As E")
        End Function

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/3518")>
        Public Async Function TestNotInTrivia() As Task
            Dim markup = <Text><![CDATA[
Class C
    Sub Test()
        M(Type2.A)
        ' $$
    End Sub

    Private Sub M(a As Type1)
        Throw New NotImplementedException()
    End Sub
End Class
''' <completionlist cref="Type2"/>
Public Class Type1
End Class

Public Class Type2
    Public Shared A As Type1
    Public Shared B As Type1
End Class
]]></Text>.Value
            Await VerifyNoItemsExistAsync(markup)
        End Function

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/3518")>
        Public Async Function TestNotAfterInvocationWithCompletionListTagTypeAsFirstParameter() As Task
            Dim markup = <Text><![CDATA[
Class C
    Sub Test()
        M(Type2.A)
        $$
    End Sub

    Private Sub M(a As Type1)
        Throw New NotImplementedException()
    End Sub
End Class
''' <completionlist cref="Type2"/>
Public Class Type1
End Class

Public Class Type2
    Public Shared A As Type1
    Public Shared B As Type1
End Class
]]></Text>.Value
            Await VerifyNoItemsExistAsync(markup)
        End Function

        <Fact, WorkItem("https://github.com/dotnet/roslyn/issues/18787")>
        Public Async Function NotAfterDot() As Task
            Dim markup = <Text><![CDATA[
Public Class Program
     Private Shared field1 As Integer
 
    ''' <summary>
    ''' </summary>
    ''' <completionList cref="Program"></completionList>
    Public Class Program2
Public Async Function TestM() As Task
            Dim obj As Program2 = Program.$$
        End Sub
    End Class
End Class
]]></Text>.Value
            Await VerifyNoItemsExistAsync(markup)
        End Function

        Friend Overrides Function GetCompletionProviderType() As Type
            Return GetType(CompletionListTagCompletionProvider)
        End Function
    End Class
End Namespace
