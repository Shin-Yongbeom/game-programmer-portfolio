using System;
using System.IO;
using Greyline.Core;
using UnityEngine;

namespace Greyline.Save
{
    /// <summary>
    /// File-backed save service for the systems vertical slice. Only GameFlags are persisted;
    /// checkpoint, combat, physics and camera state are not.
    ///
    /// Writes are atomic (temp file → replace) and keep the previous file as <c>.bak</c>; Load
    /// falls back to <c>.bak</c> when the primary file is missing or unreadable. The data scope
    /// (bool GameFlags + schema v1) is still deliberately minimal — promoting to a full game save
    /// adds ISaveable capture, checkpoint id and schema migration, not a different file layer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlagsSaveService : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;
        private const string DefaultFileName = "systems-sandbox.json";

        [SerializeField] private string fileName = DefaultFileName;

        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);
        public string BackupPath => SavePath + ".bak";
        private string TempPath => SavePath + ".tmp";

        public bool Save()
        {
            GameFlags flags = ResolveFlags();
            if (flags == null)
            {
                Debug.LogError("GameFlagsSaveService: no GameFlagsHost available.", this);
                return false;
            }

            GameFlagsSaveData data = GameFlagsSaveData.Capture(flags);
            data.schemaVersion = CurrentSchemaVersion;
            string json = JsonUtility.ToJson(data, true);

            try
            {
                File.WriteAllText(TempPath, json);
                CommitTempFile();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogError($"GameFlagsSaveService: save failed ({e.GetType().Name}: {e.Message}).", this);
                TryDelete(TempPath);
                return false;
            }

            Debug.Log($"GameFlags saved to {SavePath}.", this);
            return true;
        }

        public bool Load()
        {
            GameFlags flags = ResolveFlags();
            if (flags == null)
            {
                Debug.LogError("GameFlagsSaveService: no GameFlagsHost available.", this);
                return false;
            }

            GameFlagsSaveData data = ReadValid(SavePath, out string usedPath)
                                     ?? ReadValid(BackupPath, out usedPath);
            if (data == null)
            {
                Debug.LogWarning("GameFlagsSaveService: no valid save file (primary or .bak).", this);
                return false;
            }

            data.Restore(flags);
            Debug.Log(
                usedPath == BackupPath
                    ? $"GameFlags loaded from BACKUP {usedPath} (primary was missing or corrupt)."
                    : $"GameFlags loaded from {usedPath}.",
                this);
            return true;
        }

        public void ClearRuntimeFlags()
        {
            GameFlags flags = ResolveFlags();
            if (flags == null)
            {
                Debug.LogError("GameFlagsSaveService: no GameFlagsHost available.", this);
                return;
            }

            flags.Clear();
            Debug.Log("GameFlags runtime state cleared; save file retained.", this);
        }

        /// <summary>Sandbox verification path: Save, clear runtime flags, then Load them back.</summary>
        public bool RoundTrip()
        {
            return Save() && ClearAndLoad();
        }

        [ContextMenu("Save Game Flags")]
        private void SaveFromContextMenu() => Save();

        [ContextMenu("Load Game Flags")]
        private void LoadFromContextMenu() => Load();

        [ContextMenu("Clear Runtime Flags")]
        private void ClearFromContextMenu() => ClearRuntimeFlags();

        [ContextMenu("Save, Clear, Load Round Trip")]
        private void RoundTripFromContextMenu() => RoundTrip();

        private bool ClearAndLoad()
        {
            ClearRuntimeFlags();
            return Load();
        }

        /// <summary>Atomically swaps the temp file into place, rotating the old file to <c>.bak</c>.</summary>
        private void CommitTempFile()
        {
            if (!File.Exists(SavePath))
            {
                File.Move(TempPath, SavePath);
                return;
            }

            try
            {
                // ReplaceFile Win32 semantics: atomic, old file preserved as the backup.
                File.Replace(TempPath, SavePath, BackupPath);
            }
            catch (IOException)
            {
                // Filesystems without atomic replace: fall back to copy-backup then overwrite.
                File.Copy(SavePath, BackupPath, overwrite: true);
                File.Delete(SavePath);
                File.Move(TempPath, SavePath);
            }
        }

        private GameFlagsSaveData ReadValid(string path, out string usedPath)
        {
            usedPath = path;
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                GameFlagsSaveData data = JsonUtility.FromJson<GameFlagsSaveData>(File.ReadAllText(path));
                if (data == null || data.schemaVersion != CurrentSchemaVersion)
                {
                    Debug.LogWarning($"GameFlagsSaveService: {path} has an unsupported or invalid schema version.", this);
                    return null;
                }

                return data;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                Debug.LogWarning($"GameFlagsSaveService: could not read {path} ({e.GetType().Name}).", this);
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
                // best effort
            }
        }

        private GameFlags ResolveFlags()
        {
            GameFlagsHost host = GameFlagsHost.Current != null
                ? GameFlagsHost.Current
                : FindFirstObjectByType<GameFlagsHost>();
            return host != null ? host.Flags : null;
        }
    }
}
