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
            uint legacyKey = key ^ (uint)body.Length;
            if (body.Length < 4 || !OpenFontMagics.Contains(BinaryPrimitives.ReadUInt32BigEndian(body) ^ legacyKey))
                return false;
            key = legacyKey;
            size = (uint)body.Length;
        }

        font = new byte[size];
        Xor(body[..(int)size], font, key);
        return true;
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
