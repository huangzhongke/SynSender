using Jst.SynchubClient.Cosco;
using Jst.SynchubClient.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jst.SynchubClient.Kmtc.work;
using Jst.SynchubClient.One;

namespace Jst.SynchubClient
{
    public class Channel
    {

        
        /// <summary>
        /// 开始心跳检测
        /// </summary>
        public static void StartHeartBeat()
        {

            SyncClientInfo clientInfo = new SyncClientInfo()
            {
                Code = Config.Configuration["Client:Code"],
                Description = Config.Configuration["Client:Description"],
                Name = Config.Configuration["Client:Name"],
            };
            string content = JObject.FromObject(clientInfo).ToString();

            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 加载服务器配置文件
                        // Config.LoadServerConfig();

                        await Redis.Db.StringSetAsync(RedisKeys.HeartBeat(), content, TimeSpan.FromSeconds(65));
                        Console.WriteLine("[心跳检测]" + DateTime.Now + " 刷新服务器配置并进行心跳  版本:1022 ");

                    }
                    catch(Exception ex)
                    {
                        await LocalLogger.Main.LogText("心跳检测", ex.ToString());
                    }
                    finally
                    {
                        await Task.Delay(60 * 1000);
                    }
                }

            });

        }

        public static void ListenChannel()
        {
            string channelCode = RedisKeys.ListenChannelCode();
            // string channelCode = "one_channel";
            Console.WriteLine("[Redis] 开始监听Redis " + channelCode);
            // 先同步一次任务
            //TODO 这里要同步成对应ClinetCode的任务
            // CoscoTaskManager.Instance.SyncTasks();
            // OneTaskManager.Instance.SyncTasks();
            KmtcTaskManager.Instance.SyncTasks();
            Redis.Connection.GetSubscriber().Subscribe(channelCode, (channel, message) =>
            {

                Console.WriteLine("[Channel] " + message);
                
                var msg = JObject.Parse(message).ToObject<ChannelMessage>();
                
                // 同步任务
                if(msg.Type == "sync-task")
                {
                    Console.WriteLine("开始同步任务");
                    //TODO 这里等会儿要改成one
                    // OneTaskManager.Instance.SyncTasks();
                    // CoscoTaskManager.Instance.SyncTasks();
                    KmtcTaskManager.Instance.SyncTasks();
                }

            });
            
        }
    }
}
