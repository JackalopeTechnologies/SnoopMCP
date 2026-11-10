// UpgradeActions.cs
// Copyright © 2012–Present Jackalope Technologies, Inc. and Doug Gerard.
// SPDX-License-Identifier: MIT
// Licensed under the MIT License. See LICENSE in the repository root.

namespace SnoopMCP.InstallerActions;

using System.IO;
using WixToolset.Dtf.WindowsInstaller;
#if NET
using System.Runtime.Versioning;
#endif

/// <summary>Installer transaction hooks protecting files from the legacy uninstaller.</summary>
#if NET
[SupportedOSPlatform("windows")]
#endif
public static class UpgradeActions
{
    private const string RelatedProducts = "WIX_UPGRADE_DETECTED";
    private const string SavedInstallDirectory = "INSTALLDIRSAVED";
    private const string InstallDirectory = "INSTALLFOLDER";
    private const string SnapshotDirectory = "Snapshot";
    private const string BackupParent = "SnoopMCP.Upgrade";
    private const string RestoreAction = "RestoreUpgradeState";
    private const string CleanupAction = "CleanupUpgradeState";
    private const string DirectoryQuery = "SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`";
    private const string FileQuery = "SELECT `File`.`FileName`, `Component`.`Directory_` FROM `File`, `Component` WHERE `File`.`Component_` = `Component`.`Component`";
    private const string RootDirectoryName = ".";
    private const string HostExecutableName = "SnoopMCP.Host.exe";
    private static readonly char[] smProductSeparator = [';'];
    private static readonly char[] smSourceSeparator = [':'];
    private static readonly char[] smLongNameSeparator = ['|'];

    /// <summary>Runs before old-product removal; failure aborts the upgrade before destructive cleanup.</summary>
    [CustomAction]
    public static ActionResult PrepareUpgradeState(Session session)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        return Run(session, () => Prepare(session));
    }

    /// <summary>Restores user files after the new package's files have been installed.</summary>
    [CustomAction]
    public static ActionResult RestoreUpgradeState(Session session)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        return Run(session, () => Snapshot(session).Restore());
    }

    /// <summary>Restores user files after failure or cancellation, including old-product removal failures.</summary>
    [CustomAction]
    public static ActionResult RestoreInterruptedUpgradeState(Session session)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        return Run(session, () =>
        {
            string data = session[RestoreAction];
            if (!string.IsNullOrEmpty(data))
            {
                string path = new CustomActionData(data)[SnapshotDirectory];
                var snapshot = new UpgradeSnapshot(path);
                snapshot.Restore();
                new UpgradeTaskSnapshot(path).Restore(InstallerTaskStore.Read, InstallerTaskStore.Write, InstallerTaskStore.Remove);
            }
        });
    }

    /// <summary>Deletes the backup only after the installation transaction commits successfully.</summary>
    [CustomAction]
    public static ActionResult CleanupUpgradeState(Session session)
    {
        session = session ?? throw new ArgumentNullException(nameof(session));
        return Run(session, () =>
        {
            new UpgradeTaskSnapshot(session.CustomActionData[SnapshotDirectory]).Delete();
            Snapshot(session).Delete();
        });
    }

    private static UpgradeSnapshot Snapshot(Session session)
        => new(session.CustomActionData[SnapshotDirectory]);

    private static void Prepare(Session session)
    {
        string installRoot = session[SavedInstallDirectory];
        if (string.IsNullOrEmpty(installRoot))
        {
            installRoot = session[InstallDirectory];
        }
        var packageFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string product in session[RelatedProducts].Split(smProductSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var installed = new ProductInstallation(product);
            using var database = new Database(installed.LocalPackage, DatabaseOpenMode.ReadOnly);
            AddPackageFiles(database, installRoot, packageFiles);
        }
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string path = Path.Combine(local, BackupParent, Guid.NewGuid().ToString("N"));
        var snapshot = new UpgradeSnapshot(path);
        snapshot.Capture(LegacyClientPaths.Configurations(profile, roaming),
            LegacyClientPaths.Skills(profile).Concat(new[] { installRoot }), packageFiles);
        new UpgradeTaskSnapshot(path).Capture(Path.Combine(installRoot, HostExecutableName), InstallerTaskStore.Read);
        var data = new CustomActionData { [SnapshotDirectory] = path };
        session[RestoreAction] = data.ToString();
        session[CleanupAction] = data.ToString();
        session.Log("Preserved upgrade state at {0}. It will be retained if rollback is needed.", path);
    }

    private static void AddPackageFiles(Database database, string root, HashSet<string> files)
    {
        IList<string> rows = database.ExecuteStringQuery(DirectoryQuery);
        var directories = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
        for (int index = 0; index < rows.Count; index += 3)
        {
            directories.Add(rows[index], new KeyValuePair<string, string>(rows[index + 1], LongName(rows[index + 2])));
        }
        IList<string> fileRows = database.ExecuteStringQuery(FileQuery);
        for (int index = 0; index < fileRows.Count; index += 2)
        {
            string directory = fileRows[index + 1];
            string relative = LongName(fileRows[index]);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (directory != InstallDirectory && directories.TryGetValue(directory, out var parent) && visited.Add(directory))
            {
                if (parent.Value != RootDirectoryName)
                {
                    relative = Path.Combine(parent.Value, relative);
                }
                directory = parent.Key;
            }
            if (directory == InstallDirectory)
            {
                files.Add(Path.GetFullPath(Path.Combine(root, relative)));
            }
        }
    }

    private static string LongName(string value) => value.Split(smSourceSeparator)[0].Split(smLongNameSeparator).Last();

    private static ActionResult Run(Session session, Action action)
    {
        ActionResult result = ActionResult.Success;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            session.Log("SnoopMCP upgrade preservation failed: {0}. Backup files have been retained.", ex);
            result = ActionResult.Failure;
        }
        return result;
    }
}
