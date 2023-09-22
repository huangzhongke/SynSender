using Newtonsoft.Json.Linq;

namespace SynchubServer.Models
{
    public class SynconHubResponse
    {
        /// <summary>
        /// status code 一般0为正常
        /// </summary>
        public string code { get; set; }
        /// <summary>
        /// 信息
        /// </summary>
        public string message { get; set; }
        /// <summary>
        /// 详细数据
        /// </summary>
        public JObject data { get; set; }
    }
}
