using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lazpoly.Infra.Logging
{
    /// <summary>
    /// Appends errors to a text file so a player can send it when something goes wrong. Pure file
    /// handling, no engine: the service feeds it. When the file grows past the limit, the old one
    /// is kept once as <c>.1</c> and a fresh file starts, so the log never fills the disk.
    /// </summary>
    public sealed class ErrorLog
    {
        public const string FileName = "lazpoly-hata.log";
        public const long MaxBytes = 512 * 1024;

        private readonly string _path;
        private readonly long _maxBytes;
        private readonly object _gate = new object();

        public ErrorLog(string path, long maxBytes = MaxBytes)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A log needs a path.", nameof(path));
            if (maxBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            _path = path;
            _maxBytes = maxBytes;
        }

        public string Path => _path;

        /// <summary>The file that holds the previous log after a rotation.</summary>
        public string RotatedPath => _path + ".1";

        /// <summary>Writes one header line when a run starts: version and platform, so a log can be matched to a build.</summary>
        public void BeginRun(DateTime time, string version, string platform)
        {
            Write($"{Stamp(time)} RUN  Lazpoly {version} ({platform})" + Environment.NewLine);
        }

        /// <summary>Appends an error with its stack trace; blank lines in the trace are dropped.</summary>
        public void Append(DateTime time, string kind, string message, string stackTrace)
        {
            Write(Format(time, kind, message, stackTrace));
        }

        /// <summary>Appends a report the player asked for, or the game's own notes on a problem: a title and indented lines.</summary>
        public void AppendReport(DateTime time, string title, IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (string line in lines ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(line)) sb.Append(line.TrimEnd()).Append('\n');
            }

            Write(Format(time, "REPORT", title, sb.ToString()));
        }

        /// <summary>One entry: a stamped line, then the trace indented, then a blank line.</summary>
        public static string Format(DateTime time, string kind, string message, string stackTrace)
        {
            var sb = new StringBuilder();
            sb.Append(Stamp(time)).Append(' ').Append((kind ?? "?").ToUpperInvariant().PadRight(4)).Append(' ').Append(Single(message)).Append(Environment.NewLine);
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                foreach (string line in stackTrace.Split('\n'))
                {
                    string trimmed = line.TrimEnd('\r').Trim();
                    if (trimmed.Length > 0) sb.Append("    ").Append(trimmed).Append(Environment.NewLine);
                }
            }

            sb.Append(Environment.NewLine);
            return sb.ToString();
        }

        private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd HH:mm:ss");

        private static string Single(string text) => (text ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();

        private void Write(string text)
        {
            lock (_gate)
            {
                try
                {
                    string directory = System.IO.Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    var file = new FileInfo(_path);
                    if (file.Exists && file.Length + text.Length > _maxBytes) Rotate();
                    File.AppendAllText(_path, text, Encoding.UTF8);
                }
                catch (IOException)
                {
                    // A log that cannot be written must never take the game down with it.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private void Rotate()
        {
            if (File.Exists(RotatedPath)) File.Delete(RotatedPath);
            File.Move(_path, RotatedPath);
        }
    }
}
