using System.Collections.Generic;

namespace HardnessCell.Records
{
    /// <summary>
    /// Persistence backend for completed test records. Implementations throw on storage failure; TestHistory catches
    /// and reports. The local JSON file is the Stage 5 implementation; a database store can replace it later.
    /// </summary>
    public interface ITestRecordStore
    {
        /// <summary>Human-readable location (file path, connection name, ...).</summary>
        string Location { get; }

        /// <summary>Loads (or reloads) all stored records, oldest first. Must be called before the other members.</summary>
        IReadOnlyList<TestRecord> LoadAll();

        /// <summary>Next unused test ID (TEST-000001, ...); never reuses an ID issued by this store, even after Clear.</summary>
        string NextTestId();

        void Append(TestRecord record);

        /// <summary>Deletes all stored records (simulation/testing only).</summary>
        void Clear();
    }
}
