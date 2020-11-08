using Disqord.Logging;
using Microsoft.Extensions.Logging;
using System;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace VerificationBot
{
    public class VLogger : Disqord.Logging.ILogger
    {
        private readonly ILogger _logger;

        public VLogger(ILogger logger)
            => _logger = logger;

        public void Log(object sender, LogEventArgs e)
        {
            _logger.Log((LogLevel)(int)e.Severity, e.Exception, "[{source}] : {message}", e.Source, e.Message);

            Logged?.Invoke(sender, e);
        }

        public event EventHandler<LogEventArgs> Logged;
        public void Dispose() { }
    }
}
