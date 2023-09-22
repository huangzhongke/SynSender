using System;

namespace Jst.SynchubClient
{
    internal class Program
    {
        static void Main(string[] args)
        {
            

            // 读取配置文件
            Config.LoadConifg();

            // 初始化 redis 连接
            Redis.Init();

            // 加载服务器配置文件
            // Config.LoadServerConfig();

            // 监听频道
            Channel.ListenChannel();
            
            // 开始心跳检测
            Channel.StartHeartBeat();



            // 保持程序不退出
            while (true)
            {
                string cmd = Console.ReadLine();
                if(cmd == "exit")
                {
                    break;
                }
            }
        }
    }
}
