using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Lazpoly.Infra.Logging
{
    /// <summary>
    /// Listens to Unity's log and writes errors, exceptions and failed assertions to
    /// <see cref="ErrorLog"/> in the persistent data folder. Registered first, so it also
    /// catches problems while the other services start.
    /// </summary>
    public sealed class ErrorLogService : IGameService
    {
        private readonly ErrorLog _log;
        private readonly string _details;

        /// <param name="details">Anything else worth matching a log to a build, such as the network protocol and save format versions.</param>
        public ErrorLogService(string details = null) : this(new ErrorLog(Path.Combine(Application.persistentDataPath, ErrorLog.FileName)), details)
        {
        }

        public ErrorLogService(ErrorLog log, string details = null)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _details = details;
        }

        /// <summary>Where the log is written; the settings window offers to open its folder.</summary>
        public string FilePath => _log.Path;

        public string Folder => Path.GetDirectoryName(_log.Path);

        public void Initialize()
        {
            string platform = $"{Application.platform}, Unity {Application.unityVersion}";
            _log.BeginRun(DateTime.Now, Application.version, string.IsNullOrEmpty(_details) ? platform : $"{platform}, {_details}");
            Application.logMessageReceivedThreaded += OnLog;
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        /// <summary>Writes what the player or the game wants a reader of the log to know, with the time.</summary>
        public void Report(string title, IEnumerable<string> lines) => _log.AppendReport(DateTime.Now, title, lines);

        /// <summary>Only the serious kinds are kept; warnings and plain logs would bury them.</summary>
        public static bool Keeps(LogType type) => type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (!Keeps(type)) return;
            _log.Append(DateTime.Now, type.ToString(), message, stackTrace);
        }
    }
}
