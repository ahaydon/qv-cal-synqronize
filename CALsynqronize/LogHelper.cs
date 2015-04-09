using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace CALsynqronize
{
    class LogProperties
    {
        public string TaskNameOrId { get; set; }
        public string ExecId { get; set; }
    }

    class LogHelper
    {
        public static void Log(LogLevel level, string message, LogProperties logProperties)
        {
            LogEventInfo myEvent = new LogEventInfo(level, "", message);
            myEvent.LoggerName = logger.Name;
            logger.Log(myEvent);
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();
    }
}
