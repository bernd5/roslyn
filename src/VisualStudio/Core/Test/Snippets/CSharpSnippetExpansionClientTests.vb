﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.AddImport
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.CSharp.Formatting
Imports Microsoft.CodeAnalysis.Editor.UnitTests.Extensions
Imports Microsoft.CodeAnalysis.Options
Imports Microsoft.CodeAnalysis.Test.Utilities
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.VisualStudio.LanguageServices.Implementation.Snippets
Imports Microsoft.VisualStudio.Text.Editor
Imports Microsoft.VisualStudio.Text.Projection
Imports Roslyn.Test.Utilities

Namespace Microsoft.VisualStudio.LanguageServices.UnitTests.Snippets
    <[UseExportProvider]>
    <Trait(Traits.Feature, Traits.Features.Snippets)>
    Public Class CSharpSnippetExpansionClientTests

        <WpfFact>
        Public Async Function TestAddImport_EmptyDocument() As Task
            Dim originalCode = ""
            Dim namespacesToAdd = {"System"}
            Dim expectedUpdatedCode = "using System;

"

            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_EmptyDocument_SystemAtTop() As Task
            Dim originalCode = ""
            Dim namespacesToAdd = {"First.Alphabetically", "System.Bar"}
            Dim expectedUpdatedCode = "using System.Bar;
using First.Alphabetically;

"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_EmptyDocument_SystemNotSortedToTop() As Task
            Dim originalCode = ""
            Dim namespacesToAdd = {"First.Alphabetically", "System.Bar"}
            Dim expectedUpdatedCode = "using First.Alphabetically;
using System.Bar;

"

            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=False, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_AddsOnlyNewNamespaces() As Task
            Dim originalCode = "using A.B.C;
using D.E.F;
"
            Dim namespacesToAdd = {"D.E.F", "G.H.I"}
            Dim expectedUpdatedCode = "using A.B.C;
using D.E.F;
using G.H.I;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/4457")>
        Public Async Function TestAddImport_InsideNamespace() As Task
            Dim originalCode = "
using A;

namespace N
{
    using B;

    class C
    {
        $$
    }
}"
            Dim namespacesToAdd = {"D"}
            Dim expectedUpdatedCode = "
using A;

namespace N
{
    using B;
    using D;

    class C
    {
        
    }
}"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_AddsOnlyNewAliasAndNamespacePairs() As Task
            Dim originalCode = "using A = B.C;
using D = E.F;
using G = H.I;
"
            Dim namespacesToAdd = {"A = E.F", "D = B.C", "G = H.I", "J = K.L"}
            Dim expectedUpdatedCode = "using A = B.C;
using A = E.F;
using D = B.C;
using D = E.F;
using G = H.I;
using J = K.L;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_DuplicateNamespaceDetectionDoesNotIgnoreCase() As Task
            Dim originalCode = "using A.b.C;
"
            Dim namespacesToAdd = {"a.B.C", "A.B.c"}
            Dim expectedUpdatedCode = "using a.B.C;
using A.b.C;
using A.B.c;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_DuplicateAliasNamespacePairDetectionIgnoresWhitespace1() As Task
            Dim originalCode = "using A = B.C;
"
            Dim namespacesToAdd = {"A  =        B.C"}
            Dim expectedUpdatedCode = "using A = B.C;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_DuplicateAliasNamespacePairDetectionIgnoresWhitespace2() As Task
            Dim originalCode = "using A     =  B.C;
"
            Dim namespacesToAdd = {"A=B.C"}
            Dim expectedUpdatedCode = "using A     =  B.C;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_DuplicateAliasNamespacePairDetectionDoesNotIgnoreCase() As Task
            Dim originalCode = "using A = B.C;
"
            Dim namespacesToAdd = {"a = b.C"}
            Dim expectedUpdatedCode = "using a = b.C;
using A = B.C;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Async Function TestAddImport_OnlyFormatNewImports() As Task
            Dim originalCode = "using A     =  B.C;
using G=   H.I;
"
            Dim namespacesToAdd = {"D        =E.F"}
            Dim expectedUpdatedCode = "using A     =  B.C;
using D = E.F;
using G=   H.I;
"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/44423")>
        Public Async Function TestAddImport_BadNamespaceGetsAdded() As Task
            Dim originalCode = ""
            Dim namespacesToAdd = {"$system"}
            Dim expectedUpdatedCode = "using $system;

"
            Await TestSnippetAddImportsAsync(originalCode, namespacesToAdd, placeSystemNamespaceFirst:=True, expectedUpdatedCode:=expectedUpdatedCode)
        End Function

        <WpfFact>
        Public Sub TestSnippetFormatting_ProjectionBuffer_FullyInSubjectBuffer()
            Dim workspaceXmlWithSubjectBufferDocument =
<Workspace>
    <Project Language=<%= LanguageNames.CSharp %> CommonReferences="true">
        <Document>class C {
    void M()
    {
        {|S1:for (int x = 0; x &lt; length; x++)
{
        $$ 
}|}
    }</Document>
    </Project>
</Workspace>

            Dim surfaceBufferDocument = <Document>&lt;div&gt;
    @[|{|S1:|} |]
&lt;/div&gt;</Document>

            Dim expectedSurfaceBuffer = <SurfaceBuffer>&lt;div&gt;
    @for (int x = 0; x &lt; length; x++)
        {

        } 
&lt;/div&gt;</SurfaceBuffer>

            TestProjectionFormatting(workspaceXmlWithSubjectBufferDocument, surfaceBufferDocument, expectedSurfaceBuffer)
        End Sub

        <WpfFact>
        Public Sub TestSnippetFormatting_ProjectionBuffer_ExpandedIntoSurfaceBuffer()
            Dim workspaceXmlWithSubjectBufferDocument =
<Workspace>
    <Project Language=<%= LanguageNames.CSharp %> CommonReferences="true">
        <Document>class C {
    void M()
    {
        {|S1:for|}
    }</Document>
    </Project>
</Workspace>

            Dim surfaceBufferDocument = <Document>&lt;div&gt;
    @[|{|S1:|} (int x = 0; x &lt; length; x++)
{
        $$
}|]
&lt;/div&gt;</Document>

            Dim expectedSurfaceBuffer = <SurfaceBuffer>&lt;div&gt;
    @for (int x = 0; x &lt; length; x++)
{
        
}
&lt;/div&gt;</SurfaceBuffer>

            TestProjectionFormatting(workspaceXmlWithSubjectBufferDocument, surfaceBufferDocument, expectedSurfaceBuffer)
        End Sub

        <WpfFact>
        Public Sub TestSnippetFormatting_ProjectionBuffer_FullyInSurfaceBuffer()
            Dim workspaceXmlWithSubjectBufferDocument =
<Workspace>
    <Project Language=<%= LanguageNames.CSharp %> CommonReferences="true">
        <Document>class C {
    void M()
    {
        {|S1:|}
    }</Document>
    </Project>
</Workspace>

            Dim surfaceBufferDocument = <Document>&lt;div&gt;
    @[|{|S1:|}for (int x = 0; x &lt; length; x++)
{
        $$
}|]
&lt;/div&gt;</Document>

            Dim expectedSurfaceBuffer = <SurfaceBuffer>&lt;div&gt;
    @for (int x = 0; x &lt; length; x++)
{
        
}
&lt;/div&gt;</SurfaceBuffer>

            TestProjectionFormatting(workspaceXmlWithSubjectBufferDocument, surfaceBufferDocument, expectedSurfaceBuffer)
        End Sub

        Public Sub TestSnippetFormatting_TabSize_3()
            TestFormattingWithTabSize(3)
        End Sub

        <WpfTheory, WorkItem("https://github.com/dotnet/roslyn/issues/4652")>
        <InlineData(3)>
        <InlineData(4)>
        <InlineData(5)>
        Public Sub TestFormattingWithTabSize(tabSize As Integer)
            Dim workspaceXml =
<Workspace>
    <Project Language=<%= LanguageNames.CSharp %> CommonReferences="true">
        <Document>class C {
	void M()
	{
		[|for (int x = 0; x &lt; length; x++)
{
    $$
}|]
	}
}</Document>
    </Project>
</Workspace>

            Dim expectedResult = <Test>class C {
	void M()
	{
		for (int x = 0; x &lt; length; x++)
		{

		}
	}
}</Test>

            Using workspace = EditorTestWorkspace.Create(workspaceXml, openDocuments:=False, composition:=VisualStudioTestCompositions.LanguageServices)
                Dim document = workspace.Documents.Single()
                Dim textBuffer = document.GetTextBuffer()
                Dim editorOptionsService = workspace.GetService(Of EditorOptionsService)()
                Dim editorOptions = editorOptionsService.Factory.GetOptions(textBuffer)

                editorOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, False)
                editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize)
                editorOptions.SetOptionValue(DefaultOptions.IndentSizeOptionId, tabSize)

                Dim expansionClientFactory = workspace.Services.GetRequiredService(Of ISnippetExpansionClientFactory)()
                Dim snippetExpansionClient = expansionClientFactory.GetOrCreateSnippetExpansionClient(
                    textBuffer.AsTextContainer().GetOpenDocumentInCurrentContext(),
                    document.GetTextView(),
                    textBuffer)

                SnippetExpansionClientTestsHelper.TestFormattingAndCaretPosition(snippetExpansionClient, document, expectedResult, tabSize * 3)
            End Using
        End Sub

        Public Sub TestProjectionFormatting(workspaceXmlWithSubjectBufferDocument As XElement, surfaceBufferDocumentXml As XElement, expectedSurfaceBuffer As XElement)
            Using workspace = EditorTestWorkspace.Create(workspaceXmlWithSubjectBufferDocument, composition:=VisualStudioTestCompositions.LanguageServices)
                Dim subjectBufferDocument = workspace.Documents.Single()

                Dim surfaceBufferDocument = workspace.CreateProjectionBufferDocument(
                    surfaceBufferDocumentXml.NormalizedValue,
                    {subjectBufferDocument},
                    options:=ProjectionBufferOptions.WritableLiteralSpans)

                Dim expansionClientFactory = workspace.Services.GetRequiredService(Of ISnippetExpansionClientFactory)()
                Dim snippetExpansionClient = expansionClientFactory.GetOrCreateSnippetExpansionClient(
                    subjectBufferDocument.GetTextBuffer().AsTextContainer().GetOpenDocumentInCurrentContext(),
                    surfaceBufferDocument.GetTextView(),
                    subjectBufferDocument.GetTextBuffer())

                SnippetExpansionClientTestsHelper.TestProjectionBuffer(snippetExpansionClient, surfaceBufferDocument, expectedSurfaceBuffer)
            End Using
        End Sub

        Private Shared Async Function TestSnippetAddImportsAsync(
                markupCode As String,
                namespacesToAdd As String(),
                placeSystemNamespaceFirst As Boolean,
                expectedUpdatedCode As String) As Tasks.Task

            Dim originalCode As String = Nothing
            Dim position As Integer?
            MarkupTestFile.GetPosition(markupCode, originalCode, position)

            Dim workspaceXml = <Workspace>
                                   <Project Language=<%= LanguageNames.CSharp %> CommonReferences="true">
                                       <Document><%= originalCode %></Document>
                                   </Project>
                               </Workspace>

            Dim snippetNode = <Snippet>
                                  <Imports>
                                  </Imports>
                              </Snippet>

            For Each namespaceToAdd In namespacesToAdd
                snippetNode.Element("Imports").Add(<Import>
                                                       <Namespace><%= namespaceToAdd %></Namespace>
                                                   </Import>)
            Next

            Using workspace = EditorTestWorkspace.CreateCSharp(originalCode, composition:=VisualStudioTestCompositions.LanguageServices)
                Dim expansionClientFactory = workspace.Services.GetRequiredService(Of ISnippetExpansionClientFactory)()
                Dim expansionClient = expansionClientFactory.GetOrCreateSnippetExpansionClient(
                    workspace.Documents.Single().GetTextBuffer().AsTextContainer().GetOpenDocumentInCurrentContext(),
                    workspace.Documents.Single().GetTextView(),
                    workspace.Documents.Single().GetTextBuffer())

                Dim document = workspace.CurrentSolution.Projects.Single().Documents.Single()
                Dim addImportOptions = New AddImportPlacementOptions() With
                {
                    .PlaceSystemNamespaceFirst = placeSystemNamespaceFirst
                }

                Dim formattingOptions = CSharpSyntaxFormattingOptions.Default

                Dim updatedDocument = Await expansionClient.GetTestAccessor().LanguageHelper.AddImportsAsync(
                    document,
                    addImportOptions,
                    formattingOptions,
                    If(position, 0),
                    snippetNode,
                    CancellationToken.None)

                AssertEx.EqualOrDiff(expectedUpdatedCode,
                             (Await updatedDocument.GetTextAsync()).ToString())
            End Using
        End Function
    End Class
End Namespace
