using Jst.SynchubClient.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient
{
    public class Logger
    {



        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        public static async Task Log(LogInfo info)
        {
            info.Code = Config.ClientCode;
            info.Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
            string content = JObject.FromObject(info).ToString();
            
            await Redis.Db.ListLeftPushAsync($"log:{info.TaskId}", content);
        }

        /// <summary>
        /// 记录日志
        /// </summary>
        /// <param name="type"></param>
        /// <param name="content"></param>
        /// <param name="taskId"></param>
        /// <returns></returns>
        public static Task LogText(string type,string content,string taskId)
        {
            return Log(new LogInfo() { 
                Content = content,
                Type = type,
                TaskId = taskId
            });

        }

    }
}
