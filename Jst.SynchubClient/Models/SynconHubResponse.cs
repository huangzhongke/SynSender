using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Models
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
