using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace HardnessCell.Records
{
    /// <summary>
    /// Converts test records to CSV, one row per measured point. Excel-friendly: UTF-8 with BOM, CRLF line endings,
    /// invariant numbers, "yyyy-MM-dd HH:mm:ss" timestamps, RFC 4180 quoting.
    /// </summary>
    public static class CsvExporter
    {
        public static readonly string[] Header =
        {
            "TestId", "MoldId", "TestTimestamp", "PointId", "PointTimestamp", "Hardness",
            "MinLimit", "MaxLimit", "PointResult", "FailureReason", "OverallResult",
        };

        public static void Write(IEnumerable<TestRecord> records, TextWriter writer)
        {
            WriteRow(writer, Header);
            var row = new string[Header.Length];
            foreach (var record in records)
            {
                if (record == null) continue;
                foreach (var m in record.Measurements)
                {
                    row[0] = record.TestId;
                    row[1] = record.MoldId;
                    row[2] = TestRecord.ToDisplayTimestamp(record.Timestamp);
                    row[3] = m.PointId;
                    row[4] = TestRecord.ToDisplayTimestamp(m.Timestamp);
                    row[5] = Number(m.Hardness);
                    row[6] = Number(record.MinimumLimit);
                    row[7] = Number(record.MaximumLimit);
                    row[8] = m.WithinLimits ? TestRecord.PassResult : TestRecord.FailResult;
                    row[9] = m.FailureReason;
                    row[10] = record.Result;
                    WriteRow(writer, row);
                }
            }
        }

        /// <summary>Writes a new timestamped CSV file into the directory and returns its full path. Throws on I/O failure.</summary>
        public static string ExportToFile(IEnumerable<TestRecord> records, string directory)
        {
            Directory.CreateDirectory(directory);
            string stem = "hardness-tests_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, stem + ".csv");
            for (int i = 2; File.Exists(path); i++) path = Path.Combine(directory, $"{stem}_{i}.csv");

            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(true)) { NewLine = "\r\n" })
            {
                Write(records, writer);
            }
            return path;
        }

        public static string Escape(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            bool quote = field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || field[0] == ' ' || field[field.Length - 1] == ' ';
            return quote ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
        }

        private static void WriteRow(TextWriter writer, string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) writer.Write(',');
                writer.Write(Escape(fields[i]));
            }
            writer.WriteLine();
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
