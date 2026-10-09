using UnityEngine;

namespace HardnessCell.Hmi
{
    /// <summary>Dark control-panel colours shared by the HMI runtime and the editor installer that lays it out.</summary>
    public static class HmiPalette
    {
        public static readonly Color HeaderBackground = Hex(0x101317);
        public static readonly Color PanelBackground = Hex(0x181C21, 0.97f);
        public static readonly Color InsetBackground = Hex(0x111417);
        public static readonly Color Divider = Hex(0x2B323B);
        public static readonly Color Accent = Hex(0xF2A900);

        public static readonly Color TextPrimary = Hex(0xE8ECF1);
        public static readonly Color TextMuted = Hex(0x8A94A3);
        public static readonly Color TextDim = Hex(0x56606C);

        public static readonly Color ProgressTrack = Hex(0x2A3038);
        public static readonly Color ProgressFill = Hex(0x2D9CDB);

        public static readonly Color TileFill = Hex(0x20252C);
        public static readonly Color TileFrame = Hex(0x2F363F);
        public static readonly Color TileMeasuredFrame = Hex(0x4C5A6A);
        public static readonly Color TileActiveFill = Hex(0x3A321E);

        public static readonly Color StatusReady = Hex(0x3D6A99);
        public static readonly Color StatusTesting = Hex(0xB7791F);
        public static readonly Color StatusMeasuring = Hex(0x1597A6);
        public static readonly Color StatusResetting = Hex(0x6F6352);
        public static readonly Color StatusComplete = Hex(0x2F8F5B);
        public static readonly Color StatusFault = Hex(0xB83232);

        // Overall quality result (Stage 4). Individual point tiles stay neutral; only the overall result is coloured.
        public static readonly Color ResultPending = Hex(0x2F363F);
        public static readonly Color ResultPass = Hex(0x2F8F5B);
        public static readonly Color ResultFail = Hex(0xB83232);
        public static readonly Color ResultIncomplete = Hex(0xB7791F);
        public static readonly Color FailureText = Hex(0xE57373);

        // Safety / faults (Stage 6).
        public static readonly Color StatusPaused = Hex(0x7A5FA8);
        public static readonly Color SafetyOk = Hex(0x2F8F5B);
        public static readonly Color SafetyEmergency = Hex(0xC62828);
        public static readonly Color SafetyGateOpen = Hex(0xC77700);
        public static readonly Color SafetyFault = Hex(0xB83232);
        public static readonly Color EmergencyFrame = Hex(0xF2C200);
        public static readonly Color GateClosed = Hex(0x2F8F5B);
        public static readonly Color SimulateFaultButton = Hex(0x6B3A3A);
        public static readonly Color ClearFaultButton = Hex(0x3D6A99);

        public static readonly Color StartButton = Hex(0x2F8F5B);
        public static readonly Color ResetButton = Hex(0x4A5560);

        private static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
