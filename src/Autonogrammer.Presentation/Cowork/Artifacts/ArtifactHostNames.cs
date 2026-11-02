using System.Globalization;
using System.Text;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// Host name display for the Access dialog: punycode and decoded forms, and a mixed-script check. The script check is an
/// approximation over common letter blocks; it marks a label that mixes two of them, and it never decides a host is safe.
/// </summary>
public static class ArtifactHostNames
{
    /// <summary>Builds the display form of one host.</summary>
    /// <param name="host">The host name as the artifact declared it.</param>
    /// <returns>The ASCII form, the decoded form and the mixed-script mark.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public static ArtifactOriginView Describe(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var ascii = AsciiForm(host);
        var decoded = DecodedForm(ascii);
        return new ArtifactOriginView(ascii, decoded, MixedScript(decoded));
    }

    /// <summary>Returns the punycode form of a host, or the input unchanged when it cannot be converted.</summary>
    /// <param name="host">The host name.</param>
    /// <returns>The ASCII form.</returns>
    public static string AsciiForm(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            return new IdnMapping { UseStd3AsciiRules = true }.GetAscii(host);
        }
        catch (ArgumentException)
        {
            return host;
        }
    }

    /// <summary>Returns the decoded form of a punycode host, or the input unchanged when it cannot be decoded.</summary>
    /// <param name="ascii">The ASCII form.</param>
    /// <returns>The decoded form.</returns>
    public static string DecodedForm(string ascii)
    {
        ArgumentNullException.ThrowIfNull(ascii);
        try
        {
            return new IdnMapping { UseStd3AsciiRules = true }.GetUnicode(ascii);
        }
        catch (ArgumentException)
        {
            return ascii;
        }
    }

    /// <summary>Returns whether one dot-separated label mixes two script blocks.</summary>
    /// <param name="host">The host name, in any form.</param>
    /// <returns>True when a label mixes scripts.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public static bool MixedScript(string host)
    {
        ArgumentNullException.ThrowIfNull(host);
        foreach (var label in host.Split('.'))
        {
            var scripts = new HashSet<int>();
            foreach (var rune in label.EnumerateRunes())
            {
                var script = ScriptOf(rune);
                if (script >= 0)
                {
                    scripts.Add(script);
                }
            }

            if (scripts.Count > 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int ScriptOf(Rune rune)
    {
        var value = rune.Value;
        if (value < 0x80)
        {
            return char.IsAsciiLetter((char)value) ? 0 : -1;
        }

        if (Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.OtherLetter))
        {
            return -1;
        }

        return value switch
        {
            >= 0x00C0 and <= 0x024F and not 0x00D7 and not 0x00F7 => 0,
            >= 0x1E00 and <= 0x1EFF => 0,
            >= 0x0370 and <= 0x03FF => 1,
            >= 0x1F00 and <= 0x1FFF => 1,
            >= 0x0400 and <= 0x052F => 2,
            >= 0x0530 and <= 0x058F => 3,
            >= 0x0590 and <= 0x05FF => 4,
            >= 0x0600 and <= 0x06FF => 5,
            _ => 6,
        };
    }
}
