using System.IO;
using UnityEngine;

namespace HardnessCell.Records
{
    /// <summary>Runtime data locations. Everything lives under Application.persistentDataPath, never under Assets.</summary>
    public static class RecordPaths
    {
        public static string DataDirectory => Path.Combine(Application.persistentDataPath, "HardnessCell");
        public static string ExportDirectory => Path.Combine(DataDirectory, "Exports");
    }
}
