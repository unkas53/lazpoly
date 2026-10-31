using System;
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

        public ErrorLogService() : this(new ErrorLog(Path.Combine(Application.persistentDataPath, ErrorLog.FileName)))
        {
        }

        public ErrorLogService(ErrorLog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Where the log is written; the settings window offers to open its folder.</summary>
        public string FilePath => _log.Path;

        public string Folder => Path.GetDirectoryName(_log.Path);

        public void Initialize()
        {
            _log.BeginRun(DateTime.Now, Application.version, $"{Application.platform}, Unity {Application.unityVersion}");
            Application.logMessageReceivedThreaded += OnLog;
        }

        public void Tick()
        {
        }

        public void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        /// <summary>Only the serious kinds are kept; warnings and plain logs would bury them.</summary>
        public static bool Keeps(LogType type) => type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (!Keeps(type)) return;
            _log.Append(DateTime.Now, type.ToString(), message, stackTrace);
        }
    }
}
