﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Editor.BackgroundWorkIndicator;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.Notification;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename;

internal abstract partial class AbstractRenameCommandHandler : ICommandHandler<RenameCommandArgs>
{
    public CommandState GetCommandState(RenameCommandArgs args)
    {
        var caretPoint = args.TextView.GetCaretPoint(args.SubjectBuffer);
        if (!caretPoint.HasValue)
        {
            return CommandState.Unspecified;
        }

        if (!CanRename(args))
        {
            return CommandState.Unspecified;
        }

        return CommandState.Available;
    }

    public bool ExecuteCommand(RenameCommandArgs args, CommandExecutionContext context)
    {
        if (!CanRename(args))
        {
            return false;
        }

        var token = listener.BeginAsyncOperation(nameof(ExecuteCommand));
        _ = ExecuteCommandAsync(args).CompletesAsyncOperation(token);
        return true;
    }

    private async Task ExecuteCommandAsync(RenameCommandArgs args)
    {
        threadingContext.ThrowIfNotOnUIThread();

        if (!args.SubjectBuffer.TryGetWorkspace(out var workspace))
        {
            return;
        }

        var caretPoint = args.TextView.GetCaretPoint(args.SubjectBuffer);
        if (!caretPoint.HasValue)
        {
            await ShowErrorDialogAsync(workspace, FeaturesResources.You_must_rename_an_identifier).ConfigureAwait(false);
            return;
        }

        // If there is already an active session, commit it first
        if (renameService.ActiveSession != null)
        {
            if (renameService.ActiveSession.IsCommitInProgress)
            {
                return;
            }

            if (renameService.ActiveSession.TryGetContainingEditableSpan(caretPoint.Value, out _))
            {
                // Is the caret within any of the rename fields in this buffer?
                // If so, focus the dashboard
                SetFocusToAdornment(args.TextView);
                return;
            }
            else
            {
                // Otherwise, cancel the existing session and start a new one.
                CancelRenameSession();
            }
        }

        var backgroundWorkIndicatorFactory = workspace.Services.GetRequiredService<IBackgroundWorkIndicatorFactory>();
        using var context = backgroundWorkIndicatorFactory.Create(
            args.TextView,
            args.TextView.GetTextElementSpan(caretPoint.Value),
            EditorFeaturesResources.Finding_token_to_rename);

        var cancellationToken = context.UserCancellationToken;

        var document = await args
            .SubjectBuffer
            .CurrentSnapshot
            .GetFullyLoadedOpenDocumentInCurrentContextWithChangesAsync(context)
            .ConfigureAwait(false);

        if (document == null)
        {
            await ShowErrorDialogAsync(workspace, FeaturesResources.You_must_rename_an_identifier).ConfigureAwait(false);
            return;
        }

        var selectedSpans = args.TextView.Selection.GetSnapshotSpansOnBuffer(args.SubjectBuffer);

        // Now make sure the entire selection is contained within that token.
        // There can be zero selectedSpans in projection scenarios.
        if (selectedSpans.Count != 1)
        {
            await ShowErrorDialogAsync(workspace, FeaturesResources.You_must_rename_an_identifier).ConfigureAwait(false);
            return;
        }

        var sessionInfo = await renameService.StartInlineSessionAsync(document, selectedSpans.Single().Span.ToTextSpan(), cancellationToken).ConfigureAwait(false);
        if (!sessionInfo.CanRename)
        {
            await ShowErrorDialogAsync(workspace, sessionInfo.LocalizedErrorMessage).ConfigureAwait(false);
            return;
        }
    }

    private static bool CanRename(RenameCommandArgs args)
    {
        return args.SubjectBuffer.TryGetWorkspace(out var workspace) &&
            workspace.CanApplyChange(ApplyChangesKind.ChangeDocument) &&
            args.SubjectBuffer.CurrentSnapshot.GetOpenDocumentInCurrentContextWithChanges() is Document document &&
            document.GetLanguageService<IEditorInlineRenameService>() is IEditorInlineRenameService inlineRenameService &&
            inlineRenameService.IsEnabled &&
            args.SubjectBuffer.SupportsRename() && !args.SubjectBuffer.IsInLspEditorContext();
    }

    private async Task ShowErrorDialogAsync(Workspace workspace, string message)
    {
        await threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync();
        var notificationService = workspace.Services.GetService<INotificationService>();
        notificationService.SendNotification(message, title: EditorFeaturesResources.Rename, severity: NotificationSeverity.Error);
    }
}
