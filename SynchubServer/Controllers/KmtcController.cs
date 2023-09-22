using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Jst.SynchubClient.entity.kmtc;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SynchubServer.Models;
using SynchubServer.utils;

namespace SynchubServer.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class KmtcController
    {
        const string MAX_ID_KEY = "kmtc:maxTaskId";

        private string TaskInfoKey(string id)
        {
            return $"kmtc:taskInfo:" + id;
        }

        /// <summary>
        /// 获取港口信息
        /// </summary>
        /// <param name="placeName"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<List<Dictionary<string,object>>>> getPort(string placeName)
        {
            string url = "https://api.ekmtc.com/common/commons/places";
            var parameters = new Dictionary<string, object>();
            parameters["plcNm"] = placeName;

            var result = await EasyHttpUtil.Instance.GetAsync(url, null, parameters);
            
            return new Result<List<Dictionary<string,object>>>()
            {
                Status = true,
                Data = JsonConvert.DeserializeObject<List<Dictionary<string,object>>>(result["data"].ToString()),
                Message = "ok"
            };
        }
        
        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result<string>> CreateTask(KmtcFormVo info)
        {
            // Console.Write(JsonConvert.SerializeObject(info));
            if (info == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "数据不能为空"
                };
            }
            
            if (info.Departure == null || info.Arrival == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "起始港和目的港 信息不能为空"
                };
            }
            if (info.VesselName == null || info.Voyage == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "船名航次不能为空"
                };
            }
            if (info.Equipment == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "柜子信息不能为空"
                };
            }
            //将任务的运行状态置为1 默认值是0
            info.Status = 1;
            string maxId = await Redis.Db.StringGetAsync(MAX_ID_KEY);
            if (string.IsNullOrEmpty(maxId))
            {
                maxId = "0"; 
            }

            if (int.TryParse(maxId, out int maxIdInt))
            {
                info.Id = (maxIdInt + 1).ToString();
                await Redis.Db.StringSetAsync(MAX_ID_KEY, info.Id);

                // 保存数据

                await Redis.Db.StringSetAsync(TaskInfoKey(info.Id), JObject.FromObject(info).ToString());
                return new Result<string>()
                {
                    Status = true,
                    Data = info.Id,
                    Message = "创建完成"
                };
            }
            else
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "生成Id失败"
                };
            }
        }
        /// <summary>
        /// 获取任务列表
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<KmtcFormVo>> GetList(int status = -1)
        {
            List<KmtcFormVo> infos = new List<KmtcFormVo>();
            foreach (var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0])
                .Keys(15, TaskInfoKey("") + "*"))
            {
                string val = await Redis.Db.StringGetAsync(key);
                infos.Add(JObject.Parse(val).ToObject<KmtcFormVo>());
            }

            if (status != -1)
            {
                infos = infos.Where(x => x.Status == status).ToList();
            }

            infos = infos.OrderByDescending(t => int.Parse(t.Id)).ToList();
            return infos;
        }
        
        /// <summary>
        /// 删除节点
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<string>> Delete(string id)
        {
            //先获取在运行中的任务
            var clients = await RunningClients();
            foreach (var client in clients)
            {
                if (client.Tasks.Any(t => t.Id == id))
                {
                    await RemoveTaskInClient(client.Code, id);
                }
            }

            string key = TaskInfoKey(id);
            Redis.Db.KeyDelete(key);
            return new Result<string>()
            {
                Status = true,
                Message = "删除成功"
            };
        }
        
        /// <summary>
        /// 获取正在运行中的任务
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<KmtcClientInfo>> RunningClients()
        {
            //返回带任务的结果
            List<KmtcClientInfo> results = new List<KmtcClientInfo>();

            foreach (var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0]).Keys(15, "client:*"))
            {
                KmtcClientInfo info = JObject.Parse(await Redis.Db.StringGetAsync(key))
                    .ToObject<KmtcClientInfo>();
                
                //client:* 查到的是 client:one 里面的code = one
                string tasksKey = "tasks:" + info.Code;
                //同步任务 获取 tasks:one 里的集合数据
                string content = await Redis.Db.StringGetAsync(tasksKey);
                if (string.IsNullOrEmpty(content))
                {
                    info.Tasks = new List<KmtcFormVo>();
                }
                else
                {
                    var parsedTasks = JArray.Parse(content);
                    List<KmtcFormVo> tasks = new List<KmtcFormVo>();
                    foreach (var item in parsedTasks)
                    {
                        tasks.Add(item.ToObject<KmtcFormVo>());
                    }

                    info.Tasks = tasks;
                }

                results.Add(info);
            }


            return results;
        }
        
        /// <summary>
        /// 删除任务
        /// </summary>
        /// <param name="code"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<string>> RemoveTaskInClient(string code, string id)
        {
            string tasksKey = "tasks:" + code;
            // 同步任务
            var tasks = new List<KmtcFormVo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (!string.IsNullOrEmpty(content))
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<KmtcFormVo>());
                }
            }

            tasks.RemoveAll(t => t.Id == id);
            await Redis.Db.StringSetAsync(tasksKey, JArray.FromObject(tasks).ToString());

            sendSyncTask(code);
            return new Result<string>()
            {
                Status = true,
                Message = "成功"
            };
        }
        
        /// <summary>
        /// 删除或者增加任务后需要同步更新一下任务队列
        /// </summary>
        /// <param name="clientCode"></param>
        private void sendSyncTask(string clientCode)
        {
            Redis.Connection.GetSubscriber().Publish("channel:" + clientCode,
                JObject.FromObject(new {Type = "sync-task"}).ToString());
        }
        
        /// <summary>
        /// 新增任务加入到队列
        /// </summary>
        /// <param name="code"></param>
        /// <param name="id"></param>
        /// <returns></returns>
         [HttpGet]
        public async Task<Result<string>> AddTaskToClient(string code, string id)
        {
            if (string.IsNullOrEmpty(code))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "运行节点不能为空"
                };
            }

            // 前端添加任务到节点会带一个id和code,code是通过runningclients接口返回的数据可以得到=>one
            //查询 one:taskInfo:id 的数据
            var addTaskContent = await Redis.Db.StringGetAsync(TaskInfoKey(id));

            if (string.IsNullOrWhiteSpace(addTaskContent))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务不存在"
                };
            }

            KmtcFormVo addTask = JObject.Parse(addTaskContent).ToObject<KmtcFormVo>();
            //如果提交到运行的任务status=2表示已经执行结束不能再提交
            if (addTask.Status == 2)
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务状态已完成无法添加"
                };
            }

            //tasks:one 里面存放所有任务数据的集合
            string tasksKey = "tasks:" + code;
            List<KmtcFormVo> tasks = new List<KmtcFormVo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (string.IsNullOrEmpty(content))
            {
                tasks = new List<KmtcFormVo>();
            }
            else
            {
                //client:one 内集合数据不为空 List<SearchInfo>
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<KmtcFormVo>());
                }
            }

            //如果client:one  中已经有该数据提交记录 则失败
            if (tasks.Any(t => t.Id == addTask.Id))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务已存在"
                };
            }

            //将新任务数据放进集合
            tasks.Add(addTask);
            //将数据存进client:one
            await Redis.Db.StringSetAsync(tasksKey, JArray.FromObject(tasks).ToString());
            //推送任务 表示定时任务有新任务更新了，通过redis的监听通道功能
            sendSyncTask(code);
            return new Result<string>()
            {
                Status = true,
                Message = "成功"
            };
        }
        /// <summary>
        /// 获得客户端正在执行的任务
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<KmtcFormVo>> GetClientTasks(string code)
        {
            //获取已经加入运行节点的任务
            string tasksKey = "tasks:" + code;
            List<KmtcFormVo> searchInfos = new List<KmtcFormVo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (!string.IsNullOrEmpty(content))
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    searchInfos.Add(item.ToObject<KmtcFormVo>());
                }
            }

            //检查每个任务的status如果为0，2过滤掉
            List<KmtcFormVo> result = new List<KmtcFormVo>();
            foreach (var task in searchInfos)
            {
                string taskInfoContent = await Redis.Db.StringGetAsync(TaskInfoKey(task.Id));
                if (!string.IsNullOrEmpty(taskInfoContent))
                {
                    var taskInfo = JObject.Parse(taskInfoContent).ToObject<KmtcFormVo>();
                    if (taskInfo != null)
                    {
                        result.Add(taskInfo);
                    }
                }
            }
            return result;
        }

        [HttpPost]
        public async Task<Result<Dictionary<string, object>>> Search(KmtcScheduleVo vo)
        {
            if (vo == null)
            {
                return new Result<Dictionary<string, object>>()
                {
                    Status = false,
                    Data = null,
                    Message = "数据不能为空"
                };
            }
            if (vo.Departure == null)
            {
                return new Result<Dictionary<string, object>>()
                {
                    Status = false,
                    Data = null,
                    Message = "起运港数据不能为空"
                };
            }
            if (vo.Arrival == null)
            {
                return new Result<Dictionary<string, object>>()
                {
                    Status = false,
                    Data = null,
                    Message = "目的港数据不能为空"
                };
            }
            if (vo.ShipDate == null)
            {
                return new Result<Dictionary<string, object>>()
                {
                    Status = false,
                    Data = null,
                    Message = "船期不能为空"
                };
            }
            var resultData = new Dictionary<string, object>();

            var schedule = await SearchSchedule(vo);
            var listSchedule = JsonConvert.DeserializeObject<List<Dictionary<string, Object>>>(schedule["listSchedule"].ToString());
            listSchedule = listSchedule.Where(data =>
            {
                return DateTime.ParseExact(data["etd"].ToString(), "yyyyMMdd", CultureInfo.InvariantCulture).Month == vo.ShipDate.Value.Month;
            }).ToList();
            var vessels = new List<Dictionary<string, object>>();
            var vesselAndVoyage = new Dictionary<string, object>();
            foreach (var item in listSchedule)
            {
                var temp = new Dictionary<string, object>();
                var time = DateTime.ParseExact(item["etd"].ToString() + item["etdTm"], "yyyyMMddHHmm", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd");
                var voyage = item["voyNo"].ToString();
                temp["label"] = time;
                temp["value"] = item["vslNm"].ToString() + "-" + voyage;
                vessels.Add(temp);
                //TODO 需要修改下key的值，因为同一个月份会出现相同航名，会造成冲突
                var key = item["vslNm"] + "-" + voyage;
                vesselAndVoyage[key] = item["voyNo"].ToString();
                
            }

            resultData["vesselOptions"] = vessels;
            resultData["vesselWithVoyage"] = vesselAndVoyage;
            return new Result<Dictionary<string, object>>()
            {
                Status = true,
                Data = resultData,
            };;
        }
        
        
        private async Task<Dictionary<string, object>> SearchSchedule(KmtcScheduleVo Info)
        {
            string url = "https://api.ekmtc.com/schedule/schedule/leg/search-schedule";
            Dictionary<string, object> parameter = new Dictionary<string, object>();
            parameter["startPlcCd"] = Info.Departure.plcCd;
            parameter["searchMonth"] = Info.ShipDate.Value.Month.ToString().PadLeft(2, '0');
            parameter["bound"] = "O";
            parameter["startPlcName"] = Info.Departure.plcEnmOnly;
            parameter["destPlcCd"] = Info.Arrival.plcCd;
            parameter["searchYear"] = Info.ShipDate.Value.Year;
            parameter["filterYn"] = "N";
            parameter["searchYN"] = "N";
            parameter["startCtrCd"] = Info.Departure.ctrCd;
            parameter["destCtrCd"] = Info.Arrival.ctrCd;
            parameter["filterTs"] = "Y";
            parameter["filterDirect"] = "Y";
            parameter["filterTranMax"] = "0";
            parameter["filterTranMin"] = "0";
            parameter["destPlcName"] = Info.Arrival.plcNm;
            parameter["main"] = "N";
            parameter["legIdx"] = "0";
            parameter["vslType01"] = "01";
            parameter["vslType03"] = "03";
            parameter["eiCatCd"] = "O";
            parameter["calendarOrList"] = "C";
            parameter["cpYn"] = "N";
            parameter["promotionChk"] = "N";

            Dictionary<string, string> headers = new Dictionary<string, string>();
            // headers["Jwt"] = jwt;
            headers["Referer"] = "https://www.ekmtc.com/";
            var result = await EasyHttpUtil.Instance.GetAsync(url, headers, parameter);

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(result["data"].ToString());
        }
    }
}