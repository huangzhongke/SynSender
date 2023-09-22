using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient
{
    public class LocalLogger
    {
        public static readonly LocalLogger Main = new LocalLogger();
        private LocalLogger()
        {
            string LogFilePath(string LogEvent) => $@"Logs\log.log";
            string SerilogOutputTemplate = "Date:{Timestamp:yyyy-MM-dd HH:mm:ss.fff}{NewLine}{Message}{NewLine}" + new string('-', 50) + "{NewLine}";
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()//记录相关上下文信息 
                .WriteTo.Console(outputTemplate: SerilogOutputTemplate)//输出到控制台
                .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Debug).WriteTo.File(LogFilePath("Debug"), rollingInterval: RollingInterval.Day, outputTemplate: SerilogOutputTemplate))
                .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Information).WriteTo.File(LogFilePath("Information"), rollingInterval: RollingInterval.Day, outputTemplate: SerilogOutputTemplate))
                .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Warning).WriteTo.File(LogFilePath("Warning"), rollingInterval: RollingInterval.Day, outputTemplate: SerilogOutputTemplate))
                .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Error).WriteTo.File(LogFilePath("Error"), rollingInterval: RollingInterval.Day, outputTemplate: SerilogOutputTemplate))
                .WriteTo.Logger(lg => lg.Filter.ByIncludingOnly(p => p.Level == LogEventLevel.Fatal).WriteTo.File(LogFilePath("Fatal"), rollingInterval: RollingInterval.Day, outputTemplate: SerilogOutputTemplate))
                .CreateLogger();
        }


        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="group"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public Task LogText(string group, string message)
        {
            Log.Information($"[{group}] {message}");
            return Task.CompletedTask;
        }
    }
}
