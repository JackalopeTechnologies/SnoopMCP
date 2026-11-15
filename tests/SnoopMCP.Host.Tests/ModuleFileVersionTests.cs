// ModuleFileVersionTests.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Tests;

using System.Diagnostics;
using System.IO;
using Injection;
using Xunit;

public sealed class ModuleFileVersionTests
{
    [Fact]
    public void Read_ExistingAssembly_PreservesFileVersion()
    {
        string path = typeof(ProcessEnumerator).Assembly.Location;
        string? expected = FileVersionInfo.GetVersionInfo(path).FileVersion;

        string version = ModuleFileVersion.Read(path);

        Assert.NotNull(expected);
        Assert.Equal(expected, version);
    }

    [Fact]
    public void Read_RemovedModuleFile_ReturnsUnknown()
    {
        string path = Path.GetTempFileName();
        File.Delete(path);

        string version = ModuleFileVersion.Read(path);

        Assert.Equal(ModuleFileVersion.Unknown, version);
    }

    [Fact]
    public void Read_RemovedRuntimeDirectory_ReturnsUnknown()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "hostfxr.dll");

        string version = ModuleFileVersion.Read(path);

        Assert.Equal(ModuleFileVersion.Unknown, version);
    }

    [Fact]
    public void Read_FileWithoutVersionMetadata_ReturnsUnknown()
    {
        string path = Path.GetTempFileName();
        try
        {
            string version = ModuleFileVersion.Read(path);

            Assert.Equal(ModuleFileVersion.Unknown, version);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
