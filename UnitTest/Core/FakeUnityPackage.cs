using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Wireframe.UnitTest
{
    /// <summary>
    /// Writes .unitypackage files laid out like Unity's: a gzipped tar with a folder per GUID holding "pathname"
    /// ("&lt;path&gt;\n00"), "asset" and "asset.meta". Nothing is imported.
    /// </summary>
    internal static class FakeUnityPackage
    {
        internal class Entry
        {
            public string Guid;
            public string Path;

            /// <summary>
            /// Null for a folder.
            /// </summary>
            public string Content;

            /// <summary>
            /// Write the entry as "./&lt;guid&gt;/..." like some tar tools do.
            /// </summary>
            public bool DotPrefix;

            /// <summary>
            /// Write the pathname entry's name through a GNU long name record.
            /// </summary>
            public bool LongName;

            public Entry(string guid, string path, string content = null)
            {
                Guid = guid;
                Path = path;
                Content = content;
            }
        }

        public static void Write(string path, params Entry[] entries)
        {
            using (FileStream file = File.Create(path))
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
            {
                foreach (Entry entry in entries)
                {
                    string folder = (entry.DotPrefix ? "./" : "") + entry.Guid;
                    WriteEntry(gzip, folder, new byte[0], '5');
                    string pathname = folder + "/pathname";
                    if (entry.LongName)
                    {
                        WriteEntry(gzip, "././@LongLink", Encoding.UTF8.GetBytes(pathname + "\0"), 'L');
                        pathname = "truncated";
                    }

                    WriteEntry(gzip, pathname, Encoding.UTF8.GetBytes(entry.Path + "\n00"), '0');
                    if (entry.Content != null)
                    {
                        WriteEntry(gzip, folder + "/asset", Encoding.UTF8.GetBytes(entry.Content), '0');
                    }

                    WriteEntry(gzip, folder + "/asset.meta", Encoding.UTF8.GetBytes("fileFormatVersion: 2\nguid: " + entry.Guid + "\n"), '0');
                }

                // A tar ends with two empty blocks.
                gzip.Write(new byte[1024], 0, 1024);
            }
        }

        private static void WriteEntry(Stream tar, string name, byte[] data, char type)
        {
            byte[] header = new byte[512];
            Put(header, 0, name, 100);
            Put(header, 100, "0000644", 8);
            Put(header, 108, "0000000", 8);
            Put(header, 116, "0000000", 8);
            Put(header, 124, Convert.ToString(data.Length, 8).PadLeft(11, '0'), 12);
            Put(header, 136, Convert.ToString(1700000000, 8).PadLeft(11, '0'), 12);
            header[156] = (byte)type;
            Put(header, 257, "ustar", 6);
            Put(header, 263, "00", 2);

            for (int i = 148; i < 156; i++)
            {
                header[i] = (byte)' ';
            }

            int checksum = 0;
            foreach (byte b in header)
            {
                checksum += b;
            }

            Put(header, 148, Convert.ToString(checksum, 8).PadLeft(6, '0'), 7);

            tar.Write(header, 0, header.Length);
            tar.Write(data, 0, data.Length);
            int padding = (512 - data.Length % 512) % 512;
            tar.Write(new byte[padding], 0, padding);
        }

        private static void Put(byte[] header, int offset, string text, int length)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            Array.Copy(bytes, 0, header, offset, Math.Min(bytes.Length, length));
        }
    }
}
