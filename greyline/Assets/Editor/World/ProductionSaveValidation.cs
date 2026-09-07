using System;
using System.IO;
using Greyline.Save;
using UnityEngine;

namespace Greyline.World.EditorTools
{
    public static class ProductionSaveValidation
    {
        public static void Validate()
        {
            string root = Path.GetFullPath(Path.Combine(ProductionDistrictPlayQA.OutputDirectory, "SaveFixtures", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "profile.json");
            var store = new ProductionSaveStore(path);
            Require(!store.TryLoad("fixture", out _) && store.CanWrite, "Missing profile must allow first save.");
            var data = new ProductionSaveData { catalogId="fixture", points=3 };
            Require(store.TrySave(data), "First save failed.");
            data.points=2; data.skills=new[]{"conditioning"};
            Require(store.TrySave(data) && File.Exists(store.BackupPath), "Second save must rotate a backup.");
            var reader = new ProductionSaveStore(path);
            Require(reader.TryLoad("fixture", out var loaded) && loaded.points==2 && loaded.skills.Length==1, "Production profile roundtrip failed.");
            string committed = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Require(!reader.TrySave(data), "Locked primary must fail without destructive fallback.");
            Require(File.ReadAllText(path)==committed, "Failed save changed the committed profile.");
            File.WriteAllText(path, "corrupt primary");
            reader = new ProductionSaveStore(path);
            Require(reader.TryLoad("fixture", out loaded) && reader.RecoveredBackup && loaded.points==3, "Corrupt primary must recover the valid backup.");
            string backup = File.ReadAllText(reader.BackupPath);
            Require(reader.TrySave(loaded) && File.ReadAllText(reader.BackupPath)==backup, "Repair save must preserve the known-good backup.");
            loaded.skills=new[]{"duplicate","duplicate"};
            Require(!reader.TrySave(loaded), "Duplicate IDs must be rejected.");
            committed=File.ReadAllText(path);
            File.WriteAllText(path, committed.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"));
            string future=File.ReadAllText(path);
            reader=new ProductionSaveStore(path);
            Require(!reader.TryLoad("fixture", out _) && !reader.CanWrite && !reader.TrySave(data) && File.ReadAllText(path)==future,
                "Future schemas must never fall back and overwrite newer progress.");
            string corruptPath=Path.Combine(root,"unrecoverable.json"); File.WriteAllText(corruptPath,"{}");
            var corrupt=new ProductionSaveStore(corruptPath);
            Require(!corrupt.TryLoad("fixture",out _) && !corrupt.CanWrite && !corrupt.TrySave(data), "Unrecoverable save must remain protected.");
            Debug.Log("PRODUCTION_SAVE_VALIDATION_OK: roundtrip, backup rotation/recovery/repair, locked-file failure, duplicate IDs, future schema and corrupt-profile protection.");
        }
        private static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    }
}
