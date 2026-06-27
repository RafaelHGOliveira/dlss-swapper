using System;
using System.IO;

namespace DLSS_Swapper.Helpers;

// Reads VS_FIXEDFILEINFO from a native Windows PE (DLL/EXE) file.
// Used on Linux where FileVersionInfo.GetVersionInfo returns zeros for native PE files.
internal static class PeVersionReader
{
    internal static Version? ReadFileVersion(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (br.ReadUInt16() != 0x5A4D) return null; // MZ
            fs.Seek(0x3C, SeekOrigin.Begin);
            uint peOffset = br.ReadUInt32();

            fs.Seek(peOffset, SeekOrigin.Begin);
            if (br.ReadUInt32() != 0x00004550) return null; // PE\0\0

            fs.Seek(2, SeekOrigin.Current); // Machine
            ushort numSections = br.ReadUInt16();
            fs.Seek(12, SeekOrigin.Current); // TimeDateStamp + SymbolTablePtr + NumSymbols
            ushort optHeaderSize = br.ReadUInt16();
            fs.Seek(2, SeekOrigin.Current); // Characteristics
            long optStart = fs.Position;

            ushort magic = br.ReadUInt16();
            bool pe32plus = magic == 0x20B;

            // Data directory #2 = resource table. Array starts at optStart+96 (PE32) or +112 (PE32+).
            fs.Seek(optStart + (pe32plus ? 112 : 96) + 2 * 8, SeekOrigin.Begin);
            uint rsrcRVA = br.ReadUInt32();
            uint rsrcSize = br.ReadUInt32();
            if (rsrcRVA == 0 || rsrcSize == 0) return null;

            long sectStart = optStart + optHeaderSize;
            uint rsrcFileOff = 0;
            for (int i = 0; i < numSections; i++)
            {
                fs.Seek(sectStart + i * 40L, SeekOrigin.Begin);
                fs.Seek(8, SeekOrigin.Current); // Name
                fs.Seek(4, SeekOrigin.Current); // VirtualSize
                uint sectRVA = br.ReadUInt32();
                fs.Seek(4, SeekOrigin.Current); // SizeOfRawData
                uint rawOff = br.ReadUInt32();
                if (sectRVA == rsrcRVA) { rsrcFileOff = rawOff; break; }
            }
            if (rsrcFileOff == 0) return null;

            int readSize = (int)Math.Min(rsrcSize, 2 * 1024 * 1024);
            byte[] rsrc = new byte[readSize];
            fs.Seek(rsrcFileOff, SeekOrigin.Begin);
            fs.ReadExactly(rsrc);

            // Walk resource tree: Root → RT_VERSION(16) → first name → first lang → data entry
            uint lvl2 = FindSubdirEntry(rsrc, 0, 16);
            if (lvl2 == uint.MaxValue) return null;
            uint lvl3 = FindFirstSubdir(rsrc, lvl2);
            if (lvl3 == uint.MaxValue) return null;
            uint dataEntry = FindFirstDataEntry(rsrc, lvl3);
            if (dataEntry == uint.MaxValue) return null;

            uint dataRVA = BitConverter.ToUInt32(rsrc, (int)dataEntry);
            uint dataSize = BitConverter.ToUInt32(rsrc, (int)dataEntry + 4);
            uint dataOff = dataRVA - rsrcRVA;
            if (dataOff + dataSize > rsrc.Length) return null;

            // Scan for VS_FIXEDFILEINFO magic 0xFEEF04BD
            for (uint i = dataOff; i + 52 <= dataOff + dataSize; i++)
            {
                if (BitConverter.ToUInt32(rsrc, (int)i) == 0xFEEF04BD)
                {
                    uint ms = BitConverter.ToUInt32(rsrc, (int)i + 8);
                    uint ls = BitConverter.ToUInt32(rsrc, (int)i + 12);
                    return new Version((int)(ms >> 16), (int)(ms & 0xFFFF), (int)(ls >> 16), (int)(ls & 0xFFFF));
                }
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    static uint FindSubdirEntry(byte[] rsrc, uint dirOff, uint id)
    {
        if (dirOff + 16 > rsrc.Length) return uint.MaxValue;
        ushort named = BitConverter.ToUInt16(rsrc, (int)dirOff + 12);
        ushort ided  = BitConverter.ToUInt16(rsrc, (int)dirOff + 14);
        uint eOff = dirOff + 16;
        for (int i = 0; i < named + ided; i++, eOff += 8)
        {
            if (eOff + 8 > rsrc.Length) break;
            uint nameOrId = BitConverter.ToUInt32(rsrc, (int)eOff);
            uint sub      = BitConverter.ToUInt32(rsrc, (int)eOff + 4);
            if ((nameOrId & 0x80000000) == 0 && nameOrId == id && (sub & 0x80000000) != 0)
                return sub & 0x7FFFFFFF;
        }
        return uint.MaxValue;
    }

    static uint FindFirstSubdir(byte[] rsrc, uint dirOff)
    {
        if (dirOff + 16 > rsrc.Length) return uint.MaxValue;
        ushort named = BitConverter.ToUInt16(rsrc, (int)dirOff + 12);
        ushort ided  = BitConverter.ToUInt16(rsrc, (int)dirOff + 14);
        if (named + ided == 0) return uint.MaxValue;
        uint eOff = dirOff + 16;
        if (eOff + 8 > rsrc.Length) return uint.MaxValue;
        uint sub = BitConverter.ToUInt32(rsrc, (int)eOff + 4);
        return (sub & 0x80000000) != 0 ? sub & 0x7FFFFFFF : uint.MaxValue;
    }

    // Returns the IMAGE_RESOURCE_DATA_ENTRY offset for the first leaf in a directory
    static uint FindFirstDataEntry(byte[] rsrc, uint dirOff)
    {
        if (dirOff + 16 > rsrc.Length) return uint.MaxValue;
        ushort named = BitConverter.ToUInt16(rsrc, (int)dirOff + 12);
        ushort ided  = BitConverter.ToUInt16(rsrc, (int)dirOff + 14);
        if (named + ided == 0) return uint.MaxValue;
        uint eOff = dirOff + 16;
        if (eOff + 8 > rsrc.Length) return uint.MaxValue;
        uint sub = BitConverter.ToUInt32(rsrc, (int)eOff + 4);
        return (sub & 0x80000000) == 0 ? sub : uint.MaxValue;
    }
}
