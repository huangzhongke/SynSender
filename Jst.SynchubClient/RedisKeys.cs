using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient
{
    public static class RedisKeys
    {
        /// <summary>
        /// 拉取当前client 的任务
        /// </summary>
        /// <returns></returns>
        public static string FetchTasks()
        {
            return "tasks:" + Config.ClientCode;
        }

        public static string TaskInfo(string id,string carrierCode)
        {
            return carrierCode + ":taskInfo:" + id;
        }

        public static string HeartBeat()
        {
            return "client:" + Config.ClientCode;
        }

        /// <summary>
        /// 监听的频道
        /// </summary>
        /// <returns></returns>
        public static string ListenChannelCode()
        {
            return "channel:" + Config.ClientCode;
        }

        /// <summary>
        /// 任务完成的列表
        /// </summary>
        /// <returns></returns>
        public static string SuccessList()
        {
            return "successList";
        }


    }
}
