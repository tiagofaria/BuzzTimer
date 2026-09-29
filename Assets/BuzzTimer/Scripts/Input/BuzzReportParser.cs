using System;

namespace Buzz.Input
{
    /// <summary>Decodes the five-byte input report used by Sony's wireless Wbuzz receiver.</summary>
    public static class BuzzReportParser
    {
        // Each pair is { byte offset, bit mask }. Buttons are ordered player-major:
        // Red, Blue, Orange, Green, Yellow for players 1 through 4.
        private static readonly byte[,] Mapping =
        {
            { 2, 0x01 }, { 2, 0x10 }, { 2, 0x08 }, { 2, 0x04 }, { 2, 0x02 },
            { 2, 0x20 }, { 3, 0x02 }, { 3, 0x01 }, { 2, 0x80 }, { 2, 0x40 },
            { 3, 0x04 }, { 3, 0x40 }, { 3, 0x20 }, { 3, 0x10 }, { 3, 0x08 },
            { 3, 0x80 }, { 4, 0x08 }, { 4, 0x04 }, { 4, 0x02 }, { 4, 0x01 }
        };

        public static bool TryParse(byte[] report, bool[] destination)
        {
            if (report == null || report.Length < 5)
                return false;
            if (destination == null || destination.Length < 20)
                throw new ArgumentException("The destination must contain at least 20 entries.", nameof(destination));

            // node-hid exposes the five payload bytes directly. HidSharp/Windows may
            // prepend a report-ID byte, producing a six-byte buffer. Anchor the mapping
            // to the final five bytes so both representations decode identically.
            var payloadOffset = report.Length - 5;
            for (var i = 0; i < 20; i++)
                destination[i] = (report[Mapping[i, 0] + payloadOffset] & Mapping[i, 1]) != 0;

            return true;
        }
    }
}
