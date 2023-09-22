using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Models
{
    public class RequestTask
    {
        /// <summary>
        /// Info id
        /// </summary>
        public string InfoId { get; set; }
        /// <summary>
        /// 请求路径
        /// </summary>
        public string UrlPath { get; set; }
        /// <summary>
        /// body
        /// </summary>
        public string Body { get; set; }
        /// <summary>
        /// 请求方式 GET / POST
        /// </summary>
        public string Method { get; set; }
    }
}
