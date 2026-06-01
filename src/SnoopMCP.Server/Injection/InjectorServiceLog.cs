// InjectorServiceLog.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Injection;

using Microsoft.Extensions.Logging;

internal static partial class InjectorServiceLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Payload injected into pid {processId} on pipe {pipeName}.")]
    public static partial void LogInjectionSucceeded(this ILogger logger, int processId, string pipeName);
}
