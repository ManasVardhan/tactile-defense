using System;
using System.IO;
using UnityEngine;

namespace TactileWars.Save
{
    /// <summary>
    /// Versioned JSON save in Application.persistentDataPath/profile.json.
    /// Writes are atomic (temp file, then replace, keeping a .bak of the previous good file). Loading migrates old
    /// versions and falls back to the backup, then to a fresh profile, if the file is corrupt (the corrupt file is
    /// kept as profile.corrupt.json for inspection). Callers change the profile through Meta.MetaGame which saves
    /// immediately (autosave on change).
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>3: the level ladder (squad list; Squad Size and Bigger Backup research retired and refunded).</summary>
        public const int CurrentVersion = 3;
        public const string FileName = "profile.json";

        static PlayerProfile profile;
        static string directoryOverride;

        /// <summary>Raised after every successful save (UI refresh hook).</summary>
        public static event Action Saved;

        /// <summary>True when the last load had to recover from a corrupt or unreadable file.</summary>
        public static bool LastLoadRecovered { get; private set; }
        /// <summary>True when the last load found no save (first run).</summary>
        public static bool LastLoadWasNew { get; private set; }
        /// <summary>Optional hook that fills a brand new profile (starting gold etc.), installed by the meta layer.</summary>
        public static Action<PlayerProfile> NewProfileInitializer;
        /// <summary>Optional hook run on a profile migrated from an older version, after SaveMigrations and Sanitize
        /// (the meta layer fills what needs its database, such as the squad).</summary>
        public static Action<PlayerProfile> MigratedProfileInitializer;

        /// <summary>Redirects the save folder (tests). Null restores persistentDataPath. Drops the cached profile.</summary>
        public static string DirectoryOverride
        {
            get => directoryOverride;
            set { directoryOverride = value; profile = null; }
        }

        public static string Directory => string.IsNullOrEmpty(directoryOverride) ? Application.persistentDataPath : directoryOverride;
        public static string FilePath => Path.Combine(Directory, FileName);
        static string TempPath => FilePath + ".tmp";
        static string BackupPath => FilePath + ".bak";
        static string CorruptPath => Path.Combine(Directory, "profile.corrupt.json");

        public static PlayerProfile Profile
        {
            get
            {
                if (profile == null) profile = Load();
                return profile;
            }
        }

        public static bool HasProfileLoaded => profile != null;

        /// <summary>Re-reads the save from disk, replacing the cached profile.</summary>
        public static PlayerProfile Reload()
        {
            profile = Load();
            return profile;
        }

        public static PlayerProfile Load()
        {
            LastLoadRecovered = false;
            LastLoadWasNew = false;
            string path = FilePath;
            if (!File.Exists(path))
            {
                if (File.Exists(BackupPath))
                {
                    var fromBak = TryRead(BackupPath);
                    if (fromBak != null) { LastLoadRecovered = true; return fromBak; }
                }
                LastLoadWasNew = true;
                return CreateNew();
            }
            var p = TryRead(path);
            if (p != null) return p;

            // Corrupt: keep the bad file for inspection, then try the backup.
            try { File.Copy(path, CorruptPath, true); } catch (Exception e) { Debug.LogWarning("[Save] could not keep corrupt file: " + e.Message); }
            LastLoadRecovered = true;
            var bak = File.Exists(BackupPath) ? TryRead(BackupPath) : null;
            if (bak != null)
            {
                Debug.LogWarning("[Save] profile.json was corrupt, restored the backup");
                return bak;
            }
            Debug.LogWarning("[Save] profile.json was corrupt and no backup could be read, starting a new profile");
            return CreateNew();
        }

        static PlayerProfile CreateNew()
        {
            var p = new PlayerProfile();
            NewProfileInitializer?.Invoke(p);
            p.Sanitize();
            return p;
        }

        static PlayerProfile TryRead(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                int v = SaveMigrations.ReadVersion(json);
                if (v < 0) return null;
                bool migrated = v < CurrentVersion;
                PlayerProfile p = migrated ? SaveMigrations.Migrate(json, v) : JsonUtility.FromJson<PlayerProfile>(json);
                if (p == null) return null;
                p.Sanitize();
                if (migrated) MigratedProfileInitializer?.Invoke(p);
                return p;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] read failed for " + path + ": " + e.Message);
                return null;
            }
        }

        /// <summary>Writes the cached profile atomically. Returns false on IO failure (the old file stays intact).</summary>
        public static bool Save()
        {
            if (profile == null) return false;
            return Write(profile);
        }

        public static bool Write(PlayerProfile p)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                p.version = CurrentVersion;
                string json = JsonUtility.ToJson(p, true);
                File.WriteAllText(TempPath, json);
                if (File.Exists(FilePath))
                {
                    try { File.Replace(TempPath, FilePath, BackupPath); }
                    catch (Exception)
                    {
                        // Some file systems do not support Replace: fall back to copy-backup then move.
                        File.Copy(FilePath, BackupPath, true);
                        File.Delete(FilePath);
                        File.Move(TempPath, FilePath);
                    }
                }
                else File.Move(TempPath, FilePath);
                Saved?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[Save] write failed: " + e.Message);
                return false;
            }
        }

        /// <summary>Deletes the save and its backup, then starts a fresh profile (settings "reset save").</summary>
        public static PlayerProfile ResetProfile()
        {
            foreach (var f in new[] { FilePath, BackupPath, TempPath })
                try { if (File.Exists(f)) File.Delete(f); } catch (Exception e) { Debug.LogWarning("[Save] delete failed: " + e.Message); }
            profile = CreateNew();
            LastLoadWasNew = true;
            Save();
            return profile;
        }

        /// <summary>Replaces the cached profile without writing (tests).</summary>
        public static void SetProfileForTests(PlayerProfile p) { profile = p; }
    }
}
