using System;

namespace Jst.SynchubClient.entity.kmtc
{
    public class KmtcFormVo
    {

        public string Id;
        /// <summary>
        /// 
        /// </summary>
        public KmtcPort Departure { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public KmtcPort Arrival { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string VesselName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Voyage { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Equipment { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Packages { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string GrossWeight { get; set; }
        /// <summary>
        /// 
        /// </summary>
            public string Measurement { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ShipperName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ShipperAddr { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ConsigNeeName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string ConsigNeeAddr { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string NotifyName { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string NotifyAddr { get; set; }
        
        /// <summary>
        /// 状态  0-> 暂停   1-> 运行中   2-> 完成     
        /// </summary>
        public short Status { get; set; }
        /// <summary>
        /// 开始运行时间    
        /// </summary>
        public DateTime? StartRunDate { get; set; }
        /**
         * 结束运行时间
         */
        public DateTime? EndRunDate { get; set; }
        /// <summary>
        ///  船期
        /// </summary>
        public DateTime? ShipDate { get; set; }
        /// <summary>
        /// 下单状态是否成功
        /// 0 未完成 1 完成
        /// </summary>
        public short BookingStatus { get; set;}
        /// <summary>
        /// 箱量
        /// </summary>
        public int Quantity { get; set;}

    }
}