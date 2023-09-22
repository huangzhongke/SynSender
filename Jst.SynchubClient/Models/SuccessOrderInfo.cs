using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Models
{
    public class SuccessOrderInfo
    {
        /// <summary>
        /// client code
        /// </summary>
        public string ClientCode { get; set; }
        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }
        /// <summary>
        /// 任务Id
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 抢到的时间
        /// </summary>
        public DateTime? Time { get; set; }


        
        
    }
}
