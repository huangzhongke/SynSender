using System.Collections.Generic;

namespace SynchubServer.entity
{
    public class SyncOneClientInfo
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 代码
        /// </summary>
        public string Code { get; set; }
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 任务
        /// </summary>
        public List<SearchInfo> Tasks { get; set; }
    }
}