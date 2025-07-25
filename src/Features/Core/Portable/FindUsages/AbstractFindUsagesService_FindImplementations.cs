﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Collections;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Remote;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.FindUsages;

internal abstract partial class AbstractFindUsagesService
{
    public async Task FindImplementationsAsync(
        IFindUsagesContext context, Document document, int position, OptionsProvider<ClassificationOptions> classificationOptions, CancellationToken cancellationToken)
    {
        // If this is a symbol from a metadata-as-source project, then map that symbol back to a symbol in the primary workspace.
        var symbolAndProject = await FindUsagesHelpers.GetRelevantSymbolAndProjectAtPositionAsync(
            document, position, cancellationToken).ConfigureAwait(false);
        if (symbolAndProject is not var (symbol, project))
        {
            await context.ReportNoResultsAsync(
                FeaturesResources.Cannot_navigate_to_the_symbol_under_the_caret, cancellationToken).ConfigureAwait(false);
            return;
        }

        await FindImplementationsAsync(
            context, symbol, project, classificationOptions, cancellationToken).ConfigureAwait(false);
    }

    public static async Task FindImplementationsAsync(
        IFindUsagesContext context, ISymbol symbol, Project project, OptionsProvider<ClassificationOptions> classificationOptions, CancellationToken cancellationToken)
    {
        var solution = project.Solution;
        var client = await RemoteHostClient.TryGetClientAsync(solution.Services, cancellationToken).ConfigureAwait(false);
        if (client != null)
        {
            // Create a callback that we can pass to the server process to hear about the 
            // results as it finds them.  When we hear about results we'll forward them to
            // the 'progress' parameter which will then update the UI.
            var serverCallback = new FindUsagesServerCallback(solution, context, classificationOptions);
            var symbolAndProjectId = SerializableSymbolAndProjectId.Create(symbol, project, cancellationToken);

            await client.TryInvokeAsync<IRemoteFindUsagesService>(
                solution,
                (service, solutionInfo, callbackId, cancellationToken) => service.FindImplementationsAsync(solutionInfo, callbackId, symbolAndProjectId, cancellationToken),
                serverCallback,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Couldn't effectively search in OOP. Perform the search in-process.
            await FindImplementationsInCurrentProcessAsync(
                symbol, project, context, classificationOptions, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task FindImplementationsInCurrentProcessAsync(
        ISymbol symbol, Project project, IFindUsagesContext context, OptionsProvider<ClassificationOptions> classificationOptions, CancellationToken cancellationToken)
    {
        await context.SetSearchTitleAsync(
            string.Format(FeaturesResources._0_implementations,
            FindUsagesHelpers.GetDisplayName(symbol)),
            cancellationToken).ConfigureAwait(false);

        var solution = project.Solution;
        var implementations = await FindSourceImplementationsAsync(solution, symbol, cancellationToken).ConfigureAwait(false);

        if (implementations.IsEmpty)
        {
            await context.ReportNoResultsAsync(FeaturesResources.The_symbol_has_no_implementations, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var implementation in implementations)
        {
            var definitionItem = await implementation.ToClassifiedDefinitionItemAsync(
                classificationOptions, solution, FindReferencesSearchOptions.Default, isPrimary: true, includeHiddenLocations: false, cancellationToken).ConfigureAwait(false);

            await context.OnDefinitionFoundAsync(definitionItem, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ImmutableArray<ISymbol>> FindSourceImplementationsAsync(
        Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        var builder = new HashSet<ISymbol>(SymbolEquivalenceComparer.Instance);

        // If we're in a linked file, try to find all the symbols this links to, and find all the implementations of
        // each of those linked symbols. De-dupe the results so the user only gets unique results.
        var linkedSymbols = await SymbolFinder.FindLinkedSymbolsAsync(
            symbol, solution, cancellationToken).ConfigureAwait(false);

        // Because we're searching linked files, we may get many symbols that are conceptually 
        // 'duplicates' to the user.  Specifically, any symbols that would navigate to the same
        // location do not provide value to the user as selecting any from that set of items 
        // would navigate them to the exact same location.  For this, we use file-paths and spans
        // as those will be the same regardless of how a file is linked or used in shared project
        // scenarios.
        var seenLocations = new HashSet<(string filePath, TextSpan span)>();

        foreach (var linkedSymbol in linkedSymbols)
        {
            var implementations = await FindImplementationsWorkerAsync(
                solution, linkedSymbol, cancellationToken).ConfigureAwait(false);
            foreach (var implementation in implementations)
            {
                if (AddedAllLocations(implementation, seenLocations))
                    builder.Add(implementation);
            }
        }

        // If we didn't find any implementations, then just return the original symbol.
        if (builder.Count == 0)
            builder.Add(symbol.OriginalDefinition);

        return [.. builder];

        static bool AddedAllLocations(ISymbol implementation, HashSet<(string filePath, TextSpan span)> seenLocations)
        {
            foreach (var location in implementation.Locations)
            {
                if (location.IsInSource && !seenLocations.Add((location.SourceTree.FilePath, location.SourceSpan)))
                    return false;
            }

            return true;
        }
    }

    private static async Task<ImmutableArray<ISymbol>> FindImplementationsWorkerAsync(
        Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        var implementations = await FindSourceAndMetadataImplementationsAsync(solution, symbol, cancellationToken).ConfigureAwait(false);

        using var _ = PooledHashSet<ISymbol>.GetInstance(out var result);
        result.AddRange(implementations.Select(s => s.OriginalDefinition));

        // For members, if we've found overrides of the original symbol, then filter out any abstract
        // members these inherit from.  The user has asked for literal implementations, and in the case
        // of an override, including the abstract as well isn't helpful.

        // Make a copy of this list as we mutating it as we proceed.
        var overrides = result.Where(s => s.IsOverride).ToImmutableArray();
        foreach (var ov in overrides)
        {
            for (var overridden = ov.GetOverriddenMember(allowLooseMatch: true);
                 overridden != null;
                 overridden = overridden.GetOverriddenMember(allowLooseMatch: true))
            {
                if (overridden.IsAbstract)
                    result.Remove(overridden.OriginalDefinition);
            }
        }

        return [.. result];
    }

    private static async Task<ImmutableArray<ISymbol>> FindSourceAndMetadataImplementationsAsync(
        Solution solution, ISymbol symbol, CancellationToken cancellationToken)
    {
        if (symbol.IsInterfaceType() || symbol.IsImplementableMember())
        {
            var implementations = await SymbolFinder.FindImplementationsAsync(
                symbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false);

            // It's important we use a HashSet here -- we may have cases in an inheritance hierarchy where more than one method
            // in an overrides chain implements the same interface method, and we want to duplicate those. The easiest way to do it
            // is to just use a HashSet.
            using var _ = PooledHashSet<ISymbol>.GetInstance(out var implementationsAndOverrides);

            foreach (var implementation in implementations)
            {
                implementationsAndOverrides.Add(implementation);

                // FindImplementationsAsync will only return the base virtual/abstract method, not that method and the overrides
                // of the method. We should also include those.
                if (implementation.IsOverridable())
                {
                    var overrides = await SymbolFinder.FindOverridesAsync(
                        implementation, solution, cancellationToken: cancellationToken).ConfigureAwait(false);
                    implementationsAndOverrides.AddRange(overrides);
                }
            }

            return [.. implementationsAndOverrides];
        }
        else if (symbol is INamedTypeSymbol { TypeKind: TypeKind.Class } namedType)
        {
            return ImmutableArray<ISymbol>.CastUp(await SymbolFinder.FindDerivedClassesArrayAsync(
                namedType, solution, transitive: true, cancellationToken: cancellationToken).ConfigureAwait(false));
        }
        else if (symbol.IsOverridable())
        {
            return await SymbolFinder.FindOverridesArrayAsync(
                symbol, solution, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // If a symbol is partial definition, return its implementation part
            var implementationPart = symbol switch
            {
                IMethodSymbol method => method.PartialImplementationPart,
                IPropertySymbol property => property.PartialImplementationPart,
                IEventSymbol ev => ev.PartialImplementationPart,
                _ => symbol,
            };

            return implementationPart is null ? [] : [implementationPart];
        }
    }
}
