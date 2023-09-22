using System;

namespace Jst.SynchubClient.entity.kmtc
{
    public class KmtcScheduleVo
    {
        /// <summary>
        /// 起运港
        /// </summary>
        public KmtcPort Departure { get; set;}
        /// <summary>
        /// 目的港
        /// </summary>
        public KmtcPort Arrival { get; set;}
        /// <summary>
        /// 船期
        /// </summary>
        public DateTime? ShipDate { get; set;}
    }
}