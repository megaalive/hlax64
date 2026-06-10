namespace HlaX64.DebugAdapter;

/// <summary>Reads PE/ELF entry point when debug symbols are absent.</summary>
public static class NativeBinaryEntryPoint
{
    public static bool TryGetEntryPoint(string path, out ulong address)
    {
        address = 0;
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            if (TryReadPeEntry(stream, out address))
                return true;
            stream.Position = 0;
            return TryReadElfEntry(stream, out address);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetPeEntryRva(string path, out uint entryRva)
    {
        entryRva = 0;
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            return TryReadPeEntryRva(stream, out entryRva);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryResolvePeEntryPoint(string path, ulong loadedImageBase, out ulong entryPoint)
    {
        entryPoint = 0;
        if (loadedImageBase == 0 || !TryGetPeEntryRva(path, out var entryRva) || entryRva == 0)
            return false;

        entryPoint = loadedImageBase + entryRva;
        return true;
    }

    public static bool TryGetPePreferredImageBase(string path, out ulong imageBase)
    {
        imageBase = 0;
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            return TryReadPeImageBase(stream, out imageBase);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetPeSizeOfImage(string path, out uint sizeOfImage)
    {
        sizeOfImage = 0;
        if (!File.Exists(path))
            return false;

        try
        {
            using var stream = File.OpenRead(path);
            return TryReadPeSizeOfImage(stream, out sizeOfImage);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadPeImageBase(Stream stream, out ulong imageBase)
    {
        imageBase = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
            return false;

        stream.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();
        if (peOffset <= 0 || peOffset + 0x40 > stream.Length)
            return false;

        stream.Seek(peOffset, SeekOrigin.Begin);
        if (reader.ReadUInt32() != 0x00004550)
            return false;

        var optionalOffset = peOffset + 24;
        stream.Seek(optionalOffset, SeekOrigin.Begin);
        var magic = reader.ReadUInt16();
        if (magic is not (0x10b or 0x20b))
            return false;

        imageBase = magic switch
        {
            0x10b => ReadUInt32At(stream, optionalOffset + 0x1C),
            0x20b => ReadUInt64At(stream, optionalOffset + 0x18),
            _ => 0
        };

        return imageBase != 0;
    }

    private static bool TryReadPeSizeOfImage(Stream stream, out uint sizeOfImage)
    {
        sizeOfImage = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
            return false;

        stream.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();
        if (peOffset <= 0 || peOffset + 0x40 > stream.Length)
            return false;

        stream.Seek(peOffset, SeekOrigin.Begin);
        if (reader.ReadUInt32() != 0x00004550)
            return false;

        var optionalOffset = peOffset + 24;
        stream.Seek(optionalOffset + 0x38, SeekOrigin.Begin);
        sizeOfImage = reader.ReadUInt32();
        return sizeOfImage != 0;
    }

    private static bool TryReadPeEntry(Stream stream, out ulong address)
    {
        address = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
            return false;

        stream.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();
        if (peOffset <= 0 || peOffset + 0x40 > stream.Length)
            return false;

        stream.Seek(peOffset, SeekOrigin.Begin);
        if (reader.ReadUInt32() != 0x00004550)
            return false;

        var optionalOffset = peOffset + 24;
        stream.Seek(optionalOffset, SeekOrigin.Begin);
        var magic = reader.ReadUInt16();
        if (magic is not (0x10b or 0x20b))
            return false;

        stream.Seek(optionalOffset + 0x10, SeekOrigin.Begin);
        var entryRva = reader.ReadUInt32();

        ulong imageBase = magic switch
        {
            0x10b => ReadUInt32At(stream, optionalOffset + 0x1C),
            0x20b => ReadUInt64At(stream, optionalOffset + 0x18),
            _ => 0
        };

        if (imageBase == 0 || entryRva == 0)
            return false;

        address = imageBase + entryRva;
        return true;
    }

    private static bool TryReadPeEntryRva(Stream stream, out uint entryRva)
    {
        entryRva = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
            return false;

        stream.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadInt32();
        if (peOffset <= 0 || peOffset + 0x40 > stream.Length)
            return false;

        stream.Seek(peOffset, SeekOrigin.Begin);
        if (reader.ReadUInt32() != 0x00004550)
            return false;

        var optionalOffset = peOffset + 24;
        stream.Seek(optionalOffset, SeekOrigin.Begin);
        var magic = reader.ReadUInt16();
        if (magic is not (0x10b or 0x20b))
            return false;

        stream.Seek(optionalOffset + 0x10, SeekOrigin.Begin);
        entryRva = reader.ReadUInt32();
        return entryRva != 0;
    }

    private static bool TryReadElfEntry(Stream stream, out ulong address)
    {
        address = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (stream.Length < 0x20)
            return false;

        var ident = reader.ReadBytes(4);
        if (ident[0] != 0x7f || ident[1] != (byte)'E' || ident[2] != (byte)'L' || ident[3] != (byte)'F')
            return false;

        var elfClass = reader.ReadByte();
        stream.Seek(elfClass == 2 ? 0x18L : 0x18L, SeekOrigin.Begin);
        address = elfClass == 2 ? reader.ReadUInt64() : reader.ReadUInt32();
        return address != 0;
    }

    private static ulong ReadUInt32At(Stream stream, long offset)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadUInt32();
    }

    private static ulong ReadUInt64At(Stream stream, long offset)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        return reader.ReadUInt64();
    }
}
