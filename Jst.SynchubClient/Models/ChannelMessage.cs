using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Models
{
    /// <summary>
    /// channel 会推送的消息
    /// </summary>
    public class ChannelMessage
    {
        /// <summary>
        /// 类型
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public string Data { get; set; }
             
    }
}
