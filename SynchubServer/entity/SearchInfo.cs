using System;
using System.Collections.Generic;

namespace SynchubServer.entity
{
    public class SearchInfo
    {
        public string Id { get; set; }
        
        public Port originPort { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Port destinationPort { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Equipment equipmentType { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int quantity { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string vesselName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string voyage { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public List <string > references { get; set; }
        /// <summary>
        /// DateTime? 问好表示该对象可以为空
        /// </summary>
        public DateTime afterDate { get; set; }
        /// <summary>
        /// 查询间隔时间 
        /// </summary>
        public int searchSleepTime { get; set; }
        
        /// <summary>
        /// 状态  0-> 暂停   1-> 运行中   2-> 完成     
        /// </summary>
        public short Status { get; set; }
        /// <summary>
        /// 开始运行时间    
        /// </summary>
        public DateTime? startRunDate { get; set; }
        /**
         * 结束运行时间
         */
        public DateTime? endRunDate { get; set; }
    }
}