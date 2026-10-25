// InstallerTaskStore.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.InstallerActions;

using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Versioning;
#endif

/// <summary>Accesses Task Scheduler without treating access errors as a missing task or starting it.</summary>
#if NET
[SupportedOSPlatform("windows")]
#endif
internal static class InstallerTaskStore
{
    private const string ServiceId = "Schedule.Service";
    private const string ConnectMethod = "Connect";
    private const string GetFolderMethod = "GetFolder";
    private const string GetTaskMethod = "GetTask";
    private const string RegisterMethod = "RegisterTask";
    private const string DeleteMethod = "DeleteTask";
    private const string XmlProperty = "Xml";
    private const string RootFolder = @"\";
    private const string TaskName = "SnoopMCP Host";
    private const int NotFound = unchecked((int) 0x80070002);
    private const int CreateOrUpdate = 6;
    private const int InteractiveToken = 3;

    internal static string? Read()
    {
        string? result = null;
        WithFolder(folder =>
        {
            object? task = null;
            try
            {
                task = Invoke(folder, GetTaskMethod, TaskName);
                result = task.GetType().InvokeMember(XmlProperty, BindingFlags.GetProperty, null, task, null,
                    CultureInfo.InvariantCulture) as string ?? throw new InvalidOperationException("Task Scheduler returned no task definition.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException?.HResult == NotFound)
            {
                // Only an explicitly missing task is safe to treat as absent.
            }
            catch (Exception ex) when (ex.HResult == NotFound)
            {
                // CLR versions map missing COM tasks to either COMException or FileNotFoundException.
            }
            finally
            {
                Release(task);
            }
        });
        return result;
    }

    internal static void Write(string xml)
    {
        xml = xml ?? throw new ArgumentNullException(nameof(xml));
        WithFolder(folder => Release(Invoke(folder, RegisterMethod, TaskName, xml, CreateOrUpdate,
            null, null, InteractiveToken, null)));
    }

    internal static void Remove() => WithFolder(folder => Invoke(folder, DeleteMethod, TaskName, 0));

    private static void WithFolder(Action<object> action)
    {
        Type type = Type.GetTypeFromProgID(ServiceId) ?? throw new InvalidOperationException("Task Scheduler is unavailable.");
        object service = Activator.CreateInstance(type) ?? throw new InvalidOperationException("Could not connect to Task Scheduler.");
        object? folder = null;
        try
        {
            Invoke(service, ConnectMethod);
            folder = Invoke(service, GetFolderMethod, RootFolder);
            action(folder);
        }
        finally
        {
            Release(folder);
            Release(service);
        }
    }

    private static object Invoke(object target, string name, params object?[] arguments)
        => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, arguments, CultureInfo.InvariantCulture)
           ?? new object();

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
