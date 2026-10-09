using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>
    /// Persistent history of completed tests, loaded at startup. Wraps an ITestRecordStore and turns storage failures
    /// into logged errors so the simulation and HMI keep running. RESET never touches the history.
    /// </summary>
    public sealed class TestHistory : MonoBehaviour
    {
        [Tooltip("History file name inside Application.persistentDataPath/HardnessCell.")]
        [SerializeField] private string fileName = "test-history.json";

        private ITestRecordStore store;
        private IReadOnlyList<TestRecord> records = Array.Empty<TestRecord>();

        /// <summary>False if the stored history could not be read; saving is then refused to avoid overwriting it.</summary>
        public bool IsLoaded { get; private set; }
        public IReadOnlyList<TestRecord> Records => records;
        public TestRecord Latest => records.Count > 0 ? records[records.Count - 1] : null;
        public string StorageLocation => store?.Location;

        public event Action HistoryChanged;

        private void Awake()
        {
            store = new JsonFileTestRecordStore(Path.Combine(RecordPaths.DataDirectory, fileName));
            Load();
        }

        public bool Load()
        {
            try
            {
                records = store.LoadAll();
                IsLoaded = true;
                Debug.Log($"HISTORY: loaded {records.Count} test record(s) from {store.Location}", this);
            }
            catch (Exception e)
            {
                records = Array.Empty<TestRecord>();
                IsLoaded = false;
                Debug.LogException(e, this);
                Debug.LogError($"HISTORY: could not load {store.Location}. New test records will not be saved this session.", this);
            }
            HistoryChanged?.Invoke();
            return IsLoaded;
        }

        /// <summary>Next unused test ID, or null if the history is unavailable.</summary>
        public string NextTestId()
        {
            if (!IsLoaded) return null;
            try
            {
                return store.NextTestId();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                return null;
            }
        }

        public bool Save(TestRecord record)
        {
            if (!IsLoaded)
            {
                Debug.LogError($"HISTORY: {record?.TestId} not saved - history storage is unavailable ({store?.Location}).", this);
                return false;
            }
            try
            {
                store.Append(record);
                Debug.Log($"HISTORY: saved {record.TestId} ({record.MoldId}, {record.Result}) to {store.Location}", this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"HISTORY: FAILED to save {record?.TestId} ({record?.MoldId}, {record?.Result}) to {store.Location}.", this);
                return false;
            }
            HistoryChanged?.Invoke();
            return true;
        }

        /// <summary>Deletes all saved records (test IDs still continue from the last issued number). Simulation/testing only.</summary>
        [ContextMenu("Clear Test History")]
        public bool Clear()
        {
            if (!IsLoaded) return false;
            try
            {
                store.Clear();
                Debug.LogWarning($"HISTORY: cleared all test records in {store.Location}", this);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError($"HISTORY: failed to clear {store.Location}.", this);
                return false;
            }
            HistoryChanged?.Invoke();
            return true;
        }
    }
}
