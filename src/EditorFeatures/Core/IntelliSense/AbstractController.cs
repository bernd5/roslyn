﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Implementation.IntelliSense;

internal abstract class AbstractController<TSession, TModel, TPresenterSession, TEditorSession> : IController<TModel>
    where TSession : class, ISession<TModel>
    where TPresenterSession : IIntelliSensePresenterSession
{
    protected readonly IThreadingContext ThreadingContext;
    protected readonly IGlobalOptionService GlobalOptions;
    protected readonly ITextView TextView;
    protected readonly ITextBuffer SubjectBuffer;
    protected readonly IIntelliSensePresenter<TPresenterSession, TEditorSession> Presenter;
    protected readonly IDocumentProvider DocumentProvider;

    private readonly IAsynchronousOperationListener _asyncListener;
    private readonly string _asyncOperationId;

    // Null when we absolutely know we don't have any sort of item computation going on. Non
    // null the moment we think we start computing state. Null again once we decide we can
    // stop.
    protected TSession sessionOpt;

    protected bool IsSessionActive => sessionOpt != null;

    protected AbstractController(
        IGlobalOptionService globalOptions,
        IThreadingContext threadingContext,
        ITextView textView,
        ITextBuffer subjectBuffer,
        IIntelliSensePresenter<TPresenterSession, TEditorSession> presenter,
        IAsynchronousOperationListener asyncListener,
        IDocumentProvider documentProvider,
        string asyncOperationId)
    {
        this.GlobalOptions = globalOptions;
        ThreadingContext = threadingContext;
        this.TextView = textView;
        this.SubjectBuffer = subjectBuffer;
        this.Presenter = presenter;
        _asyncListener = asyncListener;
        this.DocumentProvider = documentProvider;
        _asyncOperationId = asyncOperationId;

        this.TextView.Closed += OnTextViewClosed;

        // Caret position changed only fires if the caret is explicitly moved.  It doesn't fire
        // when the caret is moved because of text change events.
        this.TextView.Caret.PositionChanged += this.OnCaretPositionChanged;
        this.TextView.TextBuffer.PostChanged += this.OnTextViewBufferPostChanged;
    }

    internal abstract void OnModelUpdated(TModel result, bool updateController);
    internal abstract void OnTextViewBufferPostChanged(object sender, EventArgs e);
    internal abstract void OnCaretPositionChanged(object sender, EventArgs e);

    private void OnTextViewClosed(object sender, EventArgs e)
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        DismissSessionIfActive();

        this.TextView.Closed -= OnTextViewClosed;
        this.TextView.Caret.PositionChanged -= this.OnCaretPositionChanged;
        this.TextView.TextBuffer.PostChanged -= this.OnTextViewBufferPostChanged;
    }

    public Task WaitForModelComputation_ForTestingPurposesOnlyAsync()
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        VerifySessionIsActive();
        return sessionOpt.WaitForModelComputation_ForTestingPurposesOnlyAsync();
    }

    void IController<TModel>.OnModelUpdated(TModel result, bool updateController)
    {
        // This is only called from the model computation if it was not cancelled.  And if it was 
        // not cancelled then we must have a pointer to it (as well as the presenter session).
        this.ThreadingContext.ThrowIfNotOnUIThread();
        VerifySessionIsActive();

        this.OnModelUpdated(result, updateController);
    }

    IAsyncToken IController<TModel>.BeginAsyncOperation(string name, object tag, string filePath, int lineNumber)
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        VerifySessionIsActive();
        name = String.IsNullOrEmpty(name)
            ? _asyncOperationId
            : $"{_asyncOperationId} - {name}";
        return _asyncListener.BeginAsyncOperation(name, tag, filePath: filePath, lineNumber: lineNumber);
    }

    protected void VerifySessionIsActive()
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        Contract.ThrowIfFalse(IsSessionActive);
    }

    protected void VerifySessionIsInactive()
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        Contract.ThrowIfTrue(IsSessionActive);
    }

    protected void DismissSessionIfActive()
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        if (IsSessionActive)
        {
            this.StopModelComputation();
        }
    }

    public void StopModelComputation()
    {
        this.ThreadingContext.ThrowIfNotOnUIThread();
        VerifySessionIsActive();

        // Make a local copy so that we won't do anything that causes us to recurse and try to
        // dismiss this again.
        var localSession = sessionOpt;
        sessionOpt = null;
        localSession.Stop();
    }
}
