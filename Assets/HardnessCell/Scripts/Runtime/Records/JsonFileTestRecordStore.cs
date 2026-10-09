using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>
    /// Stores the whole history in one JSON file (JsonUtility). Every change rewrites the file via a temp file +
    /// replace, so a failed write never leaves a half-written history. A file that cannot be read is never
    /// overwritten (writes are refused until a load succeeds); a file that cannot be parsed is moved aside as
    /// "*.corrupt-yyyyMMdd-HHmmss" and a new history is started.
    /// </summary>
    public sealed class JsonFileTestRecordStore : ITestRecordStore
    {
        private const string IdPrefix = "TEST-";
        private const int SchemaVersion = 1;

        [Serializable]
        private sealed class HistoryFile
        {
            public int schemaVersion = SchemaVersion;
            [Tooltip("Highest test sequence number ever issued; survives Clear so IDs are never reused.")]
            public int lastSequence;
            public List<TestRecord> records = new List<TestRecord>();
        }

        private readonly string path;
        private HistoryFile data;

        public JsonFileTestRecordStore(string path)
        {
            this.path = path;
        }

        public string Location => path;

        public IReadOnlyList<TestRecord> LoadAll()
        {
            data = null;
            data = File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new HistoryFile();
            if (data.records == null) data.records = new List<TestRecord>();
            data.records.RemoveAll(r => r == null);
            return data.records;
        }

        public string NextTestId()
        {
            EnsureLoaded();
            int highest = data.lastSequence;
            foreach (var record in data.records) highest = Math.Max(highest, ParseSequence(record.TestId));
            return IdPrefix + (highest + 1).ToString("D6", CultureInfo.InvariantCulture);
        }

        public void Append(TestRecord record)
        {
            EnsureLoaded();
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (data.records.Exists(r => r.TestId == record.TestId))
                throw new InvalidOperationException($"Test ID {record.TestId} already exists in {path}.");

            data.records.Add(record);
            data.lastSequence = Math.Max(data.lastSequence, ParseSequence(record.TestId));
            try
            {
                Write();
            }
            catch
            {
                data.records.Remove(record);
                throw;
            }
        }

        public void Clear()
        {
            EnsureLoaded();
            var previous = data.records;
            data.records = new List<TestRecord>();
            try
            {
                Write();
            }
            catch
            {
                data.records = previous;
                throw;
            }
        }

        private HistoryFile Parse(string json)
        {
            HistoryFile parsed = null;
            Exception error = null;
            try
            {
                parsed = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<HistoryFile>(json);
            }
            catch (ArgumentException e)
            {
                error = e;
            }
            if (parsed != null) return parsed;

            string quarantine = path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            File.Move(path, quarantine);
            Debug.LogError($"HISTORY: {path} could not be parsed ({error?.Message ?? "empty file"}). " +
                           $"It was moved to {quarantine} and a new history was started.");
            return new HistoryFile();
        }

        private void Write()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        private void EnsureLoaded()
        {
            if (data == null) throw new InvalidOperationException($"Test history at {path} is not loaded; refusing to overwrite it.");
        }

        private static int ParseSequence(string testId)
        {
            if (testId == null || !testId.StartsWith(IdPrefix, StringComparison.Ordinal)) return 0;
            return int.TryParse(testId.Substring(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
    }
}
