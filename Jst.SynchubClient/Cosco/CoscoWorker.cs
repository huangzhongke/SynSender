using Jst.SynchubClient.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Cosco
{
    public class CoscoWorker
    {
        public synconhub_info Info { get; private set; }
        private CancellationTokenSource _cancelToken;
        public CoscoWorker(synconhub_info info, CancellationTokenSource cancellationToken)
        {
            _cancelToken = cancellationToken;
            Info = info;
        }





        /// <summary>
        /// 订舱API url
        /// </summary>
        string bookingApiUrl = "";
        /// <summary>
        /// 获得列表API url
        /// </summary>
        string searchApiUrl = "";
        /// <summary>
        /// 详情API Url
        /// </summary>
        string detailApiUrl = "";
        /// <summary>
        /// 版本号
        /// </summary>
        string version = "";

        public void Init()
        {
            version = Info.ApiVersion;
            if (version == "V1")
            {
                bookingApiUrl = "/service/synconhub/shipment/booking";
                searchApiUrl = "/service/synconhub/product/instantBooking/search";
                detailApiUrl = "/service/synconhub/common/intermodalService";
            }
            else
            {
                bookingApiUrl = "/service/synconhub/shipment/general/booking";
                searchApiUrl = "/service/synconhub/product/general/search";
                detailApiUrl = "/service/synconhub/common/intermodalService/general";
            }

            generateQueryBody();
            TickTimer.Instance.Event += Handle;
        }


        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 是否触发过
        /// </summary>
        public bool Trigger { get; set; }        


        /// <summary>
        /// time handle
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        public void Handle(DateTime time)
        {
            if (Trigger || Success || _cancelToken.IsCancellationRequested)
            {
                return;
            }

            if (time >= Info.StartRunDate && time < Info.EndRunDate)
            {
                if (Trigger || Success || _cancelToken.IsCancellationRequested)
                {
                    return;
                }
                Trigger = true;
                _ = run();
                
            }
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <returns></returns>
        private async Task run()
        {
            while (_cancelToken.IsCancellationRequested == false && string.IsNullOrEmpty(targetProductId))
            {

                try
                {
                    // 请求查询
                    _ = execute();

                }
                catch (Exception ex)
                {
                    _ = Logger.LogText("任务", ex.ToString(),Info.Id);
                  
                }
                // 休眠一会再继续尝试发请求
                await Task.Delay(Info.SearchSleepTime);
            }
        }





        // 请求body
        RequestTask requestTask = null;
        string targetProductId = null;
        /// <summary>
        /// 是否已经发送了
        /// </summary>
        bool sent = false;
        public async Task execute()
        {
            if (sent)
            {
                _ = Logger.LogText("任务" + Info.Id, "(查询前)已经发送过下单请求  停止发送",Info.Id);
                return;
            }
            _ = Logger.LogText("任务" + Info.Id, "开始请求查询 ", Info.Id);
            Stopwatch queryWatch = Stopwatch.StartNew();
            int? inventory = await queryTask();
            queryWatch.Stop();
            

            if (inventory.HasValue)
            {
                if (inventory.Value == -1)
                {

                    _ = Logger.LogText("任务" + Info.Id, "已查询到targetProductId不进行查询 进行下单 ", Info.Id);

                }
                else
                {
                    _ = Logger.LogText("任务" + Info.Id, $"(查询花费时间 {queryWatch.ElapsedMilliseconds})获得库存数量:" + inventory.Value + " ", Info.Id);
                }


                if (sent)
                {
                    _ = Logger.LogText("任务" + Info.Id, "(下单前)已经发送过下单请求  停止发送", Info.Id);
                    return;
                }
                sent = true;



                for(int i = 0; i < Info.RoundCount; i++)
                {
                    for(int j = 0; j < Info.RunCount; j++)
                    {
                        _ = bookingTask(Info.ContainerCount);
                    }
                    
                    if(Info.RoundSleepTime > 0)
                    {
                        await Task.Delay(Info.RoundSleepTime);
                    }
                }



            }
            else
            {
                _ = Logger.LogText("任务" + Info.Id, $"(查询花费时间 {queryWatch.ElapsedMilliseconds}) 未获得库存数量", Info.Id);
            }


        }


        /// <summary>
        /// 查询请求
        /// </summary>
        /// <returns></returns>
        private async Task<int?> queryTask()
        {
            if (string.IsNullOrEmpty(targetProductId) == false)
            {
                return -1;
            }


            var response = await CoscoRequester.Instance.Request(Info.Id,requestTask.UrlPath, requestTask.Body, requestTask.Method, _cancelToken);
            if (response.data != null)
            {
                if (response.data.TryGetValue("content", out JToken content))
                {
                    var arr = content.ToArray();
                    if (arr.Length == 0)
                    {
                        // 返回结果为空 等待下一次查询
                        return null;
                    }
                    foreach (var item in arr)
                    {
                        if (string.IsNullOrEmpty(Info.serviceCode) || (!string.IsNullOrEmpty(Info.serviceCode) && item["serviceCode"].ToString().ToUpper() == Info.serviceCode.ToUpper()))
                        {
                            if (int.TryParse(item["inventory"].ToString(), out int inventory))
                            {
                                if (inventory > 0)
                                {
                                    targetProductId = item["id"].ToString();
                                    return inventory;
                                }

                            }
                        }

                    }
                }
                else
                {
                    var item = response.data;
                    if (int.TryParse(item["inventory"].ToString(), out int inventory))
                    {
                        if (inventory > 0)
                        {
                            targetProductId = item["id"].ToString();
                        }
                        return inventory;
                    }
                }

            }
            return null;
        }
        /// <summary>
        /// 订舱请求
        /// </summary>
        /// <param name="task"></param>
        /// <param name="bookingCount"></param>
        /// <returns></returns>
        private async Task bookingTask(int bookingCount)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetProductId))
                {

                    _ = Logger.LogText("任务" + Info.Id, "创建订单失败 产品Id为空", Info.Id);
                    return;
                }
                RequestTask req = new RequestTask()
                {
                    InfoId = Info.Id,
                    Body = generateBookingBody(bookingCount),
                    Method = "POST",
                    UrlPath = bookingApiUrl
                };



                Stopwatch stopwatch = Stopwatch.StartNew();
                var response = await CoscoRequester.Instance.Request(Info.Id,req.UrlPath, req.Body, req.Method, _cancelToken);
                stopwatch.Stop();

                if (response.data == null)
                {
                    _ = Logger.LogText("任务" + Info.Id, $"时间耗时:{stopwatch.ElapsedMilliseconds}毫秒  创建订单失败 返回数据为空", Info.Id);
                    return;
                }
                string msg = "";
                if (string.IsNullOrWhiteSpace(response.message))
                {
                    msg = "空";
                }
                else
                {
                    msg = response.message;
                }

                _ = Logger.LogText("任务" + Info.Id, " 创建订单反馈:" + msg, Info.Id);

                if (response.data["orderNo"] != null || response.data["brNo"] != null)
                {

                    _ = Logger.LogText("任务" + Info.Id, $"时间耗时:{stopwatch.ElapsedMilliseconds}毫秒  创建订单成功!", Info.Id);
                    // 更新数据库
                    Success = true;


                    Dispose();
                    await Redis.Db.ListLeftPushAsync(RedisKeys.SuccessList(),JObject.FromObject(new SuccessOrderInfo()
                    {
                        ClientCode = Config.ClientCode,
                        OrderNo = response.data["orderNo"].ToString(),
                        TaskId = Info.Id,
                        Time = DateTime.Now
                    }).ToString());
                    // 修改状态

                    Info.Status = 2;
                    await Redis.Db.StringSetAsync(RedisKeys.TaskInfo(Info.Id,"cosco"), JObject.FromObject(Info).ToString());

                    

                    return;
                }
                
                _ = Logger.LogText("任务" + Info.Id, $"时间耗时:{stopwatch.ElapsedMilliseconds}毫秒 创建订单失败" , Info.Id);
                
            }
            catch (Exception ex)
            {
                _ = Logger.LogText("任务" + Info.Id, ex.ToString(), Info.Id);
            }

        }




        private string generateBookingBody(int containerCount)
        {
            if (version == "V1")
            {
                var reqContent = new
                {
                    productId = targetProductId,
                    containerInfos = new object[]
                    {
                         new
                         {
                             containerType = Info.ContainerType,
                             quantity = containerCount
                         }
                    },
                    blQuantity = Info.blQuantity,
                    includeInsurance = Info.includeInsurance == 1 ? true : false,
                    emergencyContactInfo = new
                    {
                        name = Info.emergencyContactInfoName,
                        email = Info.emergencyContactInfoEmail,
                        mobile = Info.emergencyContactInfoMobile,
                        phone = Info.emergencyContactInfoPhone,
                        address = Info.emergencyContactInfoAddress
                    },
                    cargoInfo = new
                    {
                        desc = Info.cargoInfoDesc,
                        packageType = Info.cargoInfoPackageType,
                        quantity = Info.cargoInfoQuantity,
                        weight = Info.cargoInfoWeight,
                        volume = Info.cargoInfoVolume,
                        remarks = Info.cargoInfoRemarks
                    }
                };
                string reqString = JObject.FromObject(reqContent).ToString();
                return reqString;
            }
            else
            {
                var reqContent = new
                {
                    productId = targetProductId,
                    preferPaymentTerms = "P",
                    containerInfos = new object[]
                    {
                         new
                         {
                             containerType = Info.ContainerType,
                             quantity = containerCount
                         }
                    },
                    blQuantity = Info.blQuantity,
                    includeInsurance = Info.includeInsurance == 1 ? true : false,
                    emergencyContactInfo = new
                    {
                        name = Info.emergencyContactInfoName,
                        email = Info.emergencyContactInfoEmail,
                        mobile = Info.emergencyContactInfoMobile,
                        phone = Info.emergencyContactInfoPhone,
                        address = Info.emergencyContactInfoAddress
                    },
                    cargoInfo = new
                    {
                        desc = Info.cargoInfoDesc,
                        packageType = Info.cargoInfoPackageType,
                        quantity = Info.cargoInfoQuantity,
                        weight = Info.cargoInfoWeight,
                        volume = Info.cargoInfoVolume,
                        remarks = Info.cargoInfoRemarks
                    }
                };
                string reqString = JObject.FromObject(reqContent).ToString();
                return reqString;
            }

        }
        private void generateQueryBody()
        {

            var t = new RequestTask()
            {
                InfoId = Info.Id,
            };

            if (string.IsNullOrWhiteSpace(Info.productId))
            {
                // /service/synconhub/product/instantBooking/search
                // 不存在产品ID  查询列表
                t.Body = JObject.FromObject(new
                {
                    startDate = $"{Info.startDate.Value.ToString("yyyy-MM-dd")}T00:00:00.000Z",
                    endDate = Info.endDate.HasValue ? $"{Info.endDate.Value.ToString("yyyy-MM-dd")}T00:00:00.000Z" : "",
                    porCityId = Info.porCityId,
                    fndCityId = Info.fndCityId,
                    page = 1,
                    size = 50
                }).ToString();
                t.Method = "POST";
                t.UrlPath = searchApiUrl;
            }
            else
            {
                // /service/synconhub/product/instantBooking/{productId}
                // 存在产品ID 查询 Detail
                t.Method = "GET";
                t.UrlPath = $"{detailApiUrl}/{Info.productId}";
                t.Body = "";
                targetProductId = Info.productId;
            }

            requestTask = t;
        }




        /// <summary>
        /// 释放
        /// </summary>
        public void Dispose()
        {
            Console.WriteLine("[销毁]销毁Worker " + Info.Id);
            _cancelToken.Cancel();
            TickTimer.Instance.Event -= Handle;
        }










    }

}
