using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace BFontSharp;

public static class BFont
{
    private const uint MagicSeed = 0x7F9A0218;
    private const int HeaderSize = 8;

    private static readonly uint[] OpenFontMagics =
    [
        0x00010000, // TrueType
        0x4F54544F, // OTTO
        0x74727565, // true
        0x74746366, // ttcf
        0x774F4646, // wOFF
        0x774F4632, // wOF2
    ];

    /// <summary>The platform whose key the file is encrypted with, or null if it isn't a BFTTF/BFOTF.</summary>
    public static BFontPlatform? DetectPlatform(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize) return null;
        uint key = BinaryPrimitives.ReadUInt32BigEndian(data) ^ MagicSeed;
        return Enum.IsDefined((BFontPlatform)key) ? (BFontPlatform)key : null;
    }

    public static bool IsEncrypted(ReadOnlySpan<byte> data) => DetectPlatform(data) is not null;

    /// <summary>True for plain TrueType, OpenType, collection and WOFF data.</summary>
    public static bool IsOpenFont(ReadOnlySpan<byte> data)
        => data.Length >= 4 && OpenFontMagics.Contains(BinaryPrimitives.ReadUInt32BigEndian(data));

    public static byte[] Decrypt(ReadOnlySpan<byte> data)
        => TryDecrypt(data, out byte[]? font) ? font : throw new InvalidDataException("Not a BFTTF/BFOTF font.");

    public static bool TryDecrypt(ReadOnlySpan<byte> data, [NotNullWhen(true)] out byte[]? font)
    {
        font = null;
        if (DetectPlatform(data) is not { } platform) return false;

        uint key = (uint)platform;
        uint size = BinaryPrimitives.ReadUInt32BigEndian(data[4..]) ^ key;
        ReadOnlySpan<byte> body = data[HeaderSize..];

        if (size > body.Length)
        {
            if (body.Length < 4) return false;
            uint first = BinaryPrimitives.ReadUInt32BigEndian(body);
            uint legacyKey = key ^ (uint)body.Length;
            if (OpenFontMagics.Contains(first ^ legacyKey)) key = legacyKey;
            else if (!OpenFontMagics.Contains(first ^ key)) return false;
            size = (uint)body.Length;
        }

        font = new byte[size];
        Xor(body[..(int)size], font, key);
        return true;
    }

    // ---- by file name ----

    /// <summary>True for a font file by its name: <c>.bfotf</c>, <c>.bfttf</c>, <c>.ttf</c> or <c>.otf</c>, with or without <c>.zs</c>.</summary>
    public static bool IsFontPath(string path) => HasExtension(path, ".bfotf", ".bfttf", ".ttf", ".otf");

    /// <summary>True for the names the game keeps encrypted fonts under: <c>.bfotf</c> and <c>.bfttf</c> (with or without <c>.zs</c>).</summary>
    public static bool IsEncryptedFontPath(string path) => HasExtension(path, ".bfotf", ".bfttf");

    /// <summary>
    /// The key a font file of this name is made with in Tears of the Kingdom: <c>.bfttf</c> uses the NX key and
    /// <c>.bfotf</c> the Win key (the game's own files all follow this). Other names get <see cref="BFontPlatform.NX"/>.
    /// </summary>
    public static BFontPlatform PlatformFor(string path)
        => HasExtension(path, ".bfotf") ? BFontPlatform.Win : BFontPlatform.NX;

    /// <summary>The font as an editor or font tool wants it: decrypted if it is a BFTTF/BFOTF, otherwise as it is.</summary>
    public static byte[] ToOpenFont(byte[] data) => TryDecrypt(data, out byte[]? font) ? font : data;

    /// <summary>
    /// The bytes to write to <paramref name="targetPath"/> for an imported font. For a <c>.bfotf</c> or <c>.bfttf</c> target
    /// that is the font encrypted, with the key of the file it replaces (<paramref name="existingTarget"/>, as stored, if given)
    /// and otherwise the key the game uses for that name; an import that is already encrypted is kept as it is. For any other
    /// target it is the plain font, decrypted if need be.
    /// </summary>
    public static byte[] PrepareReplacement(ReadOnlySpan<byte> imported, string targetPath, ReadOnlySpan<byte> existingTarget = default)
    {
        bool intoGameFont = IsEncryptedFontPath(targetPath);
        if (IsEncrypted(imported))
        {
            if (intoGameFont) return imported.ToArray();
            return TryDecrypt(imported, out byte[]? decrypted) && IsOpenFont(decrypted)
                ? decrypted
                : throw new InvalidDataException("Not a valid font file.");
        }

        if (!IsOpenFont(imported)) throw new InvalidDataException("Not a valid TrueType or OpenType font file.");
        if (!intoGameFont) return imported.ToArray();

        BFontPlatform platform = DetectPlatform(existingTarget) ?? PlatformFor(targetPath);
        return Encrypt(imported, platform);
    }

    private static bool HasExtension(string path, params string[] extensions)
    {
        ReadOnlySpan<char> name = path.AsSpan();
        if (name.EndsWith(".zs", StringComparison.OrdinalIgnoreCase)) name = name[..^3];
        foreach (string extension in extensions)
            if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> font, BFontPlatform platform = BFontPlatform.NX)
    {
        if (!Enum.IsDefined(platform)) throw new ArgumentOutOfRangeException(nameof(platform));
        if (!IsOpenFont(font)) throw new InvalidDataException("Not a TrueType or OpenType font.");

        uint key = (uint)platform;
        byte[] output = new byte[HeaderSize + font.Length];
        BinaryPrimitives.WriteUInt32BigEndian(output, MagicSeed ^ key);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4), (uint)font.Length ^ key);
        Xor(font, output.AsSpan(HeaderSize), key);
        return output;
    }

    private static void Xor(ReadOnlySpan<byte> source, Span<byte> destination, uint key)
    {
        Span<byte> keyBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(keyBytes, key);
        for (int i = 0; i < source.Length; i++)
            destination[i] = (byte)(source[i] ^ keyBytes[i & 3]);
    }
}
