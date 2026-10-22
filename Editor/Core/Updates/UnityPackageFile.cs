using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Wireframe
{
    /// <summary>
    /// Reads which assets a .unitypackage holds without importing it. A .unitypackage is a gzipped tar with one
    /// folder per asset GUID holding "pathname" (where the asset goes), "asset" and "asset.meta".
    /// </summary>
    internal static class UnityPackageFile
    {
        private const string PathnameEntry = "/pathname";

        /// <summary>
        /// GUID → asset path (forward slashes, eg: Packages/com.veugeljame.builduploader/Editor/Core) for every asset
        /// and folder in the package.
        /// </summary>
        public static Dictionary<string, string> ReadContents(string unityPackagePath)
        {
            Dictionary<string, string> contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (FileStream file = File.OpenRead(unityPackagePath))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                byte[] header = new byte[512];
                string nextName = null;
                while (ReadFully(gzip, header, header.Length))
                {
                    if (IsEmpty(header))
                    {
                        break;
                    }

                    string name = nextName ?? EntryName(header);
                    nextName = null;
                    long size = ReadSize(header);
                    char type = (char)header[156];

                    // Long names come as their own entry before the one they name: GNU 'L' or a pax 'x' header.
                    bool wanted = type == 'L' || type == 'x' || name.EndsWith(PathnameEntry, StringComparison.Ordinal);
                    byte[] data = null;
                    if (wanted && size <= 1024 * 1024)
                    {
                        data = new byte[size];
                        if (!ReadFully(gzip, data, data.Length))
                        {
                            break;
                        }
                    }
                    else
                    {
                        Skip(gzip, size);
                    }

                    Skip(gzip, (512 - size % 512) % 512);

                    if (data == null)
                    {
                        continue;
                    }

                    if (type == 'L')
                    {
                        nextName = Encoding.UTF8.GetString(data).TrimEnd('\0');
                        continue;
                    }

                    if (type == 'x')
                    {
                        nextName = PaxPath(Encoding.UTF8.GetString(data));
                        continue;
                    }

                    // "<guid>/pathname", sometimes "./<guid>/pathname".
                    string guid = name.Substring(0, name.Length - PathnameEntry.Length);
                    int slash = guid.LastIndexOf('/');
                    if (slash >= 0)
                    {
                        guid = guid.Substring(slash + 1);
                    }

                    // Unity writes "<path>\n00".
                    string text = Encoding.UTF8.GetString(data);
                    int newLine = text.IndexOf('\n');
                    string assetPath = (newLine >= 0 ? text.Substring(0, newLine) : text).Trim().Replace('\\', '/');
                    if (guid.Length > 0 && assetPath.Length > 0)
                    {
                        contents[guid] = assetPath;
                    }
                }
            }

            return contents;
        }

        private static string EntryName(byte[] header)
        {
            string name = ReadString(header, 0, 100);
            bool ustar = ReadString(header, 257, 5) == "ustar";
            string prefix = ustar ? ReadString(header, 345, 155) : "";
            return prefix.Length > 0 ? prefix + "/" + name : name;
        }

        private static long ReadSize(byte[] header)
        {
            if ((header[124] & 0x80) != 0)
            {
                // GNU base-256 for sizes too big for 11 octal digits.
                long big = 0;
                for (int i = 125; i < 136; i++)
                {
                    big = (big << 8) | header[i];
                }

                return big;
            }

            long size = 0;
            for (int i = 124; i < 136; i++)
            {
                char c = (char)header[i];
                if (c >= '0' && c <= '7')
                {
                    size = size * 8 + (c - '0');
                }
                else if (size > 0 || (c != ' ' && c != '\0'))
                {
                    break;
                }
            }

            return size;
        }

        /// <summary>
        /// The path record of a pax header ("&lt;length&gt; path=&lt;value&gt;\n"), or null.
        /// </summary>
        private static string PaxPath(string records)
        {
            foreach (string record in records.Split('\n'))
            {
                int start = record.IndexOf(" path=", StringComparison.Ordinal);
                if (start >= 0)
                {
                    return record.Substring(start + " path=".Length);
                }
            }

            return null;
        }

        private static string ReadString(byte[] bytes, int start, int length)
        {
            int end = start;
            while (end < start + length && bytes[end] != 0)
            {
                end++;
            }

            return Encoding.UTF8.GetString(bytes, start, end - start);
        }

        private static bool IsEmpty(byte[] block)
        {
            foreach (byte b in block)
            {
                if (b != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ReadFully(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(buffer, read, count - read);
                if (n <= 0)
                {
                    return false;
                }

                read += n;
            }

            return true;
        }

        private static void Skip(Stream stream, long count)
        {
            byte[] buffer = new byte[8192];
            while (count > 0)
            {
                int n = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (n <= 0)
                {
                    return;
                }

                count -= n;
            }
        }
    }
}
