# BFontSharp

Encrypts and decrypts **BFTTF/BFOTF**, the wrappers Nintendo ships TrueType and OpenType fonts in.

```csharp
byte[] bfotf = File.ReadAllBytes("Symbol.bfotf");

byte[] otf = BFont.Decrypt(bfotf);                         // a plain .otf any font tool opens
BFontPlatform platform = BFont.DetectPlatform(bfotf)!.Value;

byte[] rebuilt = BFont.Encrypt(otf, platform);              // byte for byte the original
```

## Format

All big-endian. Each platform has its own 32-bit key:

| Platform | Key | Used by |
| --- | --- | --- |
| `NX` | `0x49621806` | TotK `.bfttf` |
| `Cafe` | `0x8CF2DCD9` | Wii U |
| `Win` | `0xA6018502` | TotK `.bfotf` |

| Offset | Size | |
| --- | --- | --- |
| 0x0 | u32 | `0x7F9A0218 ^ key` |
| 0x4 | u32 | Font size `^ key` |
| 0x8 | | The font, XORed with the key repeated every four bytes |

`Decrypt` also reads the files older TKVSC builds wrote, which have a zero size and key the font
with `key ^ size`.

## Build

```bash
dotnet build BFontSharp.sln -c Release
```

## Licence

AGPL-3.0-or-later. See [license.md](license.md).
