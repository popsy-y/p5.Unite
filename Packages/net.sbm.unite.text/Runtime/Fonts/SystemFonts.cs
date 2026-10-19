using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Unite.Text.Unicode;
using UnityEngine;

namespace Unite.Text.Fonts
{
    /// <summary>
    /// Last-resort fallback to the fonts installed on the machine, for characters none of
    /// the fonts given to TextFont() can draw. The first miss starts a background scan
    /// that reads only the cmap / name / OS/2 tables of every font file (collections
    /// included); the font picked for a character is then parsed in the background too.
    /// Until it is ready the character draws as the primary font's .notdef (tofu), and
    /// <see cref="Generation"/> changes when a font arrives so cached text re-shapes.
    /// Platforms without font paths (mobile, WebGL) always draw tofu.
    /// </summary>
    internal static class SystemFonts
    {
        private enum IndexState
        {
            Idle,
            Indexing,
            Ready,
            Unavailable
        }

        private sealed class Entry
        {
            public string Path;
            public int Index;
            public string Family;
            public CharacterMap Map;
            public bool Regular;
            public int Weight;
            public int Rank;
            public bool InSystemDirectory;
            public bool Loading;
            public bool Failed;
            public int FaceIndex = -1;
        }

        private static readonly List<FontFace> Loaded = new();
        private static readonly ConcurrentQueue<(Entry Entry, FontFace Face)> Completed = new();
        private static readonly HashSet<int> Uncovered = new();
        private static volatile IndexState _state = IndexState.Idle;
        private static List<Entry> _entries;

        /// <summary>Changes whenever a system font finishes loading.</summary>
        public static int Generation { get; private set; }

        /// <summary>Timings for diagnostics: font path query (main thread), index build and font loads (workers), in ms.</summary>
        public static double PathQueryMilliseconds { get; private set; }
        public static double IndexMilliseconds { get; private set; }
        public static readonly ConcurrentQueue<(string Family, double Milliseconds)> LoadTimes = new();

        public static FontFace FaceAt(int index) => Loaded[index];

        public static int LoadedCount => Loaded.Count;

        /// <summary>Main thread: publishes fonts loaded in the background. Cheap when nothing is pending.</summary>
        public static void Poll()
        {
            while (Completed.TryDequeue(out var completed))
            {
                completed.Entry.Loading = false;
                if (completed.Face == null)
                {
                    completed.Entry.Failed = true;
                    continue;
                }

                completed.Entry.FaceIndex = Loaded.Count;
                Loaded.Add(completed.Face);
                Uncovered.Clear();
                Generation++;
            }
        }

        /// <summary>
        /// The system face (index into the loaded system fonts) that draws every character
        /// of a cluster, or -1 while none is known or ready yet.
        /// </summary>
        public static int FindFace(ReadOnlySpan<int> cluster)
        {
            if (!FontFace.SystemFontFallback)
                return -1;

            Poll();
            for (int face = 0; face < Loaded.Count; face++)
            {
                if (Covers(Loaded[face].Default, cluster))
                    return face;
            }

            int key = cluster.Length > 0 ? cluster[0] : 0;
            if (Uncovered.Contains(key))
                return -1;

            if (_state == IndexState.Idle)
                StartIndexing();
            if (_state != IndexState.Ready)
                return -1;

            foreach (var entry in _entries)
            {
                if (entry.Failed || !CoversMap(entry.Map, cluster))
                    continue;

                if (entry.FaceIndex >= 0)
                    return entry.FaceIndex;
                if (!entry.Loading)
                    StartLoading(entry);
                return -1;
            }

            Uncovered.Add(key);
            return -1;
        }

        /// <summary>Whether a font has glyphs for every visible character of a cluster (decompositions count).</summary>
        public static bool Covers(FontInstance font, ReadOnlySpan<int> cluster)
        {
            foreach (int codePoint in cluster)
            {
                if (!IsCovered(font, codePoint))
                    return false;
            }
            return true;
        }

        private static bool IsCovered(FontInstance font, int codePoint)
        {
            if (codePoint == '\t' || UnicodeData.IsDefaultIgnorable(codePoint) || !Shaping.BasicShaper.HasGlyph(codePoint))
                return true;
            if (font.GetGlyphId(codePoint) != 0)
                return true;
            return UnicodeData.TryDecompose(codePoint, out int first, out int second)
                && IsCovered(font, first) && (second == 0 || IsCovered(font, second));
        }

        private static bool CoversMap(CharacterMap map, ReadOnlySpan<int> cluster)
        {
            foreach (int codePoint in cluster)
            {
                if (codePoint == '\t' || UnicodeData.IsDefaultIgnorable(codePoint) || !Shaping.BasicShaper.HasGlyph(codePoint))
                    continue;
                if (map.Lookup(codePoint) == 0)
                    return false;
            }
            return true;
        }

        // Font.GetPathsToOSFonts() costs ~90 ms on the main thread, so the font folders of
        // the desktop platforms are listed on the worker instead.
        private static void StartIndexing()
        {
            var query = System.Diagnostics.Stopwatch.StartNew();
            var platform = Application.platform;
            string[] folders = FontFolders(platform);
            PathQueryMilliseconds = query.Elapsed.TotalMilliseconds;
            if (folders.Length == 0)
            {
                _state = IndexState.Unavailable;
                return;
            }

            _state = IndexState.Indexing;
            Task.Run(() =>
            {
                try
                {
                    BuildIndex(folders);
                }
                catch (Exception)
                {
                    _state = IndexState.Unavailable;
                }
            });
        }

        private static void BuildIndex(string[] folders)
        {
            {
                var paths = ListFontFiles(folders);
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var entries = new List<Entry>();
                foreach (string path in paths)
                {
                    try
                    {
                        ReadFile(path, entries);
                    }
                    catch (Exception)
                    {
                        // Unreadable or unsupported files are left out.
                    }
                }

                // The operating systems' own fallback families first, fonts that ship with
                // the system before user-installed ones, upright regular faces, then by path.
                foreach (var entry in entries)
                {
                    entry.Rank = PreferredRank(entry.Family);
                    entry.InSystemDirectory = IsSystemDirectory(entry.Path);
                }
                entries.Sort((left, right) =>
                {
                    int order = left.Rank.CompareTo(right.Rank);
                    if (order != 0)
                        return order;
                    order = right.InSystemDirectory.CompareTo(left.InSystemDirectory);
                    if (order != 0)
                        return order;
                    order = right.Regular.CompareTo(left.Regular);
                    if (order != 0)
                        return order;
                    order = Math.Abs(left.Weight - 400).CompareTo(Math.Abs(right.Weight - 400));
                    return order != 0 ? order : string.CompareOrdinal(left.Path, right.Path);
                });
                _entries = entries;
                IndexMilliseconds = timer.Elapsed.TotalMilliseconds;
                _state = IndexState.Ready;
            }
        }

        // Families the platforms use for their own fallback, roughly by script; the first
        // one that covers a character wins. Noto families rank right after them.
        private static readonly string[] PreferredFamilies =
        {
            // Windows
            "Segoe UI", "Segoe UI Emoji", "Segoe UI Symbol", "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic",
            "Malgun Gothic", "Microsoft YaHei", "Microsoft JhengHei", "SimSun", "Nirmala UI", "Leelawadee UI",
            "Ebrima", "Gadugi", "Segoe UI Historic", "Mongolian Baiti", "Microsoft Himalaya", "Myanmar Text",
            "Javanese Text", "Microsoft Yi Baiti", "Microsoft New Tai Lue", "Microsoft Tai Le", "Phagspa",
            "MV Boli", "Sylfaen", "Arial", "Tahoma", "Times New Roman",
            // macOS
            "Helvetica Neue", "Apple Color Emoji", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "PingFang SC",
            "PingFang TC", "Apple SD Gothic Neo", "Geeza Pro", "Arial Hebrew", "Kohinoor Devanagari", "Thonburi",
            "Apple Symbols",
            // Linux
            "DejaVu Sans", "Noto Sans", "Noto Sans CJK JP", "Noto Color Emoji",
        };

        private static int PreferredRank(string family)
        {
            if (string.IsNullOrEmpty(family))
                return int.MaxValue;
            for (int index = 0; index < PreferredFamilies.Length; index++)
            {
                if (string.Equals(family, PreferredFamilies[index], StringComparison.OrdinalIgnoreCase))
                    return index;
            }
            return family.StartsWith("Noto ", StringComparison.OrdinalIgnoreCase) ? PreferredFamilies.Length : int.MaxValue;
        }

        private static bool IsSystemDirectory(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Windows/Fonts/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.StartsWith("/System/Library/Fonts", StringComparison.Ordinal)
                || normalized.StartsWith("/Library/Fonts", StringComparison.Ordinal)
                || normalized.StartsWith("/usr/share/fonts", StringComparison.Ordinal);
        }

        private static string[] FontFolders(RuntimePlatform platform)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            switch (platform)
            {
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer:
                    return new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts")
                    };
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.OSXPlayer:
                    return new[] { "/System/Library/Fonts", "/Library/Fonts", Path.Combine(home, "Library", "Fonts") };
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.LinuxPlayer:
                    return new[] { "/usr/share/fonts", "/usr/local/share/fonts", Path.Combine(home, ".local", "share", "fonts"), Path.Combine(home, ".fonts") };
                default:
                    return Array.Empty<string>();
            }
        }

        private static List<string> ListFontFiles(string[] folders)
        {
            var paths = new List<string>();
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                    continue;
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (extension is ".ttf" or ".otf" or ".ttc" or ".otc")
                            paths.Add(file);
                    }
                }
                catch (Exception)
                {
                    // Unreadable folders are skipped.
                }
            }
            return paths;
        }

        private static void StartLoading(Entry entry)
        {
            entry.Loading = true;
            Task.Run(() =>
            {
                FontFace face = null;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    face = FontFace.LoadCollectionMember(File.ReadAllBytes(entry.Path), entry.Index, entry.Family);
                    LoadTimes.Enqueue((entry.Family, timer.Elapsed.TotalMilliseconds));
                }
                catch (Exception)
                {
                    face = null;
                }
                Completed.Enqueue((entry, face));
            });
        }

        // Reads the table directories of a font file (or of each font of a collection) and
        // keeps the character map, family name and style of each font.
        private static void ReadFile(string path, List<Entry> entries)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            uint tag = ReadU32(stream, 0);
            if (tag == SfntDirectory.CollectionVersion)
            {
                int count = (int)ReadU32(stream, 8);
                for (int index = 0; index < count && index < 64; index++)
                    ReadFont(stream, path, index, (int)ReadU32(stream, 12 + index * 4), entries);
            }
            else if (tag == SfntDirectory.TrueTypeVersion || tag == SfntDirectory.OpenTypeVersion || tag == SfntDirectory.AppleTrueTypeVersion)
            {
                ReadFont(stream, path, 0, 0, entries);
            }
        }

        private static void ReadFont(FileStream stream, string path, int index, int directory, List<Entry> entries)
        {
            int tables = ReadU16(stream, directory + 4);
            int cmap = 0, cmapLength = 0, name = 0, nameLength = 0, os2 = 0, os2Length = 0;
            byte[] records = ReadBytes(stream, directory + 12, tables * 16);
            for (int table = 0; table < tables; table++)
            {
                int record = table * 16;
                string tableTag = Encoding.ASCII.GetString(records, record, 4);
                int offset = (int)BigEndian32(records, record + 8);
                int length = (int)BigEndian32(records, record + 12);
                switch (tableTag)
                {
                    case "cmap": cmap = offset; cmapLength = length; break;
                    case "name": name = offset; nameLength = length; break;
                    case "OS/2": os2 = offset; os2Length = length; break;
                }
            }

            if (cmap == 0)
                return;

            var map = CharacterMap.Read(ReadBytes(stream, cmap, cmapLength), 0);
            if (map == null)
                return; // symbol-encoded fonts (Wingdings and the like) are never fallbacks

            int weight = 400;
            bool italic = false;
            if (os2 != 0 && os2Length >= 64)
            {
                byte[] style = ReadBytes(stream, os2, 64);
                weight = (style[4] << 8) | style[5];
                italic = (((style[62] << 8) | style[63]) & 1) != 0;
            }

            entries.Add(new Entry
            {
                Path = path,
                Index = index,
                Family = name != 0 ? FamilyName(ReadBytes(stream, name, nameLength)) ?? Path.GetFileNameWithoutExtension(path) : Path.GetFileNameWithoutExtension(path),
                Map = map,
                Weight = weight,
                Regular = !italic
            });
        }

        // Windows-platform family name: typographic (id 16) before legacy (id 1), US
        // English before other languages (localized names such as Japanese ones).
        private static string FamilyName(byte[] table)
        {
            int count = (table[2] << 8) | table[3];
            int storage = (table[4] << 8) | table[5];
            string best = null;
            int bestScore = -1;
            for (int record = 0; record < count; record++)
            {
                int position = 6 + record * 12;
                int platform = (table[position] << 8) | table[position + 1];
                int language = (table[position + 4] << 8) | table[position + 5];
                int id = (table[position + 6] << 8) | table[position + 7];
                if (platform != 3 || (id != 1 && id != 16))
                    continue;

                int score = (id == 16 ? 2 : 0) + (language == 0x0409 ? 1 : 0);
                if (score <= bestScore)
                    continue;

                int length = (table[position + 8] << 8) | table[position + 9];
                int offset = (table[position + 10] << 8) | table[position + 11];
                if (storage + offset + length > table.Length)
                    continue;
                best = Encoding.BigEndianUnicode.GetString(table, storage + offset, length);
                bestScore = score;
            }
            return best;
        }

        private static byte[] ReadBytes(FileStream stream, int offset, int length)
        {
            var buffer = new byte[length];
            stream.Seek(offset, SeekOrigin.Begin);
            int read = 0;
            while (read < length)
            {
                int count = stream.Read(buffer, read, length - read);
                if (count <= 0)
                    throw new EndOfStreamException();
                read += count;
            }
            return buffer;
        }

        private static uint ReadU32(FileStream stream, int offset) => BigEndian32(ReadBytes(stream, offset, 4), 0);

        private static int ReadU16(FileStream stream, int offset)
        {
            var bytes = ReadBytes(stream, offset, 2);
            return (bytes[0] << 8) | bytes[1];
        }

        private static uint BigEndian32(byte[] data, int offset)
            => (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }
}
