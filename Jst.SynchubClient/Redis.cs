using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient
{
    public class Redis
    {

        public static ConnectionMultiplexer Connection { get; private set; }

        public static IDatabase Db { get; private set; }
        

        
        
        /// <summary>
        /// 初始化Redis
        /// </summary>
        public static void Init()
        {
            string conn = Config.Configuration["Redis:ConnectionString"];
            Connection = ConnectionMultiplexer.Connect(conn);

            int dbIdx = int.Parse(Config.Configuration["Redis:DatabaseIndex"]);

            Db = Connection.GetDatabase(dbIdx);
        }


    }
}
