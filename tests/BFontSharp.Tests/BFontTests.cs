using System.Buffers.Binary;

namespace BFontSharp.Tests;

public class BFontTests
{
    // Not a real font, but it starts like one and has bytes that every key position touches.
    private static byte[] Otf(int length = 103)
    {
        byte[] font = new byte[length];
        "OTTO"u8.CopyTo(font);
        for (int i = 4; i < font.Length; i++) font[i] = (byte)(i * 31 + 7);
        return font;
    }

    [Theory]
    [InlineData(BFontPlatform.NX)]
    [InlineData(BFontPlatform.Cafe)]
    [InlineData(BFontPlatform.Win)]
    public void AFontEncryptsAndDecryptsBack(BFontPlatform platform)
    {
        byte[] font = Otf();

        byte[] wrapped = BFont.Encrypt(font, platform);

        Assert.Equal(platform, BFont.DetectPlatform(wrapped));
        Assert.Equal(font, BFont.Decrypt(wrapped));
        Assert.Equal(wrapped, BFont.Encrypt(BFont.Decrypt(wrapped), platform));
    }

    [Fact]
    public void TheHeaderMatchesWhatTheGameShips()
    {
        // The first word of an NX file is 36 F8 1A 1E and of a Win file D9 9B 87 1A, and the size follows, XORed with the key.
        byte[] nx = BFont.Encrypt(Otf(), BFontPlatform.NX);
        byte[] win = BFont.Encrypt(Otf(), BFontPlatform.Win);

        Assert.Equal(new byte[] { 0x36, 0xF8, 0x1A, 0x1E }, nx[..4]);
        Assert.Equal(new byte[] { 0xD9, 0x9B, 0x87, 0x1A }, win[..4]);
        Assert.Equal(103u ^ 0x49621806u, BinaryPrimitives.ReadUInt32BigEndian(nx.AsSpan(4)));
    }

    [Fact]
    public void FilesWithAZeroSizeFromOlderBuildsStillDecrypt()
    {
        byte[] font = Otf();
        // key ^ length of the body, size field zero.
        uint key = (uint)BFontPlatform.NX ^ (uint)font.Length;
        byte[] old = new byte[8 + font.Length];
        BinaryPrimitives.WriteUInt32BigEndian(old, 0x7F9A0218u ^ (uint)BFontPlatform.NX);
        Span<byte> keyBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(keyBytes, key);
        for (int i = 0; i < font.Length; i++) old[8 + i] = (byte)(font[i] ^ keyBytes[i & 3]);

        Assert.Equal(font, BFont.Decrypt(old));
    }

    [Fact]
    public void AFontWrittenUnderThePlainKeyWithNoSizeStillDecrypts()
    {
        byte[] font = Otf();
        byte[] odd = new byte[8 + font.Length];
        BinaryPrimitives.WriteUInt32BigEndian(odd, 0x7F9A0218u ^ (uint)BFontPlatform.Win);
        Span<byte> keyBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(keyBytes, (uint)BFontPlatform.Win);
        for (int i = 0; i < font.Length; i++) odd[8 + i] = (byte)(font[i] ^ keyBytes[i & 3]);

        Assert.Equal(font, BFont.Decrypt(odd));
    }

    [Fact]
    public void ThingsThatAreNotWrappedFontsAreRefused()
    {
        Assert.False(BFont.TryDecrypt(Otf(), out _));
        Assert.False(BFont.TryDecrypt(new byte[4], out _));
        Assert.Throws<InvalidDataException>(() => BFont.Encrypt(new byte[40]));
        Assert.Equal(Otf(), BFont.ToOpenFont(Otf()));
    }

    [Theory]
    [InlineData("Font/Symbol.bfotf", true, true)]
    [InlineData("Font/DFP_GB_H3.BFTTF", true, true)]
    [InlineData("C:/mod/Symbol.bfotf.zs", true, true)]
    [InlineData("Arial.ttf", true, false)]
    [InlineData("Font.otf", true, false)]
    [InlineData("Font.bfarc.zs", false, false)]
    public void FontsAreToldByName(string path, bool isFont, bool isGameFont)
    {
        Assert.Equal(isFont, BFont.IsFontPath(path));
        Assert.Equal(isGameFont, BFont.IsEncryptedFontPath(path));
    }

    [Fact]
    public void TheGamesKeyForANameIsNxForBfttfAndWinForBfotf()
    {
        Assert.Equal(BFontPlatform.NX, BFont.PlatformFor("a/b.bfttf"));
        Assert.Equal(BFontPlatform.NX, BFont.PlatformFor("a/b.bfttf.zs"));
        Assert.Equal(BFontPlatform.Win, BFont.PlatformFor("a/b.bfotf"));
        Assert.Equal(BFontPlatform.Win, BFont.PlatformFor("a/b.BFOTF.zs"));
    }

    [Fact]
    public void ANewFontForABfotfIsEncryptedWithTheWinKey()
    {
        byte[] wrapped = BFont.PrepareReplacement(Otf(), "Font/Symbol.bfotf");

        Assert.Equal(BFontPlatform.Win, BFont.DetectPlatform(wrapped));
        Assert.Equal(Otf(), BFont.Decrypt(wrapped));
    }

    [Fact]
    public void ANewFontTakesTheKeyOfTheFileItReplaces()
    {
        byte[] existing = BFont.Encrypt(Otf(40), BFontPlatform.Cafe);

        byte[] wrapped = BFont.PrepareReplacement(Otf(), "Font/Symbol.bfotf", existing);

        Assert.Equal(BFontPlatform.Cafe, BFont.DetectPlatform(wrapped));
    }

    [Fact]
    public void ANewFontForAPlainNameStaysPlain()
        => Assert.Equal(Otf(), BFont.PrepareReplacement(Otf(), "mod/Custom.otf"));

    [Fact]
    public void AnAlreadyEncryptedFontIsKeptForAGameNameAndDecryptedForAPlainOne()
    {
        byte[] wrapped = BFont.Encrypt(Otf(), BFontPlatform.NX);

        Assert.Equal(wrapped, BFont.PrepareReplacement(wrapped, "Font/Other.bfttf"));
        Assert.Equal(Otf(), BFont.PrepareReplacement(wrapped, "Other.ttf"));
    }

    [Fact]
    public void ANonFontIsRefusedAsAReplacement()
        => Assert.Throws<InvalidDataException>(() => BFont.PrepareReplacement(new byte[100], "Font/Symbol.bfotf"));
}
