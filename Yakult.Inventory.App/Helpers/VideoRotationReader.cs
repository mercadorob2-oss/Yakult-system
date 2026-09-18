using System;
using System.Text;

namespace Yakult.Inventory.App.Helpers
{
    /// <summary>Reads the display-rotation baked into an MP4/MOV container's "tkhd" (track header)
    /// matrix — the same metadata phone cameras use to record portrait video without physically
    /// rotating the sensor pixel data. WPF's MediaElement (unlike mobile OS video players) does not
    /// apply this automatically, so a portrait phone recording renders sideways unless corrected.
    /// Hand-rolled parser (no external dependency) following ISO/IEC 14496-12's box layout.</summary>
    public static class VideoRotationReader
    {
        /// <summary>Returns 0, 90, 180, or 270 — the rotation to apply so the video displays
        /// upright. Fails soft (returns 0) on anything malformed/unsupported rather than throwing,
        /// since this is a display nicety, not something that should block playback.</summary>
        public static int GetRotationDegrees(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length < 8) return 0;

                var moov = FindBox(bytes, 0, bytes.Length, "moov");
                if (moov == null) return 0;

                var (moovStart, moovEnd) = moov.Value;
                var pos = moovStart;
                while (pos < moovEnd)
                {
                    var trak = FindBox(bytes, pos, moovEnd, "trak");
                    if (trak == null) break;
                    var (trakStart, trakEnd) = trak.Value;

                    var tkhd = FindBox(bytes, trakStart, trakEnd, "tkhd");
                    if (tkhd != null &&
                        TryReadTkhd(bytes, tkhd.Value.Item1, tkhd.Value.Item2, out var degrees, out var isVideoTrack) &&
                        isVideoTrack)
                    {
                        return degrees;
                    }

                    pos = trakEnd;
                }
            }
            catch
            {
                // Best-effort — malformed/unsupported containers just play unrotated.
            }
            return 0;
        }

        /// <summary>Finds the first child box of the given fourcc within [start, end) of the raw
        /// file bytes, returning (contentStart, contentEnd) — content excluding the 8-byte
        /// size+type header.</summary>
        private static (int, int)? FindBox(byte[] b, int start, int end, string fourcc)
        {
            var pos = start;
            while (pos + 8 <= end)
            {
                long size = ReadUInt32(b, pos);
                var type = Encoding.ASCII.GetString(b, pos + 4, 4);
                var headerSize = 8;

                if (size == 1)
                {
                    if (pos + 16 > end) break;
                    size = ReadUInt64(b, pos + 8);
                    headerSize = 16;
                }
                else if (size == 0)
                {
                    // Box extends to end of file/parent — not expected for moov/trak/tkhd in
                    // camera output; bail rather than guess.
                    break;
                }

                var contentStart = pos + headerSize;
                var contentEnd = pos + (int)size;
                if (contentEnd > end || contentEnd <= contentStart) break;

                if (type == fourcc) return (contentStart, contentEnd);

                pos = contentEnd;
            }
            return null;
        }

        private static bool TryReadTkhd(byte[] b, int start, int end, out int degrees, out bool isVideoTrack)
        {
            degrees = 0;
            isVideoTrack = false;
            if (start >= end) return false;

            var version = b[start];
            var timeFieldsSize = version == 1 ? 32 : 20;
            var matrixOffset = start + 4 /*version+flags*/ + timeFieldsSize + 8 /*reserved*/ + 8 /*layer+altgroup+volume+reserved*/;
            if (matrixOffset + 44 > end) return false; // 36 (matrix) + 8 (width+height)

            int a = ReadInt32(b, matrixOffset);
            int bVal = ReadInt32(b, matrixOffset + 4);
            int c = ReadInt32(b, matrixOffset + 12);
            int d = ReadInt32(b, matrixOffset + 16);

            uint width = ReadUInt32(b, matrixOffset + 36);
            uint height = ReadUInt32(b, matrixOffset + 40);
            isVideoTrack = width > 0 && height > 0;

            const int fixed1 = 0x00010000;
            const int fixedNeg1 = unchecked((int)0xFFFF0000);

            if (a == fixed1 && bVal == 0 && c == 0 && d == fixed1) degrees = 0;
            else if (a == 0 && bVal == fixed1 && c == fixedNeg1 && d == 0) degrees = 90;
            else if (a == fixedNeg1 && bVal == 0 && c == 0 && d == fixedNeg1) degrees = 180;
            else if (a == 0 && bVal == fixedNeg1 && c == fixed1 && d == 0) degrees = 270;
            else degrees = 0;

            return true;
        }

        private static uint ReadUInt32(byte[] b, int offset) =>
            (uint)((b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3]);

        private static long ReadUInt64(byte[] b, int offset)
        {
            long high = ReadUInt32(b, offset);
            long low = ReadUInt32(b, offset + 4);
            return (high << 32) | low;
        }

        private static int ReadInt32(byte[] b, int offset) => unchecked((int)ReadUInt32(b, offset));
    }
}
