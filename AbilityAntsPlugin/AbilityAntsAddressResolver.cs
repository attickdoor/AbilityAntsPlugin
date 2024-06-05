using Dalamud.Game;
using System;

namespace AbilityAntsPlugin
{
    public static class AbilityAntsAddressResolver
    {
        public static IntPtr ShouldDrawAnts { get; private set; }

        public static void Setup64Bit(ISigScanner scanner)
        {
            ShouldDrawAnts = scanner.ScanText("E8 ?? ?? ?? ?? 48 8B CB 88 47 41");
        }
    }
}
