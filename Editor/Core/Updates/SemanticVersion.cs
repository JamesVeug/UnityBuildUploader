using System;
using System.Globalization;

namespace Wireframe
{
    /// <summary>
    /// A package or release version such as 3.4.0, v3.4.0 or 3.4.0-rc.1.
    /// Pre-releases sort before their release. Letters straight after the last number (2.1.1a) count as a
    /// pre-release too, because that is how early tags were named.
    /// </summary>
    internal readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;
        private readonly string m_preRelease;

        public string PreRelease => m_preRelease ?? "";
        public bool IsPreRelease => !string.IsNullOrEmpty(m_preRelease);

        public SemanticVersion(int major, int minor, int patch, string preRelease = "")
        {
            Major = major;
            Minor = minor;
            Patch = patch;
            m_preRelease = preRelease ?? "";
        }

        /// <summary>
        /// Needs at least major.minor so branch names and commit hashes (develop, e1dfc2a) are never versions.
        /// </summary>
        public static bool TryParse(string text, out SemanticVersion version)
        {
            version = default(SemanticVersion);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            int buildIndex = text.IndexOf('+');
            if (buildIndex >= 0)
            {
                text = text.Substring(0, buildIndex);
            }

            string preRelease = "";
            int dashIndex = text.IndexOf('-');
            if (dashIndex >= 0)
            {
                preRelease = text.Substring(dashIndex + 1);
                text = text.Substring(0, dashIndex);
                if (preRelease.Length == 0)
                {
                    return false;
                }
            }

            int suffixIndex = text.Length;
            while (suffixIndex > 0 && IsLetter(text[suffixIndex - 1]))
            {
                suffixIndex--;
            }

            if (suffixIndex < text.Length)
            {
                if (preRelease.Length > 0)
                {
                    return false;
                }

                preRelease = text.Substring(suffixIndex);
                text = text.Substring(0, suffixIndex);
            }

            string[] parts = text.Split('.');
            if (parts.Length < 2 || parts.Length > 3)
            {
                return false;
            }

            int[] numbers = new int[3];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                {
                    return false;
                }
            }

            version = new SemanticVersion(numbers[0], numbers[1], numbers[2], preRelease);
            return true;
        }

        private static bool IsLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        public int CompareTo(SemanticVersion other)
        {
            int result = Major.CompareTo(other.Major);
            if (result != 0)
            {
                return result;
            }

            result = Minor.CompareTo(other.Minor);
            if (result != 0)
            {
                return result;
            }

            result = Patch.CompareTo(other.Patch);
            if (result != 0)
            {
                return result;
            }

            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        /// <summary>
        /// Same order as semver.org: no pre-release beats any pre-release, then each dot separated part is
        /// compared, numbers by value and before words.
        /// </summary>
        private static int ComparePreRelease(string a, string b)
        {
            if (a.Length == 0 || b.Length == 0)
            {
                return b.Length.CompareTo(a.Length);
            }

            string[] aParts = a.Split('.');
            string[] bParts = b.Split('.');
            for (int i = 0; i < Math.Min(aParts.Length, bParts.Length); i++)
            {
                bool aIsNumber = int.TryParse(aParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int aNumber);
                bool bIsNumber = int.TryParse(bParts[i], NumberStyles.None, CultureInfo.InvariantCulture, out int bNumber);

                int result;
                if (aIsNumber && bIsNumber)
                {
                    result = aNumber.CompareTo(bNumber);
                }
                else if (aIsNumber)
                {
                    result = -1;
                }
                else if (bIsNumber)
                {
                    result = 1;
                }
                else
                {
                    result = string.CompareOrdinal(aParts[i], bParts[i]);
                }

                if (result != 0)
                {
                    return Math.Sign(result);
                }
            }

            return aParts.Length.CompareTo(bParts.Length);
        }

        public bool Equals(SemanticVersion other)
        {
            return CompareTo(other) == 0;
        }

        public override bool Equals(object obj)
        {
            return obj is SemanticVersion other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Major;
                hash = hash * 397 ^ Minor;
                hash = hash * 397 ^ Patch;
                hash = hash * 397 ^ PreRelease.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            string version = $"{Major}.{Minor}.{Patch}";
            return IsPreRelease ? version + "-" + PreRelease : version;
        }

        public static bool operator ==(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) == 0;
        public static bool operator !=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) != 0;
        public static bool operator <(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) < 0;
        public static bool operator >(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) > 0;
        public static bool operator <=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) <= 0;
        public static bool operator >=(SemanticVersion a, SemanticVersion b) => a.CompareTo(b) >= 0;
    }
}
