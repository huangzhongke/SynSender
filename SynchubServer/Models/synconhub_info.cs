using System;
using System.Collections.Generic;

namespace SynchubServer.Models
{
    public class synconhub_info
    {
        /// <summary>
        /// Id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// API 版本 V1 / V2
        /// </summary>
        public string ApiVersion { get; set; }


        /// <summary>
        /// 价格必须低于多少才能拍
        /// </summary>
        public decimal? AmountLessThan { get; set; }
        
        /// <summary>
        /// 查询间隔时间
        /// </summary>
        public int SearchSleepTime { get; set; }

        /// <summary>
        /// 运行次数
        /// </summary>
        public int RunCount { get; set; }


        /// <summary>
        /// 下单轮数
        /// </summary>
        public int RoundCount { get; set; } = 1;
        /// <summary>
        /// 下单每轮间隔时间
        /// </summary>
        public int RoundSleepTime { get; set; } = 0;



        /// <summary>
        /// 状态  0-> 暂停   1-> 运行中   2-> 完成     
        /// </summary>
        public short Status { get; set; }
        /// <summary>
        /// 运行消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 开始运行时间
        /// </summary>
        public DateTime? StartRunDate { get; set; }
        /// <summary>
        /// 结束运行时间
        /// </summary>
        public DateTime? EndRunDate { get; set; }


        public string productId { get; set; }
        /// <summary>
        /// 付款方式（P：预付，C：到付）
        /// </summary>
        public string preferPaymentTerms { get; set; } = "P";


        /// <summary>
        /// 开航日期开始
        /// </summary>
        public DateTime? startDate { get; set; }
        /// <summary>
        /// 开航日期结束
        /// </summary>
        public DateTime? endDate { get; set; }
        /// <summary>
        /// 出发地ID
        /// </summary>
        public string porCityId { get; set; }
        /// <summary>
        /// 出发地名称
        /// </summary>
        public string porCityName { get; set; }
        /// <summary>
        /// 目的地ID
        /// </summary>
        public string fndCityId { get; set; }
        /// <summary>
        /// 目的地名称
        /// </summary>
        public string fndCityName { get; set; }
        /// <summary>
        /// 箱子类型
        /// </summary>
        public string ContainerType { get; set; }
        /// <summary>
        /// 要抢的箱子数量
        /// </summary>
        public int ContainerCount { get; set; }
        /// <summary>
        /// 航线代码  如果为空不进行匹配
        /// </summary>
        public string serviceCode { get; set; }


        /// <summary>
        /// 装港 联运服务
        /// </summary>
        public string loadingServiceNo { get; set; }
        /// <summary>
        /// 卸港 联运服务
        /// </summary>
        public string dischargeServiceNo { get; set; }

        /// <summary>
        /// 紧急联系名称
        /// </summary>
        public string emergencyContactInfoName { get; set; }
        /// <summary>
        /// 紧急联系人 邮箱
        /// </summary>
        public string emergencyContactInfoEmail { get; set; }
        /// <summary>
        /// 紧急联系人 手机
        /// </summary>
        public string emergencyContactInfoMobile { get; set; }
        /// <summary>
        ///  紧急联系人 座机
        /// </summary>
        public string emergencyContactInfoPhone { get; set; }
        /// <summary>
        /// 紧急联系人 地址信息  英文限定
        /// </summary>
        public string emergencyContactInfoAddress { get; set; }







        /// <summary>
        /// 货物信息 描述
        /// </summary>
        public string cargoInfoDesc { get; set; }
        /// <summary>
        /// 货物信息 包装类型
        /// </summary>
        public string cargoInfoPackageType { get; set; }
        /// <summary>
        /// 货物信息 数量
        /// </summary>
        public int cargoInfoQuantity { get; set; }
        /// <summary>
        /// 货物信息 重量
        /// </summary>
        public decimal cargoInfoWeight { get; set; }
        /// <summary>
        /// 货物信息 体积
        /// </summary>
        public decimal cargoInfoVolume { get; set; }
        /// <summary>
        /// 货物信息 备注
        /// </summary>
        public string cargoInfoRemarks { get; set; }



        /// <summary>
        /// 保价服务 0->不需要  1-> 需要
        /// </summary>
        public short includeInsurance { get; set; }
        /// <summary>
        /// 单证数量
        /// </summary>
        public int blQuantity { get; set; } = 1;


        public string GetInfoString()
        {
            return @$"
任务Id: {this.Id} 
出发地: {this.porCityName} {this.porCityId}
目的地: {this.fndCityName} {this.fndCityId}
指定产品: {this.productId ?? "未指定产品"}
API版本: {this.ApiVersion}
查询间隔: {this.SearchSleepTime}
运行时间: {this.StartRunDate.Value.ToString()} - {this.EndRunDate.Value.ToString()}
箱型箱量: {this.ContainerType} x {this.ContainerCount}
航线代码: {this.serviceCode ?? ""}
保价服务: {this.includeInsurance}
";
        }

    }

    public class synconhub_infoManual : synconhub_info
    {
        public List<ContainerInfo> containerInfos { get; set; }
        public List<specificPaymentTerm> specificPaymentTerms { get; set; }
    }
    public class specificPaymentTerm
    {
        public string chargeType { get; set; }
        public string chargeName { get; set; }
        public string paymentTerms { get; set; }
    }
    public class ContainerInfo
    {
        public string containerType { get; set; }
        public int quantity { get; set; }
        public string estimateWeight { get; set; }
    }
    public class synconhub_infoModel : synconhub_info
    {
        public List<string> Finished { get; set; }

    }
}
