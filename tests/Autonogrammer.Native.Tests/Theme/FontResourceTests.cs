using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests;

/// <summary>
/// The bundled font files behind the token dictionary (VX-01). Each face ships unmodified, or as the derivation the manifest records (its SHA-256 matches
/// <c>Theme/FontManifest.txt</c>), with its SIL OFL 1.1 notice, and its embedded name table names the copyright holder
/// and the family the token URI uses. The sfnt and name tables are read directly from the binary with bounds checks, so
/// no Avalonia runtime is involved and malformed input throws instead of passing silently.
/// </summary>
public sealed class FontResourceTests
{
    private const string LicenceTitle = "SIL OPEN FONT LICENSE Version 1.1";
    private const uint TrueTypeSfntVersion = 0x00010000;
    private const ushort MicrosoftPlatformId = 3;
    private const int CopyrightNameId = 0;
    private const int FamilyNameId = 1;
    private const int TypographicFamilyNameId = 16;
    private const int WeightClassOffsetInOs2 = 4;
    private const int SfntHeaderLength = 12;
    private const int TableRecordLength = 16;
    private const int NameTableHeaderLength = 6;
    private const int NameRecordLength = 12;
    private static readonly int[] TokenFamilyNameIds = [FamilyNameId, TypographicFamilyNameId];

    /// <summary>The shipped TTF files, relative to the Assets/Fonts folder.</summary>
    public static TheoryData<string> FontFiles
    {
        get
        {
            return new TheoryData<string>
            {
                "DMSans/DMSans-Regular.ttf",
                "DMSans/DMSans-Medium.ttf",
                "DMSans/DMSans-SemiBold.ttf",
                "Anybody/Anybody-SemiBold.ttf",
                "SpaceMono/SpaceMono-Regular.ttf",
                "SpaceMono/SpaceMono-Bold.ttf",
            };
        }
    }

    /// <summary>Each family: TTF path, the copyright line its OFL.txt declares, and the family name the token URI uses.</summary>
    public static TheoryData<string, string, string> Families
    {
        get
        {
            return new TheoryData<string, string, string>
            {
                { "DMSans/DMSans-Regular.ttf", "Copyright 2014 The DM Sans Project Authors", "DM Sans" },
                { "DMSans/DMSans-Medium.ttf", "Copyright 2014 The DM Sans Project Authors", "DM Sans" },
                { "DMSans/DMSans-SemiBold.ttf", "Copyright 2014 The DM Sans Project Authors", "DM Sans" },
                { "Anybody/Anybody-SemiBold.ttf", "Copyright 2020 The Anybody Project Authors", "Anybody" },
                { "SpaceMono/SpaceMono-Regular.ttf", "Copyright 2016 The Space Mono Project Authors", "Space Mono" },
                { "SpaceMono/SpaceMono-Bold.ttf", "Copyright 2016 The Space Mono Project Authors", "Space Mono" },
            };
        }
    }

    /// <summary>Each family: TTF path and the copyright line its OFL.txt declares.</summary>
    public static TheoryData<string, string> FamilyNotices
    {
        get
        {
            return new TheoryData<string, string>
            {
                { "DMSans/DMSans-Regular.ttf", "Copyright 2014 The DM Sans Project Authors" },
                { "DMSans/DMSans-Medium.ttf", "Copyright 2014 The DM Sans Project Authors" },
                { "DMSans/DMSans-SemiBold.ttf", "Copyright 2014 The DM Sans Project Authors" },
                { "Anybody/Anybody-SemiBold.ttf", "Copyright 2020 The Anybody Project Authors" },
                { "SpaceMono/SpaceMono-Regular.ttf", "Copyright 2016 The Space Mono Project Authors" },
                { "SpaceMono/SpaceMono-Bold.ttf", "Copyright 2016 The Space Mono Project Authors" },
            };
        }
    }

    /// <summary>Each shipped TTF hashes to its manifest row, so the bytes are the upstream bytes or the recorded derivation.</summary>
    [Theory]
    [MemberData(nameof(FontFiles))]
    public void EachFontFileHashesToItsManifestRow(string relativePath)
    {
        var path = FontPath(relativePath);
        File.Exists(path).ShouldBeTrue($"the font {relativePath} must be shipped under {FontsRoot()}");

        var manifest = ReadManifest();
        manifest.ContainsKey(relativePath).ShouldBeTrue($"{relativePath} must have a row in {ManifestFile()}");
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))).ShouldBe(manifest[relativePath]);
    }

    /// <summary>Each family folder carries its unmodified OFL notice: the licence title, the copyright line and its holder.</summary>
    [Theory]
    [MemberData(nameof(FamilyNotices))]
    public void EachFamilyCarriesItsOflNotice(string relativePath, string copyrightLine)
    {
        var noticePath = Path.Combine(FontsRoot(), FamilyFolder(relativePath), "OFL.txt");
        File.Exists(noticePath).ShouldBeTrue($"the OFL notice {noticePath} must ship beside the font");

        var notice = File.ReadAllText(noticePath);
        notice.ShouldContain(LicenceTitle);
        notice.ShouldContain(copyrightLine);
        notice.ShouldContain(HolderOf(copyrightLine));
    }

    /// <summary>The embedded copyright names the OFL holder, and a family name matches the token URI fragment.</summary>
    [Theory]
    [MemberData(nameof(Families))]
    public void EmbeddedNameTableNamesTheHolderAndTheTokenFamily(string relativePath, string copyrightLine, string familyName)
    {
        var names = ReadNames(File.ReadAllBytes(FontPath(relativePath)));

        names.ContainsKey(CopyrightNameId).ShouldBeTrue($"{relativePath} must carry a copyright name (nameID 0)");
        names[CopyrightNameId].ShouldContain(HolderOf(copyrightLine));

        var familyCandidates = TokenFamilyNameIds
            .Select(nameId => names.GetValueOrDefault(nameId))
            .ToArray();
        familyCandidates.ShouldContain(familyName, StringComparer.Ordinal, $"{relativePath} must name the family '{familyName}' in nameID 1 or 16");
    }

    /// <summary>Every shipped face is a static instance (no fvar table) whose OS/2 weight class is the weight its file name states, because Avalonia cannot select a variable axis (R3).</summary>
    [Theory]
    [InlineData("DMSans/DMSans-Regular.ttf", 400)]
    [InlineData("DMSans/DMSans-Medium.ttf", 500)]
    [InlineData("DMSans/DMSans-SemiBold.ttf", 600)]
    [InlineData("Anybody/Anybody-SemiBold.ttf", 600)]
    [InlineData("SpaceMono/SpaceMono-Regular.ttf", 400)]
    [InlineData("SpaceMono/SpaceMono-Bold.ttf", 700)]
    public void EachShippedFaceIsAStaticInstanceOfItsStatedWeight(string relativePath, int weightClass)
    {
        var font = File.ReadAllBytes(FontPath(relativePath));

        Should.Throw<InvalidDataException>(() => FindTable(font, "fvar"), $"{relativePath} must not be a variable font");
        var (os2Offset, _) = FindTable(font, "OS/2");
        ReadUInt16(font, os2Offset + WeightClassOffsetInOs2).ShouldBe((ushort)weightClass);
    }

    /// <summary>The font folder holds exactly the manifest fonts and their OFL notices, so no other font file ships.</summary>
    [Fact]
    public void TheFontFolderHoldsOnlyTheManifestFontsAndTheirNotices()
    {
        var root = FontsRoot();
        Directory.Exists(root).ShouldBeTrue($"the font folder {root} must exist");

        var expected = ReadManifest().Keys
            .SelectMany(static relativePath => new[] { relativePath, FamilyFolder(relativePath) + "/OFL.txt" })
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actual = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected);
    }

    /// <summary>A file without the TrueType sfnt signature is rejected with a clear error, not read as a font.</summary>
    [Fact]
    public void NonTrueTypeInputIsRejected()
    {
        Should.Throw<InvalidDataException>(() => ReadNames(new byte[64]));
    }

    /// <summary>A font cut short inside its table directory is rejected, never partially read.</summary>
    [Fact]
    public void TruncatedFontIsRejectedNotSkipped()
    {
        var bytes = File.ReadAllBytes(FontPath("DMSans/DMSans-Regular.ttf"));

        Should.Throw<InvalidDataException>(() => ReadNames(bytes[..200]));
    }

    private static string FontsRoot() => Path.Combine(ShellHarness.RepoRoot(), "src", "Autonogrammer.Native.Views", "Assets", "Fonts");

    private static string FontPath(string relativePath) => Path.Combine(FontsRoot(), relativePath);

    private static string ManifestFile() => Path.Combine(ShellHarness.RepoRoot(), "tests", "Autonogrammer.Native.Tests", "Theme", "FontManifest.txt");

    private static string FamilyFolder(string relativePath) => relativePath.Split('/')[0];

    /// <summary>"Copyright 2014 The DM Sans Project Authors" gives the holder "The DM Sans Project Authors".</summary>
    private static string HolderOf(string copyrightLine) => copyrightLine.Split(' ', 3)[2];

    /// <summary>Reads FontManifest.txt into relative path to SHA-256, skipping comments and blank lines.</summary>
    private static Dictionary<string, string> ReadManifest()
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(ManifestFile()))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var columns = line.Split('|', StringSplitOptions.TrimEntries);
            columns.Length.ShouldBe(5, $"each manifest row needs five columns: {line}");
            rows.Add(columns[0], columns[1]);
        }

        return rows;
    }

    /// <summary>
    /// Reads the Microsoft (platform 3) name records of a TrueType file: nameID to string, first record per nameID wins.
    /// Every offset is bounds-checked before it is read, so a malformed file throws <see cref="InvalidDataException"/>.
    /// </summary>
    private static Dictionary<int, string> ReadNames(byte[] font)
    {
        if (ReadUInt32(font, 0) != TrueTypeSfntVersion)
        {
            throw new InvalidDataException("the file is not a TrueType sfnt (sfntVersion must be 0x00010000)");
        }

        var (nameOffset, nameLength) = FindTable(font, "name");
        Require(font, nameOffset, nameLength, "the name table");

        var count = ReadUInt16(font, nameOffset + 2);
        var storage = nameOffset + ReadUInt16(font, nameOffset + 4);
        Require(font, nameOffset, NameTableHeaderLength + (long)count * NameRecordLength, "the name records");

        var names = new Dictionary<int, string>();
        for (var index = 0; index < count; index++)
        {
            var record = nameOffset + NameTableHeaderLength + (long)index * NameRecordLength;
            if (ReadUInt16(font, record) != MicrosoftPlatformId)
            {
                continue;
            }

            var nameId = ReadUInt16(font, record + 6);
            var length = ReadUInt16(font, record + 8);
            var start = storage + ReadUInt16(font, record + 10);
            Require(font, start, length, $"the string of name record {nameId}");
            names.TryAdd(nameId, Encoding.BigEndianUnicode.GetString(font, (int)start, length));
        }

        return names;
    }

    /// <summary>Finds a table in the sfnt table directory and returns its offset and length.</summary>
    private static (long Offset, long Length) FindTable(byte[] font, string tag)
    {
        var tableCount = ReadUInt16(font, 4);
        for (var index = 0; index < tableCount; index++)
        {
            var record = SfntHeaderLength + (long)index * TableRecordLength;
            if (string.Equals(ReadTag(font, record), tag, StringComparison.Ordinal))
            {
                return (ReadUInt32(font, record + 8), ReadUInt32(font, record + 12));
            }
        }

        throw new InvalidDataException($"the font has no {tag} table");
    }

    private static void Require(byte[] font, long offset, long length, string what)
    {
        if (offset < 0 || length < 0 || offset > font.Length || length > font.Length - offset)
        {
            throw new InvalidDataException($"{what} (offset {offset}, length {length}) lies outside the {font.Length}-byte font");
        }
    }

    private static ushort ReadUInt16(byte[] font, long offset)
    {
        Require(font, offset, 2, "a 16-bit field");
        return BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan((int)offset));
    }

    private static long ReadUInt32(byte[] font, long offset)
    {
        Require(font, offset, 4, "a 32-bit field");
        return BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan((int)offset));
    }

    private static string ReadTag(byte[] font, long offset)
    {
        Require(font, offset, 4, "a table tag");
        return Encoding.ASCII.GetString(font, (int)offset, 4);
    }
}
