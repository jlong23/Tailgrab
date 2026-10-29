using Microsoft.Win32;
using NLog;
using Tailgrab.Common;
using Tailgrab.Models;

namespace Tailgrab
{
    internal class TailgrabMigration
    {
        private static readonly Logger _Logger = LogManager.GetCurrentClassLogger();
        private readonly ServiceRegistry _serviceRegistry;

        public TailgrabMigration(ServiceRegistry serviceRegistry)
        {
            _serviceRegistry = serviceRegistry;
        }

        public void UpgradeApplication()
        {
            // Execute registry migrations
            ExecuteRegistryMigrations();

            // Create missing registry items with default values
            InitializeMissingRegistryItems();
        }

        /// <summary>
        /// Configures and executes registry migrations to bring the registry schema up to the current application version.
        /// </summary>
        private void ExecuteRegistryMigrations()
        {
            var migrationManager = new Tailgrab.Common.RegistryMigrationManager();
            string currentAppVersion = BuildInfo.GetInformationalVersion().ToLower().Split('+')[0].TrimStart('v');

            _Logger.Info($"Configuring registry migrations for application version {currentAppVersion}");

            // Register migrations in order from oldest to newest
            // Example: migrationManager.RegisterMigration("1.0.0", "1.1.0", MigrateFrom_1_0_0_To_1_1_0);
            AddMigration_113(migrationManager);     
            AddMigration_115(migrationManager);

            // Execute all pending migrations
            bool success = migrationManager.ExecuteMigrations(currentAppVersion);

            if (!success)
            {
                _Logger.Error("Registry migration failed. Application may not function correctly.");
            }
        }

        private void AddMigration_115(RegistryMigrationManager migrationManager)
        {
            migrationManager.RegisterMigration("1.1.3", "1.1.5", () =>
            {
                _Logger.Info("Migrating registry from 1.1.3 to 1.1.5: New Features");

                try
                {
                    _Logger.Info("Migrating registry from 1.1.3 to 1.1.5: Doing nothing");
                }
                catch (Exception ex)
                {
                    _Logger.Warn(ex, "Failed during registry migration from 1.1.3 to 1.1.5");
                }
            });
        }

        private void AddMigration_113(RegistryMigrationManager migrationManager)
        {
            // Migration from 1.1.0 to 1.1.3 - Add Ollama API settings
            migrationManager.RegisterMigration("1.1.0", "1.1.3", () =>
            {
                _Logger.Info("Migrating registry from 1.1.0-2 to 1.1.3: Adding Ollama API settings");

                try
                {
                    // Move Registry Ollama API settings from secrets to plain text entries
                    var ollamaEndpoint = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint);
                    if (!string.IsNullOrEmpty(ollamaEndpoint))
                    {
                        ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint);
                        ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint, ollamaEndpoint);
                        _Logger.Info("Migrated Ollama API Endpoint from secrets to registry");
                    }

                    // Move Ollama API Model from secrets to plain text entry
                    var ollamaModel = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model);
                    if (!string.IsNullOrEmpty(ollamaModel))
                    {
                        ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model);
                        ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model, ollamaModel);
                        _Logger.Info("Migrated Ollama API Model from secrets to registry");
                    }

                    // Move Ollama API Prompt from secrets to plain text entry
                    var ollamaPrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt);
                    if (!string.IsNullOrEmpty(ollamaPrompt))
                    {
                        ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt);
                        ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt, ollamaPrompt);
                        _Logger.Info("Migrated Ollama API Prompt from secrets to registry");
                    }

                    // Move Ollama API Image Prompt from secrets to plain text entry
                    var ollamaImagePrompt = ConfigStore.LoadSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt);
                    if (!string.IsNullOrEmpty(ollamaImagePrompt))
                    {
                        ConfigStore.DeleteSecret(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt);
                        ConfigStore.PutStoredKeyString(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt, ollamaImagePrompt);
                        _Logger.Info("Migrated Ollama API Image Prompt from secrets to registry");
                    }

                    TailgrabDBContext dbContext = _serviceRegistry.GetDBContext();
                    if (dbContext != null)
                    {
                        // Migrate the ProfileEvaluation table to add the new PromptMd5Checksum column, which is used to link evaluations to specific prompts.
                        try
                        {
                            dbContext.ExecuteSql("ALTER TABLE ProfileEvaluation RENAME TO ProfileEvaluation_OLD;");
                            dbContext.ExecuteSql("CREATE TABLE IF NOT EXISTS ProfileEvaluation (MD5Checksum TEXT NOT NULL, ProfileText BLOB, Evaluation\tBLOB, LastDateTime TEXT NOT NULL, isIgnored INTEGER NOT NULL DEFAULT 0, PromptMd5Checksum TEXT, CONSTRAINT PK_ProfileEvaluation PRIMARY KEY(MD5Checksum))");
                            dbContext.ExecuteSql("INSERT INTO ProfileEvaluation SELECT * FROM ProfileEvaluation WHERE 1=1");
                            dbContext.ExecuteSql("DROP TABLE IF EXISTS ProfileEvaluation_OLD");
                        }
                        catch
                        {
                            _Logger.Warn("Failed to migrate ProfileEvaluation table during registry migration. This may cause issues with profile evaluations. Consider backing up your database, deleting it, and allowing Tailgrab to create a new one if you encounter problems with profile evaluations after this update.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _Logger.Warn(ex, "Failed during registry migration from 1.1.0-2 to 1.1.3");
                }
            });
        }

        /// <summary>
        /// Initialize missing registry items with their default values.
        /// This ensures all required configuration keys exist without overwriting existing values.
        /// </summary>
        private void InitializeMissingRegistryItems()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(Tailgrab.Common.CommonConst.ConfigRegistryPath);
                if (key == null)
                {
                    _Logger.Warn("Failed to create or open registry key for configuration.");
                    return;
                }

                // Helper function to set value only if it doesn't exist
                void SetDefaultIfMissing(string name, string defaultValue)
                {
                    if (key.GetValue(name) == null && !string.IsNullOrEmpty(defaultValue))
                    {
                        key.SetValue(name, defaultValue, RegistryValueKind.String);
                        _Logger.Info($"Initialized missing registry item: {name}");
                    }
                }

                // Initialize Ollama API registry keys with defaults
                SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Endpoint,
                    Tailgrab.Common.CommonConst.Default_Ollama_API_Endpoint);
                SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Model,
                    Tailgrab.Common.CommonConst.Default_Ollama_API_Model);
                SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Prompt,
                    Tailgrab.Common.CommonConst.Default_Ollama_API_Prompt);
                SetDefaultIfMissing(Tailgrab.Common.CommonConst.Registry_Ollama_API_Image_Prompt,
                    Tailgrab.Common.CommonConst.Default_Ollama_API_Image_Prompt);

                _Logger.Info("Registry initialization completed.");
            }
            catch (Exception ex)
            {
                _Logger.Warn(ex, "Failed to initialize missing registry items");
            }
        }

    }
}
