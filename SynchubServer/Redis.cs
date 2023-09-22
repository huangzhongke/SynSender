using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SynchubServer
{
    public class Redis
    {

        public static ConnectionMultiplexer Connection { get; private set; }

        public static IDatabase Db { get; private set; }



        /// <summary>
        /// 初始化Redis
        /// </summary>
        public static void Init(string conn,int dbIdx)
        {
            int timeout = 100000;
            Connection = ConnectionMultiplexer.Connect(conn, (config) =>
            {
                config.AsyncTimeout = timeout;
                config.ConnectTimeout = timeout;
                config.ResponseTimeout = timeout;
                config.SyncTimeout = timeout;
            });
            //Connection.TimeoutMilliseconds
            Db = Connection.GetDatabase(dbIdx);
        }


    }
}
