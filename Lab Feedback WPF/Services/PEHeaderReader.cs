using System.IO;
using System.Runtime.InteropServices;

namespace Lab_Feedback_WPF.Services
{
    /// <summary>
    /// Reads the PE/COFF subsystem from a Windows executable without external tools.
    /// </summary>
    internal static class PEHeaderReader
    {
        // IMAGE_SUBSYSTEM_WINDOWS_CUI
        private const int IMAGE_SUBSYSTEM_WINDOWS_CUI = 3;

        public static bool IsConsoleSubsystem(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            fs.Position = 0x3C; // e_lfanew
            var peOffset = reader.ReadInt32();

            if (peOffset < 0 || peOffset > fs.Length - 4)
                return false;

            fs.Position = peOffset;
            var signature = reader.ReadUInt32();
            if (signature != 0x00004550) // "PE\0\0"
                return false;

            // COFF header is 20 bytes; subsystem is at offset 68 in the optional header.
            const int subsystemOffset = 68;
            fs.Position = peOffset + 4 + 20 + subsystemOffset;

            if (fs.Position + 2 > fs.Length)
                return false;

            var subsystem = reader.ReadUInt16();
            return subsystem == IMAGE_SUBSYSTEM_WINDOWS_CUI;
        }
    }
}
