﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Host;

[Obsolete("API is no longer available", error: true)]
public interface ITemporaryStorageService : IWorkspaceService
{
    ITemporaryStreamStorage CreateTemporaryStreamStorage(CancellationToken cancellationToken = default);
    ITemporaryTextStorage CreateTemporaryTextStorage(CancellationToken cancellationToken = default);
}

/// <summary>
/// API to allow a client to write data to memory-mapped-file storage (allowing it to be shared across processes).
/// </summary>
internal interface ITemporaryStorageServiceInternal : IWorkspaceService
{
    /// <summary>
    /// Write the provided <paramref name="stream"/> to a new memory-mapped-file.  Returns a handle to the data that can
    /// be used to identify the data across processes allowing it to be read back in in any process.
    /// </summary>
    /// <remarks>
    /// This type is primarily used to allow dumping metadata to disk.  This then allowing them to be read in by mapping
    /// their data into types like <see cref="AssemblyMetadata"/>.  It also allows them to be read in by our server
    /// process, without having to transmit the data over the wire.
    /// <para/> Note: The stream provided must support <see cref="Stream.Length"/>.  The stream will also be reset to
    /// <see cref="Stream.Position"/> <code>0</code> within this method.  The caller does not need to reset the stream
    /// itself.
    /// </remarks>
    ITemporaryStorageStreamHandle WriteToTemporaryStorage(Stream stream, CancellationToken cancellationToken);

    /// <summary>
    /// Write the provided <paramref name="text"/> to a new memory-mapped-file.  Returns a handle to the data that can
    /// be used to identify the data across processes allowing it to be read back in in any process.
    /// </summary>
    /// <remarks>
    /// This type is primarily used to allow dumping source texts to disk.  This then allowing them to be read in by
    /// mapping their data into types like <see cref="RecoverableTextAndVersion.RecoverableText"/>.  It also allows them
    /// to be read in by our server process, without having to transmit the data over the wire.
    /// </remarks>
    ITemporaryStorageTextHandle WriteToTemporaryStorage(SourceText text, CancellationToken cancellationToken);

    /// <inheritdoc cref="WriteToTemporaryStorage(SourceText, CancellationToken)"/>"/>
    Task<ITemporaryStorageTextHandle> WriteToTemporaryStorageAsync(SourceText text, CancellationToken cancellationToken);
}
