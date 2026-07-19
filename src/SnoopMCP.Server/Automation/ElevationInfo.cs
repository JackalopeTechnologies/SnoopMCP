// ElevationInfo.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.Host.Automation;

using System.Runtime.InteropServices;
using System.Security.Principal;

/// <summary>
/// Reports whether the current process is elevated, and whether it could become elevated via UAC.
/// Used on startup to decide whether to relaunch the host through the elevated autostart task (see
/// <c>App.xaml.cs</c>) — driving/injecting an elevated target requires the host itself to be elevated.
/// </summary>
public static class ElevationInfo
{
    // TokenElevationType info class — see the Windows SDK's TOKEN_INFORMATION_CLASS enum.
    private const int TokenElevationTypeInfoClass = 18;

    /// <summary>True when the current process token is a member of the Administrators group.</summary>
    public static bool IsElevated()
    {
        using WindowsIdentity id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// True when already elevated, or when the account is an administrator running with a UAC-filtered
    /// (<see cref="TokenElevationType.Limited"/>) token — a Limited token can be elevated via UAC.
    /// </summary>
    public static bool CanElevate()
    {
        return IsElevated() || QueryElevationType() == TokenElevationType.Limited;
    }

    private static TokenElevationType QueryElevationType()
    {
        using WindowsIdentity id = WindowsIdentity.GetCurrent();
        TokenElevationType result;
        nint buffer = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
        try
        {
            result = GetTokenInformation(id.Token, TokenElevationTypeInfoClass, buffer, Marshal.SizeOf<int>(), out _)
                ? (TokenElevationType) Marshal.ReadInt32(buffer)
                : TokenElevationType.Default;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return result;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(nint tokenHandle, int tokenInformationClass, nint tokenInformation, int tokenInformationLength, out int returnLength);

    private enum TokenElevationType
    {
        Default = 1,
        Full = 2,
        Limited = 3
    }
}
