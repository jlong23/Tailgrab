using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Tailgrab.Common
{
    /// <summary>
    /// Manages registry schema migrations across application versions.
    /// Tracks the current registry version and executes migration steps sequentially.
    /// </summary>
    public class RegistryMigrationManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Registry key name for storing the current registry schema version.
        /// </summary>
        public const string Registry_Schema_Version = "REGISTRY_SCHEMA_VERSION";

        /// <summary>
        /// List of all registered migrations, ordered by version.
        /// </summary>
        private readonly List<RegistryMigration> _migrations = new List<RegistryMigration>();

        /// <summary>
        /// Registers a new migration step.
        /// </summary>
        /// <param name="fromVersion">The source version (e.g., "1.0.0")</param>
        /// <param name="toVersion">The target version (e.g., "1.1.0")</param>
        /// <param name="migrationAction">The action to execute for this migration</param>
        public void RegisterMigration(string fromVersion, string toVersion, Action migrationAction)
        {
            _migrations.Add(new RegistryMigration
            {
                FromVersion = fromVersion,
                ToVersion = toVersion,
                MigrationAction = migrationAction
            });

            // Keep migrations sorted by target version
            _migrations.Sort((a, b) => CompareVersions(a.ToVersion, b.ToVersion));
        }

        /// <summary>
        /// Gets the current registry schema version stored in the registry.
        /// Returns "0.0.0" if no version is found.
        /// </summary>
        public string GetCurrentRegistryVersion()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(CommonConst.ConfigRegistryPath);
                if (key != null)
                {
                    var version = key.GetValue(Registry_Schema_Version) as string;
                    if (!string.IsNullOrEmpty(version))
                    {
                        logger.Debug($"Current registry schema version: {version}");
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read registry schema version");
            }

            logger.Debug("No registry schema version found, defaulting to 0.0.0");
            return "0.0.0";
        }

        /// <summary>
        /// Sets the registry schema version in the registry.
        /// </summary>
        private void SetRegistryVersion(string version)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(CommonConst.ConfigRegistryPath);
                if (key != null)
                {
                    key.SetValue(Registry_Schema_Version, version, RegistryValueKind.String);
                    logger.Info($"Updated registry schema version to: {version}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to set registry schema version to {version}");
                throw;
            }
        }

        /// <summary>
        /// Executes all pending migrations from the current registry version to the target application version.
        /// </summary>
        /// <param name="targetVersion">The target application version to migrate to</param>
        /// <returns>True if all migrations completed successfully, false otherwise</returns>
        public bool ExecuteMigrations(string targetVersion)
        {
            string currentVersion = GetCurrentRegistryVersion();

            logger.Info($"Starting registry migrations from version {currentVersion} to {targetVersion}");

            // Find all migrations that need to be applied
            var pendingMigrations = GetPendingMigrations(currentVersion, targetVersion);

            if (!pendingMigrations.Any())
            {
                logger.Info("No pending migrations found");
                
                // Still update the version if we're on a newer app version
                if (CompareVersions(currentVersion, targetVersion) < 0)
                {
                    SetRegistryVersion(targetVersion);
                }
                
                return true;
            }

            logger.Info($"Found {pendingMigrations.Count} pending migration(s)");

            // Execute migrations in order
            foreach (var migration in pendingMigrations)
            {
                try
                {
                    logger.Info($"Executing migration: {migration.FromVersion} -> {migration.ToVersion}");
                    migration.MigrationAction();
                    logger.Info($"Successfully completed migration to {migration.ToVersion}");
                    
                    // Update the registry version after each successful migration
                    SetRegistryVersion(migration.ToVersion);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Migration failed: {migration.FromVersion} -> {migration.ToVersion}");
                    return false;
                }
            }

            // Ensure we're at the target version
            SetRegistryVersion(targetVersion);
            logger.Info($"All migrations completed successfully. Registry is now at version {targetVersion}");

            return true;
        }

        /// <summary>
        /// Gets the list of migrations that need to be executed to go from current to target version.
        /// </summary>
        private List<RegistryMigration> GetPendingMigrations(string currentVersion, string targetVersion)
        {
            var pending = new List<RegistryMigration>();

            foreach (var migration in _migrations)
            {
                // Include migration if:
                // 1. The migration's target version is greater than current version
                // 2. The migration's target version is less than or equal to target version
                if (CompareVersions(migration.ToVersion, currentVersion) > 0 &&
                    CompareVersions(migration.ToVersion, targetVersion) <= 0)
                {
                    pending.Add(migration);
                }
            }

            return pending;
        }

        /// <summary>
        /// Compares two version strings.
        /// </summary>
        /// <returns>
        /// -1 if version1 &lt; version2,
        /// 0 if version1 == version2,
        /// 1 if version1 &gt; version2
        /// </returns>
        private int CompareVersions(string version1, string version2)
        {
            // Remove 'v' prefix if present
            version1 = version1.TrimStart('v');
            version2 = version2.TrimStart('v');

            // Remove any build metadata (everything after '+')
            version1 = version1.Split('+')[0];
            version2 = version2.Split('+')[0];

            if (Version.TryParse(version1, out var v1) && Version.TryParse(version2, out var v2))
            {
                return v1.CompareTo(v2);
            }

            // Fallback to string comparison if parsing fails
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Represents a single migration step.
        /// </summary>
        private class RegistryMigration
        {
            public string FromVersion { get; set; } = string.Empty;
            public string ToVersion { get; set; } = string.Empty;
            public Action MigrationAction { get; set; } = () => { };
        }
    }
}
