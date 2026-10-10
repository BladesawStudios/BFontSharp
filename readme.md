# BFontSharp

Encrypts and decrypts **BFTTF/BFOTF**, the wrappers Nintendo ships TrueType and OpenType fonts in.

```csharp
byte[] bfotf = File.ReadAllBytes("Symbol.bfotf");

byte[] otf = BFont.Decrypt(bfotf);                         // a plain .otf any font tool opens
BFontPlatform platform = BFont.DetectPlatform(bfotf)!.Value;

byte[] rebuilt = BFont.Encrypt(otf, platform);              // byte for byte the original
```

Replacing a font in a mod takes one call. It encrypts a plain font for a `.bfotf` or `.bfttf` target with the key of the
file it replaces, or with the key the game uses for that name (`.bfttf` NX, `.bfotf` Win), and leaves a plain target plain:

```csharp
byte[] toWrite = BFont.PrepareReplacement(File.ReadAllBytes("MyFont.otf"), "Font/scft/Symbol.bfotf", existingStoredBytes);

BFont.IsFontPath("a/Symbol.bfotf.zs");      // true
BFont.IsEncryptedFontPath("a/Arial.ttf");   // false
BFont.ToOpenFont(bytes);                    // decrypted if it is a BFTTF/BFOTF, otherwise as it is
```

Checked against all 49 fonts in the four `Font*.bfarc.zs` archives of the 1.2.1 dump: each decrypts to a font, and encrypting
that again gives the stored bytes exactly.

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
with `key ^ size`, and files keyed with the plain platform key and no size.

## Build

```bash
dotnet build BFontSharp.sln -c Release
```

## Licence

AGPL-3.0-or-later. See [license.md](license.md).
