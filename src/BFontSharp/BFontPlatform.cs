namespace BFontSharp;

// Each platform XORs the font with its own 32-bit key. Tears of the Kingdom ships .bfttf files
// under the NX key and .bfotf files under the Win key.
public enum BFontPlatform : uint
{
    NX = 0x49621806,
    Cafe = 0x8CF2DCD9,
    Win = 0xA6018502,
}
