using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient
{
    public static class Config
    {
        public static IConfiguration Configuration { get; private set; }

        /// <summary>
        /// 客户端唯一代码
        /// </summary>
        public static string ClientCode { get; set; }

        /// <summary>
        /// API Host
        /// </summary>
        public static string APIHost { get; set; }
        /// <summary>
        /// API KEY
        /// </summary>
        public static string APIKey { get; set; }
        /// <summary>
        /// API SECRET
        /// </summary>
        public static string APISecret { get; set; }


        /// <summary>
        /// 加载服务器配置 API
        /// </summary>
        public static void LoadServerConfig()
        {
            string value = Redis.Db.StringGet("server_setting");
            var obj = JObject.Parse(value);
            APIHost = obj["APIHost"].ToString();
            APIKey = obj["APIKey"].ToString();
            APISecret = obj["APISecret"].ToString();
        
            Console.WriteLine("[服务器配置] APIHost:" + APIHost);
            Console.WriteLine("[服务器配置] APIKey:" + APIKey);
            Console.WriteLine("[服务器配置] APISecret:" + APISecret);
        }

        public static void LoadConifg()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("./config.json");

            var config = builder.Build();
            Configuration = config;

            Console.WriteLine("[配置] Redis连接字符串:" + Configuration["Redis:ConnectionString"]);
            Console.WriteLine("[配置] Redis数据库序号:" + Configuration["Redis:DatabaseIndex"]);
            Console.WriteLine("[配置] 名称:" + Configuration["Client:Name"]);
            Console.WriteLine("[配置] 代码:" + Configuration["Client:Code"]);
            Console.WriteLine("[配置] 描述:" + Configuration["Client:Description"]);
            Console.WriteLine("[邮件] HOST:" + Configuration["Email:smtpHost"]);
            Console.WriteLine("[邮件] PORT:" + Configuration["Email:smtpPort"]);
            Console.WriteLine("[邮件] SENDER:" + Configuration["Email:SenderEmail"]);
            Console.WriteLine("[邮件] PASSWORD:" +  Configuration["Email:Password"]);
            Console.WriteLine(DateTime.Now.ToString());

            ClientCode = Configuration["Client:Code"];
        }

    }
}
