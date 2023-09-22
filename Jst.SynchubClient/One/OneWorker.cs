using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jst.SynchubClient.Cosco;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SynchubServer.entity;
using SynchubServer.Models;
using SynchubServer.utils;
using System.Net;
using System.Net.Mail;

namespace Jst.SynchubClient.One
{
    public class OneWorker
    {
        public SearchInfo Info { get; private set; }
        private CancellationTokenSource _cancelToken;

        public OneWorker(SearchInfo info, CancellationTokenSource cancellationToken)
        {
            _cancelToken = cancellationToken;
            Info = info;
        }

        private string getQuotesUrl = "https://ecomm.one-line.com/api/v1/quotation/schedules/vessel-dates";
        private string postQuotesUrl = "https://ecomm.one-line.com/api/v1/quotation/quotes";
        private string getContractNumberUrl = "https://ecomm.one-line.com/api/v1/quotation/contracts";
        private string postOrderUrl = "https://ecomm.one-line.com/ecom/CUP_HOM_3201GS.do";
        private string submitOrderUrl = "https://ecomm.one-line.com/api/v1/quotation/contracts/";
        private string token;
        private string cookie;
        private Dictionary<string, object> quotesDic = new Dictionary<string, object>();
        private Dictionary<string, object> contractDictionary = new Dictionary<string, object>();
        private List<string> successList = new List<string>();

        public void Init()
        {
            // generateQueryBody();
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

            if (time >= Info.startRunDate && time < Info.endRunDate)
            {
                if (Trigger || Success || _cancelToken.IsCancellationRequested)
                {
                    return;
                }

                Trigger = true;
                _ = run();
            }
        }

        public bool getCookieAndToken()
        {
            token = Redis.Db.StringGet("one_spider_token");
            cookie = Redis.Db.StringGet("one_spider_cookie");
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(cookie))
            {
                return false;
            }
            else
            {
                token = "Bearer " + token;
                List<CookieModel> cookieList = JsonConvert.DeserializeObject<List<CookieModel>>(cookie);
                for (var i = 0; i < cookieList.Count; i++)
                {
                    if (cookieList[i].name.Equals("gnossJSESSIONID"))
                    {
                        cookie = cookieList[i].name + "=" + cookieList[i].value + ";";
                        break;
                    }
                }

                return true;
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
            await Task.Delay(Info.searchSleepTime);
        }

        public async Task execute()
        {
            int used = Info.references.Count;
            Dictionary<string, bool> references = new Dictionary<string, bool>();
            // 初始化提单数据
            foreach (var infoReference in Info.references)
            {
                references[infoReference] = false;
            }

            foreach (var reference in references)
            {
                while (!references[reference.Key])
                {
                    if (!getCookieAndToken())
                    {
                        return;
                    }

                    List<Dictionary<string, object>> quotes = getVesselQuotes();
                    if (quotes == null)
                    {
                        await Task.Delay(Info.searchSleepTime);
                        return;
                    }

                    Dictionary<string, object> quote = juegeFitQuote(quotes);
                    if (quote == null) return;

                    Dictionary<string, object> quoteResult = postQuotes();
                    if (quoteResult == null) return;


                    Dictionary<string, object> contract = getContract(quote);
                    if (contract == null) return;

                    Dictionary<string, object> postSubmitResult = postSubmitData(contract, reference.Key, quote);
                    if (postSubmitResult == null) return;

                    bool putSubmitResult = putSubmitData(contract, quote, reference.Key);
                    if (putSubmitResult)
                    {
                        references[reference.Key] = true;
                        used--;
                        successList.Add(reference.Key);
                    }
                }
            }


            if (successList.Count > 0 && used == 0)
            {
                Info.Status = 2;
                await Redis.Db.StringSetAsync(RedisKeys.TaskInfo(Info.Id,"one"), JObject.FromObject(Info).ToString());
                sendEmail();
                await Redis.Db.StringSetAsync("one:success:" + Info.Id, JArray.FromObject(successList).ToString());
                Dispose();
            }
        }


        public void sendEmail()
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
                            Config.Configuration["Email:SenderEmail"])
                        {
                            Subject = "【" + Info.originPort.displayedName + "-" + Info.destinationPort.displayedName +
                                      "】，柜型： " +
                                      Info.equipmentType.equipmentDisplayName + ", 航名：" + Info.vesselName + "航次" +
                                      Info.voyage + "下单成功！", // 邮件主题
                            Body = "成功的小提单号有：" + JArray.FromObject(successList).ToString(), // 邮件正文
                        };
                    smtpClient.Send(mailMessage);

                    Console.WriteLine("邮件发送成功！");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"发送邮件时出现错误：{e.Message}");
                throw;
            }
        }

        public bool putSubmitData(Dictionary<string, object> contract, Dictionary<string, object> quote,
            string reference)
        {
            List<Dictionary<string, object>> freightInfos =
                JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(quote["freightInfos"].ToString());
            Dictionary<string, object> freightInfos0 = freightInfos[0];
            string url = "https://ecomm.one-line.com/api/v1/quotation/contracts/" + contract["contractNo"] + "/submit";
            string bookingRequestUrl =
                "https://ecomm.one-line.com/ecom/CUP_HOM_3201.do?cbFncNmParam=getBookingStatus&cmdtCdParam=000000&dticParam=&eqQtyParam=" +
                Info.quantity + "&eqTpParam=" + Info.equipmentType.equipmentONECntrTpSz + "&oneSpotIdParam=" +
                freightInfos0["spotRateOfferingId"] + "&prmCgoSvcFlgParam=N&tmpCtrtNoParam=" + contract["contractNo"];

            Dictionary<string, string> headers = new Dictionary<string, string>();
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;
            headers["authorization"] = token;

            parameters["bookingNo"] = reference;
            parameters["bookingRequestUrl"] = bookingRequestUrl;
            parameters["emailLists"] = "one-ebooking@nb-hj.com";

            Task<Dictionary<string, object>> task = MyHttpClientUtil.Instance.PutAsync(url, headers, parameters);
            Dictionary<string, object> taskResult = task.Result;
            int code = (int) taskResult["code"];
            if (code == 200 && taskResult["data"] != null)
            {
                return true;
            }

            return false;
        }

        public Dictionary<string, object> postSubmitData(Dictionary<string, object> contract, string reference,
            Dictionary<string, object> quote)
        {
            Dictionary<string, object> submitData = getSubmitData(contract, reference, quote);
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;
            Task<Dictionary<string, object>> task =
                MyHttpClientUtil.Instance.PostFormAsync(postOrderUrl, headers, submitData);
            Dictionary<string, object> result = task.Result;
            Dictionary<string, object> data =
                JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
            if (data["RESULT"] != null && "OK".Equals(data["RESULT"].ToString()))
            {
                return data;
            }

            return null;
        }

        public Dictionary<string, object> getSubmitData(Dictionary<string, object> contract, string reference,
            Dictionary<string, object> quote)
        {
            Dictionary<string, object> submitData = new Dictionary<string, object>();
            Dictionary<string, string> headers = new Dictionary<string, string>();
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;
            headers["authorization"] = token;
            parameters["f_cmd"] = 40;
            parameters["oneSpotIdParam"] = contractDictionary["spotRateOfferingId"];
            parameters["aoqYn"] = "Y";
            parameters["tmpCtrtNoParam"] = contract["contractNo"];
            Task<Dictionary<string, object>> task =
                MyHttpClientUtil.Instance.PostFormAsync(postOrderUrl, headers, parameters);
            Dictionary<string, object> result = task.Result;
            int code = (int) result["code"];
            if (code != 200)
            {
                return null;
            }

            Dictionary<string, object> data =
                JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
            List<Dictionary<string, object>> list =
                JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(data["list"].ToString());
            Dictionary<string, object> list0 = list[0];
            List<Dictionary<string, object>> freightInfos =
                JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(quote["freightInfos"].ToString());
            Dictionary<string, object> freightInfos0 = freightInfos[0];
            submitData["f_cmd"] = 7;
            submitData["ibflag"] = "I";
            submitData["bkgXterCust_xter_cust_tp_cd[]"] = "S";
            submitData["bkgXterCust_xter_cust_tp_cd[]"] = "F";
            submitData["bkgXterCust_cust_nm[]"] = "HUANJI SUPPLY CHAIN MANAGEMENT CO.,LTD.";
            submitData["bkgXterCust_cust_addr[]"] =
                "11F,Fortune Building,No.828,Fuming Road,Jiangdong District,Ningbo China";
            submitData["bkgXterCust_xter_sndr_id[]"] = "AOQ";
            submitData["bkgXterCust_ibflag[]"] = "I";
            submitData["bkgXterCust_cnt_cd[]"] = "CN";
            submitData["bkgXterCust_cust_seq[]"] = 107368;
            submitData["bkgXterCust_cntc_nm[]"] = "NBHJGYL";
            submitData["cust_id"] = "Joan, Huang";
            submitData["cntc_eml"] = "one-ebooking@nb-hj.com";
            submitData["cust_co_nm"] = "HUANJI SUPPLY CHAIN MANAGEMENT CO.,LTD.";
            submitData["cntc_nm"] = "Joan, Huang";
            submitData["cntc_phn_no"] = "86-574-87643053";
            submitData["cntc_fax_no"] = "86-574-87643053";
            submitData["bkg_sts_cd"] = "null";
            submitData["mot_no"] = "200000129600";
            submitData["cmdt_desc"] = "OFFICE & SCHOOL SUPPLIES, OF PLASTICS";
            submitData["cmdt_cd"] = "392610";
            submitData["estm_wgt"] = 16000;
            submitData["estm_wgt_ut_cd"] = "KGS";
            submitData["pck_qty"] = 0;
            submitData["pck_tp_cd"] = "PK";
            submitData["xter_sndr_id"] = "AOQ";
            submitData["server_return_array"] = "ContainerDataStart";
            submitData["cntc_Eml"] = "one-ebooking@nb-hj.com";
            submitData["grid1_cntr_qty"] = 1;
            submitData["vsl_cd"] = "LSFT";
            submitData["rcv_term_cd"] = "Y";
            submitData["de_term_cd"] = "Y";
            submitData["rc_flg"] = "N";
            submitData["dcgo_flg"] = "N";
            submitData["awk_cgo_flg"] = "N";
            submitData["soc_flg"] = "N";
            submitData["shpr_own_trk_flg"] = "Y";
            submitData["rqst_pson_tp_cd"] = "F";
            submitData["third_party_yn"] = "N";
            submitData["prm_cgo_svc_flg"] = "N";
            submitData["flexport_yn"] = "N";
            submitData["homEmlSvcSubsc_eml_subsc_flg[]"] = "N";
            submitData["homEmlSvcSubsc_eml_svc_id[]"] = new List<string> {"EML0033,EML0034,EML0044"};
            submitData["homEmlSvcSubsc_bl_cntr_tp_cd[]"] = "B";
            submitData["homEmlSvcSubsc_eml_flg[]"] = "N";
            submitData["homEmlSvcSubsc_eml_track_no_flg[]"] = "N";
            submitData["homEmlSvcSubsc_ibflag[]"] = "I";
            submitData["grid1_ibflag[]"] = "I";
            submitData["grid1_usr_id[]"] = "NBHJGYL";
            submitData["grid1_soc_qty[]"] = 0;
            submitData["vslmngdata_ibflag"] = "I";

            submitData["bkg_no"] = reference;
            submitData["ctrt_no"] = contract["contractNo"];
            submitData["bkg_ofc_cd"] = list0["ofcCd"];
            submitData["rcv_term_cd"] = list0["rcvTermCd"];
            submitData["de_term_cd"] = list0["deTermCd"];
            submitData["por_cd"] = list0["porCd"];
            submitData["por_nm"] = list0["porNm"];
            submitData["pod_cd"] = list0["podCd"];
            submitData["pod_nm"] = list0["podNm"];
            submitData["pol_cd"] = list0["polCd"];
            submitData["pol_nm"] = list0["polNm"];
            submitData["del_cd"] = list0["delCd"];
            submitData["del_nm"] = list0["delNm"];
            submitData["rqst_dep_dt"] = list0["rqstDepDt"];
            string trnkVvdCd = list0["trnkVvdCd"].ToString();
            submitData["vsl_cd"] = trnkVvdCd.Substring(0, 4);
            submitData["vsl_nm"] = list0["trnkVvdNm"];
            submitData["skd_voy_no"] = trnkVvdCd.Substring(4, 4);
            submitData["skd_dir_cd"] = trnkVvdCd.Substring(trnkVvdCd.Length - 1);
            string oneCntrTpSz = Info.equipmentType.equipmentONECntrTpSz;
            submitData["grid1_cntr_tpsz_cd[]"] = oneCntrTpSz;
            submitData["grid1_cntr_tp_cd[]"] = oneCntrTpSz.Substring(0, 1);
            submitData["grid1_cntr_sz_cd[]"] = oneCntrTpSz.Substring(1);
            submitData["grid1_cntr_qty[]"] = Info.quantity;
            //TODO service code
            submitData["vslmngdata_n1st_lane_cd[]"] = freightInfos0["serviceCode"];
            submitData["vslmngdata_n1st_vvd[]"] = trnkVvdCd;
            submitData["vslmngdata_trnk_vvd[]"] = trnkVvdCd;
            submitData["vslmngdata_n1st_pol_cd[]"] = list0["polCd"];
            submitData["vslmngdata_n1st_pod_cd[]"] = list0["podCd"];
            submitData["one_spot_id"] = freightInfos0["spotRateOfferingId"];
            submitData["tmp_ctrt_no"] = contract["contractNo"];
            submitData["inter_rmk_ctnt"] = "NROF+(Invalid)+" + contract["contractNo"];
            submitData["aoq_json"] = list0["jsonResult"];
            submitData["aoq_qry_str"] =
                "cbFncNmParam=getBookingStatus&clngTpCdParam=&cmdtCdParam=000000&ctrtCustCdParam=&dticParam=&eqQtyParam=" +
                Info.quantity + "&eqTpParam=" + oneCntrTpSz + "&oneSpotIdParam=" + freightInfos0["spotRateOfferingId"] +
                "&prmCgoSvcFlgParam=N&tmpCtrtNoParam=" + contract["contractNo"];
            return submitData;
        }

        public Dictionary<string, object> getContract(Dictionary<string, object> quote)
        {
            Dictionary<string, object> contractDictionary = getContractDictionary(quote);
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["authorization"] = token;
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;

            Task<Dictionary<string, object>> task =
                MyHttpClientUtil.Instance.PostAsync(getContractNumberUrl, headers, contractDictionary);
            Dictionary<string, object> result = task.Result;
            int code = (int) result["code"];
            if (code == 201)
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
            }

            return null;
        }

        public Dictionary<string, object> getContractDictionary(Dictionary<string, object> quote)
        {
            List<Dictionary<string, object>> freightInfos =
                JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(quote["freightInfos"].ToString());
            Dictionary<string, object> freightInfo = freightInfos[0];
            Dictionary<string, object> eventLogInfo = new Dictionary<string, object>();
            eventLogInfo["isFromSavedQuote"] = false;
            eventLogInfo["isFromRecentSearch"] = false;
            eventLogInfo["deviceType"] = "Desktop";

            contractDictionary["basicFreeTime"] = 0;
            contractDictionary["commodity"] = "FAK,CONSOLIDATED/MIXED LOADS OF ITEMS";
            contractDictionary["containerList"] = quotesDic["equipmentTypes"];
            contractDictionary["customerCode"] = "CN107368";
            contractDictionary["customerName"] = "HUANJI SUPPLY CHAIN MANAGEMENT CO.,LTD.";
            contractDictionary["delCountryCode"] = Info.destinationPort.countryCode;
            contractDictionary["delCountryName"] = Info.destinationPort.countryName;
            contractDictionary["delLocation"] = Info.destinationPort.displayedName;
            contractDictionary["delLocationCode"] = Info.destinationPort.locationCode;
            contractDictionary["delLocationType"] = Info.destinationPort.locationType;
            ;
            contractDictionary["delUNLocationCode"] = Info.destinationPort.UNLocationCode;
            contractDictionary["freightInfo"] = freightInfo;
            contractDictionary["eventLogInfo"] = eventLogInfo;
            contractDictionary["isDetentionSubscribed"] = false;
            contractDictionary["isPremiumSubscribed"] = false;
            contractDictionary["isPromotionSubscribed"] = false;
            contractDictionary["oftCurrency"] = "USD";

            contractDictionary["porCountryCode"] = Info.originPort.countryCode;
            contractDictionary["porCountryName"] = Info.originPort.countryName;
            contractDictionary["porLocation"] = Info.originPort.displayedName;
            contractDictionary["porLocationCode"] = Info.originPort.locationCode;
            contractDictionary["porLocationType"] = Info.originPort.locationType;
            contractDictionary["porUNLocationCode"] = Info.originPort.UNLocationCode;
            contractDictionary["selectedAdditionalFreeTime"] = 0;
            contractDictionary["spotRateOfferingId"] = freightInfo["spotRateOfferingId"];
            contractDictionary["totalDetentionPrice"] = 0;
            contractDictionary["totalPriceHotDeal"] = 0;
            contractDictionary["totalPricePremium"] = 0;
            contractDictionary["totalPricePromotion"] = 0;
            contractDictionary["userId"] = "NBHJGYL";
            return contractDictionary;
        }

        public Dictionary<string, object> postQuotes()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers["authorization"] = token;
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;
            headers["accept-encoding"] = "gzip, deflate, br";
            headers["accept-language"] = "zh-CN,zh;q=0.9";
            Task<Dictionary<string, object>> task =
                MyHttpClientUtil.Instance.PostAsync(postQuotesUrl, headers, quotesDic);
            Dictionary<string, object> result = task.Result;
            int code = (int) result["code"];
            if (code == 201)
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
            }

            return null;
        }

        public Dictionary<string, object> juegeFitQuote(List<Dictionary<string, object>> quotes)
        {
            Dictionary<string, object> dictionary = quotes[0];
            var s = dictionary["departureDateEstimated"].ToString() + " 00:00:00";
            DateTime dateTime = DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", null);
            var newDate = dateTime.Subtract(TimeSpan.FromHours(8));
            quotesDic["vesselDate"] = newDate.ToString("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'");
            foreach (var item in quotes)
            {
                string departureDateEstimated = item["departureDateEstimated"].ToString();
                //比较航期 如果离港日期小于 afterDate那么说明不符合
                DateTime departureDate = DateTime.Parse(departureDateEstimated);
                DateTime afterDate = Info.afterDate;
                if (MyCommonUtil.CompareDate(departureDate, afterDate))
                {
                    continue;
                }

                List<Dictionary<string, object>> freightInfos =
                    JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(item["freightInfos"].ToString());
                Dictionary<string, object> freightInfo = freightInfos[0];
                if ("Sold Out".Equals(freightInfo["status"]))
                {
                    continue;
                }

                if (Info.vesselName.Equals(freightInfo["transportName"].ToString()) &&
                    Info.voyage.Equals(freightInfo["conveyanceNumber"].ToString()))
                {
                    prepareQuotes(quotes);
                    return item;
                }
            }

            return null;
        }

        public List<Dictionary<string, object>> getVesselQuotes()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            Dictionary<string, object> container = new Dictionary<string, object>();
            headers["authorization"] = token;
            headers["referer"] = "https://ecomm.one-line.com/";
            headers["cookie"] = cookie;
            List<Dictionary<string, object>> containers = new List<Dictionary<string, object>>();
            container["equipmentIsoCode"] = Info.equipmentType.equipmentIsoCode;
            container["equipmentName"] = Info.equipmentType.equipmentDisplayName;
            container["quantity"] = Info.quantity;
            container["cargoType"] = Info.equipmentType.cargoType;
            container["sortIndex"] = Info.equipmentType.sortIndex;
            container["equipmentONECntrTpSz"] = Info.equipmentType.equipmentONECntrTpSz;
            container["equipmentSize"] = Info.equipmentType.equipmentSize;
            containers.Add(container);

            parameters["originLoc"] = Info.originPort.locationCode;
            parameters["destinationLoc"] = Info.destinationPort.locationCode;
            parameters["containers"] = containers;

            Task<Dictionary<string, object>> task =
                MyHttpClientUtil.Instance.PostAsync(getQuotesUrl, headers, parameters);
            Dictionary<string, object> result = task.Result;
            int code = (int) result["code"];
            var obj = JObject.Parse(result["data"].ToString()).ToObject<Dictionary<string, object>>();
            if (code == 201)
            {
                return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(obj["data"].ToString());
            }

            return null;
        }

        public void prepareQuotes(List<Dictionary<string, object>> quotes)
        {
            Dictionary<string, object> origin = new Dictionary<string, object>();
            origin["UNLocationCode"] = Info.originPort.UNLocationCode;
            origin["locationCode"] = Info.originPort.locationCode;
            origin["countryCode"] = Info.originPort.countryCode;
            origin["displayedName"] = Info.originPort.displayedName;
            origin["countryName"] = Info.originPort.countryName;
            origin["locationType"] = Info.originPort.locationType;

            Dictionary<string, object> destination = new Dictionary<string, object>();
            destination["UNLocationCode"] = Info.destinationPort.UNLocationCode;
            destination["locationCode"] = Info.destinationPort.locationCode;
            destination["countryCode"] = Info.destinationPort.countryCode;
            destination["displayedName"] = Info.destinationPort.displayedName;
            destination["countryName"] = Info.destinationPort.countryName;
            destination["locationType"] = Info.destinationPort.locationType;
            List<Dictionary<string, object>> equipmentTypes = new List<Dictionary<string, object>>();
            Dictionary<string, object> equipmentType = new Dictionary<string, object>();
            equipmentType["sortIndex"] = Info.equipmentType.sortIndex;
            equipmentType["cargoType"] = Info.equipmentType.cargoType;
            equipmentType["equipmentIsoCode"] = Info.equipmentType.equipmentIsoCode;
            equipmentType["equipmentName"] = Info.equipmentType.equipmentDisplayName;
            equipmentType["quantity"] = Info.quantity;
            equipmentType["equipmentSize"] = Info.equipmentType.equipmentSize;
            equipmentType["equipmentONECntrTpSz"] = Info.equipmentType.equipmentONECntrTpSz;
            equipmentType["isFocus"] = false;
            equipmentType["isError"] = false;
            equipmentTypes.Add(equipmentType);

            Dictionary<string, object> eventLogInfo = new Dictionary<string, object>();
            List<Dictionary<string, object>> freightInfo = new List<Dictionary<string, object>>();
            foreach (var item in quotes)
            {
                List<Dictionary<string, object>> freightInfos =
                    JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(item["freightInfos"].ToString());
                var info = freightInfos[0];
                var tempInfo = new Dictionary<string, object>();
                tempInfo["serviceCode"] = info["serviceCode"];
                tempInfo["duration"] = info["duration"];
                tempInfo["price"] = info["price"];
                tempInfo["status"] = info["status"];
                tempInfo["serviceName"] = info["serviceName"];
                tempInfo["transportName"] = info["transportName"];
                tempInfo["conveyanceNumber"] = info["conveyanceNumber"];
                tempInfo["freightCharges"] = info["freightCharges"];
                tempInfo["originCharges"] = info["originCharges"];
                tempInfo["destinationCharges"] = info["destinationCharges"];
                tempInfo["vgmCutoff"] = info["vgmCutoff"];
                tempInfo["docCutoff"] = info["docCutoff"];
                tempInfo["cyCutoff"] = info["cyCutoff"];
                tempInfo["departures"] = info["departures"];
                tempInfo["arrival"] = info["arrival"];
                tempInfo["spotRateOfferingId"] = info["spotRateOfferingId"];
                tempInfo["detentionCharges"] = info["detentionCharges"];
                tempInfo["validFromDateTime"] = info["validFromDateTime"];
                tempInfo["validToDateTime"] = info["validToDateTime"];
                tempInfo["portCutoff"] = info["portCutoff"];
                tempInfo["totalPricePromotion"] = 0;
                tempInfo["totalPricePremium"] = 0;
                tempInfo["premiumStatus"] = "";
                tempInfo["promotionStatus"] = "";
                tempInfo["departureDateEstimated"] = item["departureDateEstimated"];
                var arrival = JObject.Parse(info["arrival"].ToString()).ToObject<Dictionary<string, object>>();
                tempInfo["arrivalDateEstimated"] = arrival["arrivalDateEstimated"];
                tempInfo["isSelected"] = false;
                tempInfo["isAccepted"] = false;
                tempInfo["serviceType"] = "serviceType";
                tempInfo["routeType"] = "DIRECT";
                tempInfo["finalPrice"] = info["price"];


                freightInfo.Add(tempInfo);
            }

            eventLogInfo["isFromSavedQuote"] = false;
            eventLogInfo["isFromRecentSearch"] = false;
            eventLogInfo["deviceType"] = "Desktop";
            eventLogInfo["freightInfo"] = freightInfo;

            quotesDic["origin"] = origin;
            quotesDic["destination"] = destination;
            quotesDic["equipmentTypes"] = equipmentTypes;
            quotesDic["isFmc"] = false;
            quotesDic["eventLogInfo"] = eventLogInfo;
        }

        public void Dispose()
        {
            Console.WriteLine("[销毁]销毁Worker " + Info.Id);
            TickTimer.Instance.Event -= Handle;
        }
    }
}