using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Greyline.Save
{
    [Serializable]
    public sealed class ProductionSaveData
    {
        public string catalogId;
        public string checkpointId = "boulevard";
        public int points;
        public string[] skills = Array.Empty<string>();
        public string[] clearedEncounters = Array.Empty<string>();
        public ProductionSaveData Copy() => JsonUtility.FromJson<ProductionSaveData>(JsonUtility.ToJson(this));
    }

    /// <summary>Separate production profile; never reads or overwrites the legacy flag sandbox save.</summary>
    public sealed class ProductionSaveStore
    {
        [Serializable] private sealed class Envelope
        {
            public int schemaVersion = 1;
            public long revision;
            public ProductionSaveData data;
            public string checksum;
        }
        public string Path { get; }
        public string BackupPath => Path + ".bak";
        public string Status { get; private set; } = "New profile";
        public bool CanWrite { get; private set; } = true;
        public bool RecoveredBackup { get; private set; }
        private long revision;
        private bool primaryValid;
        public ProductionSaveStore(string path) => Path = path;

        public bool TryLoad(string catalogId, out ProductionSaveData data)
        {
            data = null; CanWrite = true; RecoveredBackup = false;
            Envelope primary = Read(Path, catalogId, out bool future);
            if (future) { CanWrite = false; Status = "Save requires a newer game version; profile is protected."; return false; }
            Envelope backup = Read(BackupPath, catalogId, out bool futureBackup);
            if (futureBackup) { CanWrite = false; Status = "Backup requires a newer game version; profile is protected."; return false; }
            primaryValid = primary != null;
            Envelope chosen = primary != null && (backup == null || primary.revision >= backup.revision) ? primary : backup;
            if (chosen == null)
            {
                // Two invalid files must not silently become a new profile that overwrites player progress.
                CanWrite = !File.Exists(Path) && !File.Exists(BackupPath);
                Status = CanWrite ? "New profile" : "No valid profile; existing files are protected.";
                return false;
            }
            revision = chosen.revision; data = chosen.data; RecoveredBackup = chosen == backup;
            primaryValid = chosen == primary;
            Status = RecoveredBackup ? "Recovered backup" : "Progress loaded";
            return true;
        }

        public bool TrySave(ProductionSaveData data)
        {
            if (!CanWrite || !Valid(data)) return false;
            string temporary = Path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                var envelope = new Envelope { revision = revision + 1, data = data, checksum = Hash(data) };
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(Path)) File.Replace(temporary, Path, primaryValid ? BackupPath : null);
                else File.Move(temporary, Path);
                revision++; primaryValid = true; RecoveredBackup = false; Status = "Progress saved";
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Keep the previous committed profile and backup intact on write failure.
                Status = "Save failed; progress remains in memory. " + e.GetType().Name;
                return false;
            }
        }

        private static Envelope Read(string path, string catalog, out bool future)
        {
            future = false;
            if (!File.Exists(path)) return null;
            try
            {
                var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if (envelope == null) return null;
                future = envelope.schemaVersion > 1;
                if (envelope.schemaVersion != 1 || envelope.revision < 1 || !Valid(envelope.data) ||
                    envelope.data.catalogId != catalog || envelope.checksum != Hash(envelope.data)) return null;
                return envelope;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { return null; }
        }
        private static bool Valid(ProductionSaveData data) => data != null && !string.IsNullOrWhiteSpace(data.catalogId) &&
            !string.IsNullOrWhiteSpace(data.checkpointId) && data.points >= 0 && data.points <= 100000 &&
            ValidIds(data.skills) && ValidIds(data.clearedEncounters);
        private static bool ValidIds(string[] ids)
        {
            if (ids == null || ids.Length > 4096) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids) if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || !unique.Add(id)) return false;
            return true;
        }
        private static string Hash(ProductionSaveData data)
        {
            using var algorithm = SHA256.Create();
            return Convert.ToBase64String(algorithm.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data))));
        }
    }
}
