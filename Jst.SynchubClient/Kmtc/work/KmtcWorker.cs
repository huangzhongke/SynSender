using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reflection.Metadata;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jst.SynchubClient.Cosco;
using Jst.SynchubClient.Kmtc.entity;
using Jst.SynchubClient.Kmtc.utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SynchubServer.entity;
using SynchubServer.Models;

namespace Jst.SynchubClient.Kmtc.work
{
    public class KmtcWorker
    {
        public KmtcFormVo Info { get; private set; }
        private CancellationTokenSource _cancelToken;
        private Dictionary<string, object> postBooking = new Dictionary<string, object>();
        private Dictionary<string, object> scheduleData = new Dictionary<string, object>();
        public KmtcWorker(KmtcFormVo info, CancellationTokenSource cancellationToken)
        {
            _cancelToken = cancellationToken;
            Info = info;
            PreparePostBooking();
            PrepareScheduleData();
        }

        public void Init()
        {
            
            
            
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

        private async Task run()
        {
            try
            {
                // 请求查询
                _ = execute();
            }
            catch (Exception ex)
            {
                _ = Logger.LogText("任务", ex.ToString(), Info.Id);
            }

            // 休眠一会再继续尝试发请求
            // await Task.Delay(Info.searchSleepTime);
        }

        public string jwt;
        public async Task execute()
        {
            // jwt = Redis.Db.StringGet("kmtc_spider_token");
            //登录获取身份码
            // Logger.LogText("准备执行任务", $@"当前时间：{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} 开始执行任务", Info.Id);
            jwt = Redis.Db.StringGet("kmtc_spider_token");
            while (Info.BookingStatus == 0)
            {
                var result = await SearchSchedule();
                var postBooking = await HandlePostBookingData(result);
                try
                {
                    if (postBooking == null)    
                    {
                        // 休眠一会再继续尝试发请求
                        await Task.Delay(300);
                        continue;
                    }
                    
                    // await ValidationBooking(postBooking);
                    var postResult = await PostBookingData(postBooking);
                    if (!postResult.ContainsKey("srNo") && !postBooking.ContainsKey("bkgNo"))
                    {
                        Logger.LogText("未发现匹配航名航次的线路", $@"{JsonConvert.SerializeObject(Info)}", Info.Id);
                        return;
                    }
                    
                    string srNo = postResult["srNo"].ToString();
                    string bkgNo = postResult["bkgNo"].ToString();
                    await SendEmail(srNo, bkgNo);
                    //下单成功
                    Info.BookingStatus = 1;
                    Info.Status = 2;
                    await Redis.Db.StringSetAsync(RedisKeys.TaskInfo(Info.Id,"kmtc"), JObject.FromObject(Info).ToString());
                   
                    //TODO 需要修改任务队列中的状态值
                    string content = Redis.Db.StringGet(RedisKeys.FetchTasks());
                    List<KmtcFormVo> vos = JsonConvert.DeserializeObject<List<KmtcFormVo>>(content);
                    vos = vos.Where(data => data.Id == Info.Id).ToList();
                    foreach (var formVo in vos)
                    {
                        if (formVo.Id == Info.Id)
                        {
                            formVo.Status = 2;
                            formVo.BookingStatus = 1;
                            break;
                        }
                    }
            
                    await Redis.Db.StringSetAsync(RedisKeys.FetchTasks(), JsonConvert.SerializeObject(vos));
                    //销毁程序
                    Dispose();
            
                }
                catch (Exception e)
                {
                    Logger.LogText("任务发生异常", $@"{e.Message}", Info.Id);
                    Console.WriteLine($@"任务发生异常 {e.Message}");
                }
            
            }
            
        }

        public async Task GetAccessToken()
        {
            var cookies = Redis.Db.StringGet("kmtc_spider_cookie");
            List<CookieModel> cookieModels = JsonConvert.DeserializeObject<List<CookieModel>>(cookies);
            foreach (var cookieModel in cookieModels)
            {
                if (cookieModel.name.Equals("access_token"))
                {
                    jwt = cookieModel.value;
                    break;
                }
            }

        }

        public async Task SendEmail(string srNo,string bkgNo)
        {   
            try
            {
                using (SmtpClient smtpClient = new SmtpClient(Config.Configuration["Email:SmtpHost"],
                    int.Parse(Config.Configuration["Email:SmtpPort"])))
                {
                    smtpClient.Credentials = new NetworkCredential(Config.Configuration["Email:SenderEmail"],
                        Config.Configuration["Email:Password"]);
                    smtpClient.EnableSsl = true;
                    MailMessage mailMessage =
                        new MailMessage(Config.Configuration["Email:SenderEmail"],
                            Config.Configuration["Email:ToEmail"])
                        {
                            Subject = "【" + Info.Departure.plcEnmOnly + "-" + Info.Arrival.plcEnmOnly +
                                      "】，柜型： " +
                                      Info.Equipment + ", 航名：" + Info.VesselName + "航次" +
                                      Info.Voyage + "下单成功！", // 邮件主题
                            Body = $@"提单号:{srNo},订单号:{bkgNo}", // 邮件正文
                        };
                    smtpClient.Send(mailMessage);

                    Console.WriteLine("邮件发送成功！");
                }
            }
            catch (Exception e)
            {
                Logger.LogText("🤔发送邮件时出现错误", $@"{e.Message}", Info.Id);
                Console.WriteLine($"发送邮件时出现错误：{e.Message}");
                throw;
            }
        }
        public async Task<Dictionary<string, object>> PostBookingData(Dictionary<string, object> postBooking)
        {
            Stopwatch stopwatch = new Stopwatch();

            // 启动 Stopwatch
            stopwatch.Start();
            string url = "https://api.ekmtc.com/trans/trans/sr";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            Dictionary<string, string> Profile = new Dictionary<string, string>();
            Profile["cstNo"] = "NHJC01";
            Profile["picNo"] = "00005";
            headers["Jwt"] = jwt;
            headers["Selected-Profile"] = JsonConvert.SerializeObject(Profile);
            headers["Service-Path"] = "#/booking-new";
            var result = await EasyHttpUtil.Instance.PostAsync(url, headers, postBooking);
            
            if (result == null || Convert.ToInt32(result["code"].ToString()) != 200)
            {
                Logger.LogText("kmtcPostBookingData", JsonConvert.SerializeObject(result), Info.Id);
                throw new Exception("下单失败");    
                
            }
        
            stopwatch.Stop();
            // 获取经过的时间
            TimeSpan elapsedTime = stopwatch.Elapsed;
            Logger.LogText("提交订单花费时间", $"{elapsedTime.TotalMilliseconds} 毫秒", Info.Id);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
        }
        

        public async Task login()
        {
            Stopwatch stopwatch = new Stopwatch();
            // 启动 Stopwatch
            stopwatch.Start();
            string url = "https://api.ekmtc.com/auth/login";
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["userId"] = "ZHANGHONG";
            parameters["userPwd"] = "87873437";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Referer"] = "https://www.ekmtc.com/";
            headers["Host"] = "api.ekmtc.com";
            var result = await EasyHttpUtil.Instance.PostAsync(url, headers, parameters);
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
            jwt = data["jwt"].ToString();
            stopwatch.Stop();

            // 获取经过的时间
            TimeSpan elapsedTime = stopwatch.Elapsed;

            // 输出执行时间
            Logger.LogText("login花费时间", $"{elapsedTime.TotalMilliseconds} 毫秒", Info.Id);
        }

        public async Task ValidationBooking(Dictionary<string, object> postBooking)
        {
            string url = "https://api.ekmtc.com/trans/trans/bookings/check/validation";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Jwt"] = jwt;

           var result = await EasyHttpUtil.Instance.PostAsync(url, headers, postBooking);
           if (result == null || Convert.ToInt32(result["code"].ToString()) != 200)

           {
               Logger.LogText("kmtcValidationBooking", JsonConvert.SerializeObject(result), Info.Id);
               throw new Exception("验证失败");
                
           }
        }

        public async Task<Dictionary<string, object>> HandlePostBookingData(Dictionary<string, object> scheduleResult)
        {
            // var postBooking = new Dictionary<string, object>();
            var vesselAndVoyage = Info.VesselName.Split("-");
            var vessel = vesselAndVoyage[0];
            var voyage = vesselAndVoyage[1];
            var listSchedule = JsonConvert.DeserializeObject<List<Dictionary<string, Object>>>(scheduleResult["listSchedule"].ToString());
            var list = listSchedule.Where(data => { return vessel.Equals(data["vslNm"]) && voyage.Equals(data["voyNo"]); }).ToList();

            if (list ==null || list.Count ==0)
            {
                return null;
            }
            var quoteSchedule = list[0];
            postBooking["porPlcNm"] = quoteSchedule["polNm"];
            postBooking["dlyPlcNm"] = quoteSchedule["podNm"];
            postBooking["vslCd"] = quoteSchedule["vslCd"];
            postBooking["obrdDt"] = quoteSchedule["polEtb"];
            postBooking["etd"] = quoteSchedule["etd"].ToString() + quoteSchedule["etdTm"];
            postBooking["eta"] = quoteSchedule["eta"].ToString() + quoteSchedule["etaTm"];
            postBooking["hidLegInfo"] = quoteSchedule["info"];
            postBooking["podTrmlCd"] = quoteSchedule["itrmlCd"];
            postBooking["cgoRest"] = quoteSchedule["bkgDocCls"];
            postBooking["hidCS008I"] = quoteSchedule["info"];
            postBooking["podEta"] = quoteSchedule["eta"];
            postBooking["polPortNm"] = quoteSchedule["polNm"];
            postBooking["polPortCd"] = quoteSchedule["portCd"];
            postBooking["polTrmlCd"] = quoteSchedule["polTml"];
            postBooking["podCtrCd"] = quoteSchedule["podCtrCd"];
            postBooking["podPortNm"] = quoteSchedule["podNm"];
            return postBooking;
        }

        public async Task<Dictionary<string, object>> SearchSchedule()
        {
            Stopwatch stopwatch = new Stopwatch();

            // 启动 Stopwatch
            stopwatch.Start();      
            string url = "https://api.ekmtc.com/schedule/schedule/leg/search-schedule";
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["Jwt"] = jwt;
            headers["Referer"] = "https://www.ekmtc.com/";
            var result = await EasyHttpUtil.Instance.GetAsync(url, headers,scheduleData);
            
            stopwatch.Stop();
            // 获取经过的时间
            TimeSpan elapsedTime = stopwatch.Elapsed;
            // 输出执行时间
            Console.WriteLine($"查询航线方法执行时间：{elapsedTime.TotalMilliseconds} 毫秒");
            Logger.LogText("查询航线花费时间", $"{elapsedTime.TotalMilliseconds} 毫秒", Info.Id);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
        }


        public void PreparePostBooking()
        {
            postBooking["emailcheck"] = "Y";
            postBooking["bizPicEmlAddr"] = "service10@nb-hj.com";
            postBooking["bkgShprCstEnm"] = "HUANJI SUPPLY CHAIN MANAGEMENT CO.,LTD.";
            postBooking["docmRmk"] = "BAGS";
            postBooking["shprCstEnm"] = Info.ShipperName;
            postBooking["shprCstAddr"] = Info.ShipperAddr;
            postBooking["cneCstEnm"] = Info.ConsigNeeName;
            postBooking["cneCstAddr"] = Info.ConsigNeeAddr;
            postBooking["ntifCstEnm"] = Info.NotifyName;
            postBooking["ntifCstAddr"] = Info.NotifyAddr;
            postBooking["bookingAgent"] = "NHJC01";
            postBooking["chemicalYn"] = "N";
            postBooking["consYn"] = "Y";
            postBooking["porCtrCd"] = Info.Departure.ctrCd;
            // postBooking["porPlcNm"] = quoteSchedule["polNm"];
            postBooking["porPlcCd"] = Info.Departure.plcCd;
            postBooking["dlyCtrCd"] = Info.Arrival.ctrCd;
            // postBooking["dlyPlcNm"] = quoteSchedule["podNm"];
            postBooking["dlyPlcCd"] = Info.Arrival.plcCd;
            var vesselAndVoyage = Info.VesselName.Split("-");
            var vessel = vesselAndVoyage[0];
            var voyage = vesselAndVoyage[1];
            postBooking["vslNm"] = vessel;
            postBooking["voyNo"] = voyage;
            // postBooking["vslCd"] = quoteSchedule["vslCd"];
            // postBooking["obrdDt"] = quoteSchedule["polEtb"];
            // postBooking["etd"] = quoteSchedule["etd"].ToString() + quoteSchedule["etdTm"];
            // postBooking["eta"] = quoteSchedule["eta"].ToString() + quoteSchedule["etaTm"];
            // postBooking["hidLegInfo"] = quoteSchedule["info"];
            postBooking["legTermHPNT"] = "N";
            postBooking["podPortCd"] = Info.Arrival.plcCd;
            // postBooking["podTrmlCd"] = quoteSchedule["itrmlCd"];
            postBooking["schLogDtm"] = DateTime.Now.ToString("yyyyMMddHHmmss");
            postBooking["polShtmCd"] = Info.Departure.plcCatCd;
            postBooking["podShtmCd"] = Info.Arrival.plcCatCd;
            postBooking["issCtrCd"] = Info.Departure.ctrCd;
            postBooking["issPlcCd"] = Info.Departure.plcCd;
            postBooking["frtPncCd"] = "P";
            postBooking["pkgCd"] = "59";
            postBooking["emptyFlag"] = "F";
            postBooking["grsWt"] = Info.GrossWeight;
            postBooking["grsCbm"] = Info.Measurement;
            postBooking["kmdVslCheck"] = "Y";
            postBooking["polShaTsYn"] = "N";
            postBooking["fwdrCstNo"] = "NHJC01";
            var containerList = new List<Dictionary<string, object>>();
            var container = new Dictionary<string, object>();
            container["cntrSeq"] = 1;
            container["cntrSzCd"] = Info.Equipment.Substring(0, 2);
            container["cntrQty"] = Info.Quantity.ToString();
            container["pcupReqDt"] = "";
            container["pcupReqTm"] = "0900";
            container["pcupReqDtBak"] = "";
            container["pcupReqDtOld"] = "";
            container["rfHpmgUrl"] = "";
            container["pickUpPlace"] = "";
            container["pcupCyCd"] = "";
            container["pcupCyNm"] = "";
            container["pickUpMan"] = "";
            container["pickUpTel"] = "";
            container["shprVanYn"] = "N";
            container["feCatCd"] = "F";
            container["isCgoTypEmpty"] = true;
            container["cgoTypCd"] = "";
            container["cntrTypCd"] = Info.Equipment.Substring(2, 2);
            container["rfSetupTmpr"] = "";
            container["rfTmprUnitCd"] = "C";
            container["cntrRfStsCd"] = "";
            var containerDGList = new List<object>();
            container["containerDGList"] = containerDGList;
            container["chkNOR"] = "";
            container["apvNo"] = "";
            container["iotCntrYn"] = "N";
            var subCntrTypList = new List<Dictionary<string, object>>();

            var Dry = new Dictionary<string, object>();
            Dry["cdNm"] = "Dry";
            Dry["cd"] = "GP";
            Dry["rmk"] = "20/40";
            Dry["isShow"] = false;

            var HIGHCUBE = new Dictionary<string, object>();
            HIGHCUBE["cdNm"] = "HIGH CUBE";
            HIGHCUBE["cd"] = "HC";
            HIGHCUBE["rmk"] = "40/45";
            HIGHCUBE["isShow"] = true;

            var REEFER = new Dictionary<string, object>();
            REEFER["cdNm"] = "REEFER";
            REEFER["cd"] = "RF";
            REEFER["rmk"] = "20";
            REEFER["isShow"] = false;
            

            var REEFERHIGHCUBE = new Dictionary<string, object>();
            REEFERHIGHCUBE["cdNm"] = "REEFER HIGH CUBE";
            REEFERHIGHCUBE["cd"] = "RH";
            REEFERHIGHCUBE["rmk"] = "40";
            REEFERHIGHCUBE["isShow"] = true;

            var TANK = new Dictionary<string, object>();
            TANK["cdNm"] = "TANK";
            TANK["cd"] = "TK";
            TANK["rmk"] = "20/40";
            TANK["isShow"] = false;

            var OPENTOP = new Dictionary<string, object>();
            OPENTOP["cdNm"] = "OPEN TOP";
            OPENTOP["cd"] = "OT";
            OPENTOP["rmk"] = "20/40";
            OPENTOP["isShow"] = false;

            var FLATRACK = new Dictionary<string, object>();
            FLATRACK["cdNm"] = "FLAT RACK";
            FLATRACK["cd"] = "FR";
            FLATRACK["rmk"] = "20/40";
            FLATRACK["isShow"] = false;

            subCntrTypList.Add(Dry);
            subCntrTypList.Add(HIGHCUBE);
            subCntrTypList.Add(REEFER);
            subCntrTypList.Add(REEFERHIGHCUBE);
            subCntrTypList.Add(TANK);
            subCntrTypList.Add(OPENTOP);
            subCntrTypList.Add(FLATRACK);

            container["subCntrTypList"] = subCntrTypList;

            var subCgoTypList = new List<Dictionary<string, object>>();

            var HZ = new Dictionary<string, object>();
            HZ["cdNm"] = "HZ";
            HZ["cd"] = "01";
            HZ["rmk"] = "RF/GP/HC/RH/OT/TK";
            HZ["isShow"] = false;

            var OOG = new Dictionary<string, object>();
            OOG["cdNm"] = "OOG";
            OOG["cd"] = "02";
            OOG["rmk"] = "OT/FR/SR";
            OOG["isShow"] = true;

            var ING = new Dictionary<string, object>();
            ING["cdNm"] = "ING";
            ING["cd"] = "03";
            ING["rmk"] = "OT/FR/SR/HC";
            ING["isShow"] = true;

            var NOR = new Dictionary<string, object>();
            NOR["cdNm"] = "NOR";
            NOR["cd"] = "05";
            NOR["rmk"] = "RF/RH";
            NOR["isShow"] = true;

            var FB = new Dictionary<string, object>();
            FB["cdNm"] = "FB";
            FB["cd"] = "06";
            FB["rmk"] = "GP";
            FB["isShow"] = false;
            subCgoTypList.Add(HZ);
            subCgoTypList.Add(OOG);
            subCgoTypList.Add(ING);
            subCgoTypList.Add(NOR);
            subCgoTypList.Add(FB);
            container["subCgoTypList"] = subCgoTypList;
            containerList.Add(container);

            postBooking["containerList"] = containerList;
            var descInfoList = new List<Dictionary<string, object>>();
            var descInfo = new Dictionary<string, object>();
            descInfo["mrk"] = "N/M";
            descInfo["dscr"] = "BAGS";
            var markInfoList = new List<object>();
            descInfo["markInfoList"] = markInfoList ;
            descInfoList.Add(descInfo);
            postBooking["descInfoList"] = descInfoList;
            postBooking["fixdCgoYn"] = "N";
            // postBooking["cgoRest"] = quoteSchedule["bkgDocCls"];
            // postBooking["hidCS008I"] = quoteSchedule["info"];
            postBooking["scheduleEditYn"] = "Y";
            // postBooking["podEta"] = quoteSchedule["eta"];
            postBooking["bkgDt"] = DateTime.Now.ToString("yyyyMMdd");
            postBooking["polCtrCd"] = Info.Departure.ctrCd;
            // postBooking["polPortNm"] = quoteSchedule["polNm"];
            // postBooking["polPortCd"] = quoteSchedule["portCd"];
            // postBooking["polTrmlCd"] = quoteSchedule["polTml"];
            // postBooking["podCtrCd"] = quoteSchedule["podCtrCd"];
            // postBooking["podPortNm"] = quoteSchedule["podNm"];
            postBooking["pkgQty"] = Info.Packages;
        }

        public void PrepareScheduleData()
        {
            
            scheduleData["startPlcCd"] = Info.Departure.plcCd;
            scheduleData["searchMonth"] = Info.ShipDate.Value.Month.ToString().PadLeft(2, '0');
            scheduleData["bound"] = "O";
            scheduleData["startPlcName"] = Info.Departure.plcEnmOnly;
            scheduleData["destPlcCd"] = Info.Arrival.plcCd;
            scheduleData["searchYear"] = Info.ShipDate.Value.Year;
            scheduleData["filterYn"] = "N";
            scheduleData["searchYN"] = "N";
            scheduleData["startCtrCd"] = Info.Departure.ctrCd;
            scheduleData["destCtrCd"] = Info.Arrival.ctrCd;
            scheduleData["filterTs"] = "Y";
            scheduleData["filterDirect"] = "Y";
            scheduleData["filterTranMax"] = "0";
            scheduleData["filterTranMin"] = "0";
            scheduleData["destPlcName"] = Info.Arrival.plcNm;
            scheduleData["main"] = "N";
            scheduleData["legIdx"] = "0";
            scheduleData["vslType01"] = "01";
            scheduleData["vslType03"] = "03";
            scheduleData["eiCatCd"] = "O";
            scheduleData["calendarOrList"] = "C";
            scheduleData["cpYn"] = "N";
            scheduleData["promotionChk"] = "N";
        }

        public void Dispose()
        {
            Console.WriteLine("[销毁]销毁Worker " + Info.Id);
            Logger.LogText("任务完成 销毁任务", "", Info.Id);
            TickTimer.Instance.Event -= Handle;
        }
    }
}