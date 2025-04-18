﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Roslyn.LanguageServer.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Class representing a set of changes to a single text document.
/// <para>
/// See the <see href="https://microsoft.github.io/language-server-protocol/specifications/specification-current/#textDocumentEdit">Language Server Protocol specification</see> for additional information.
/// </para>
/// </summary>
internal sealed class TextDocumentEdit
{
    /// <summary>
    /// Gets or sets a document identifier indication which document to apply the edits to.
    /// </summary>
    [JsonPropertyName("textDocument")]
    [JsonRequired]
    public OptionalVersionedTextDocumentIdentifier TextDocument
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets the array of edits to be applied to the document.
    /// </summary>
    /// <para>
    /// Use of <see cref="AnnotatedTextEdit"/> is guarded by the capability <see cref="WorkspaceEditSetting.ChangeAnnotationSupport"/>.
    /// </para>
    /// <remarks>
    /// Since LSP 3.16
    /// </remarks>
    [JsonPropertyName("edits")]
    [JsonRequired]
    public SumType<TextEdit, AnnotatedTextEdit>[] Edits { get; set; }
}
