using Microsoft.Extensions.Configuration;

namespace SynchubServer
{
    public class ServerConfig
    {
        public static ServerConfig Instance { get; private set; } = new ServerConfig();
        /// <summary>
        /// 
        /// </summary>
        public string APIHost { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string APIKey { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string APISecret { get; set; }

        public void Init(IConfiguration configuration)
        {
            APIHost = configuration["Cosco:APIHost"];
            APIKey = configuration["Cosco:APIKey"];
            APISecret = configuration["Cosco:APISecret"];

            
        }

    }
}
