using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;
using SynchubServer.Cosco;
using SynchubServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SynchubServer.Controllers
{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class CoscoController : ControllerBase
    {

        const string MAX_ID_KEY = "maxTaskId";

        private string TaskInfoKey(string id)
        {
            return $"taskInfo:" + id;
        }
        private string ClientTasksKey(string code)
        {
            return $"tasks:" + code;
        }


        

        private bool isLogin()
        {
            if (HttpContext.Session.TryGetValue("login", out byte[] login))
            {
                string v = System.Text.Encoding.UTF8.GetString(login);
                if (v == "success")
                {
                    return true;
                }

            }
            return false;
        }

        /// <summary>
        /// 创建任务
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result<string>> CreateTask(synconhub_info info)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }

            #region 验证

            if (info == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "数据不能为空"
                };
            }

            if (string.IsNullOrEmpty(info.ApiVersion))
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "API版本不能为空"
                };
            }
            else
            {
                if(info.ApiVersion == "V1" || info.ApiVersion == "V2")
                {

                }
                else
                {
                    return new Result<string>()
                    {
                        Status = false,
                        Data = null,
                        Message = "API版本只能是V1或者V2"
                    };
                }
            }

            if (info.SearchSleepTime <= 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "查询间隔时间必须大于0"
                };
            }

            info.Status = 1;
            info.Message = "";
            if(info.RunCount<= 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "运行次数必须大于0"
                };
            }
            if(info.StartRunDate == null || info.EndRunDate == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "运行时间没有填完全"
                };
            }
            if(info.StartRunDate > info.EndRunDate)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "开始时间不能大于结束时间"
                };
            }

            
            info.preferPaymentTerms = "P";

            if(info.startDate == null && info.endDate == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "开航日期不能都为空"
                };
            }


            if (string.IsNullOrEmpty(info.porCityId))
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "出发地不能为空"
                };
            }

            if (string.IsNullOrEmpty(info.fndCityId))
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "目的地不能为空"
                };
            }



            if (string.IsNullOrEmpty(info.ContainerType))
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "箱子类型不能为空"
                };
            }


            if (info.ContainerCount <= 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "箱子数量必须大于0"
                };
            }


            if(info.RoundCount <= 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "下单轮数不能小于等于0"
                };
            }

            if(info.RoundSleepTime < 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "下单每轮间隔时间"
                };
            }

            #endregion




            string maxId = await Redis.Db.StringGetAsync(MAX_ID_KEY);
            if (string.IsNullOrEmpty(maxId))
            {
                maxId = "0";
            }

            if(int.TryParse(maxId, out int maxIdInt)){

                info.Id = (maxIdInt+1).ToString();
                await Redis.Db.StringSetAsync(MAX_ID_KEY,info.Id);

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
        /// 获得列表
        /// </summary>
        /// <param name="status"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<synconhub_info>> GetList(int status = -1)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            
            List<synconhub_info> results = new List<synconhub_info>();
            

            foreach(var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0]).Keys(15, TaskInfoKey("") + "*"))
            {
                results.Add(JObject.Parse(await Redis.Db.StringGetAsync(key)).ToObject<synconhub_info>());                
            }
            

            if (status != -1)
            {
                results = results.Where(x => x.Status == status).ToList();
            }
            results = results.OrderByDescending(t => int.Parse(t.Id)).ToList();
            return results;
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<string>> Delete(string id)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }

            // 先从运行中的client中删除
            var clients = await RunningClients();
            foreach(var client in clients)
            {
                if(client.Tasks.Any(t=>t.Id == id))
                {
                    await RemoveTaskInClient(client.Code, id);
                }
            }

            // 删除日志
            await Redis.Db.KeyDeleteAsync("log:" + id);

            await Redis.Db.KeyDeleteAsync(TaskInfoKey(id));


            

            return new Result<string>()
            {
                Status = true,
                Message = "删除成功"
            };
        }



        /// <summary>
        /// 搜索城市
        /// </summary>
        /// <param name="keyword"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetCityList(string keyword = "")
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            var req = new
            {
                keywords = keyword,
                page = 1,
                size = 30
            };

            var res = await CoscoRequester.Instance.Request("/service/synconhub/common/city/search", JObject.FromObject(req).ToString());
            return new JsonResult(res);
            /*
             * 返回样例
             {
                "code": 0,
                "message": "",
                "data": {
                    "content": [
                        {
                            "id": "738872886233057", // city id
                            "unlocode": "CNSZN", // port code
                            "cityName": "Shenzhen",
                            "cntyName": "Shenzhen", // county name
                            "stateName": "Guangdong",
                            "stateCode": "GD",
                            "ctryRegionName": "China", // country / region name
                            "ctryRegionCode": "CN", // country / region code
                            "cityFullNameEn": "Shenzhen",
                            "cityFullNameCn": "深圳"
                        }
                    ],
                    "number": 1,
                    "size": 30,
                    "totalPages": 1,
                    "totalElements": 1,
                    "first": true, // is first page
                    "last": true, // is last page
                    "empty": false
                }
            }
             */
        }

        /// <summary>
        /// 查询产品ID
        /// </summary>
        /// <param name="porCityId"></param>
        /// <param name="fndCityId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<SynconHubResponse> GetProductSearch(string porCityId, string fndCityId, DateTime? startDate = null, DateTime? endDate = null)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }

            var body = JObject.FromObject(new
            {
                startDate = $"{startDate.Value.ToString("yyyy-MM-dd")}T00:00:00.000Z",
                endDate = endDate.HasValue ? $"{endDate.Value.ToString("yyyy-MM-dd")}T00:00:00.000Z" : "",
                porCityId = porCityId,
                fndCityId = fndCityId,
                page = 1,
                size = 50
            }).ToString();

            var response = await CoscoRequester.Instance.Request("/service/synconhub/product/instantBooking/search", body, "POST");
            return response;
        }

        


        /// <summary>
        /// 运行中的节点
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<SyncClientInfo>> RunningClients()
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            List<SyncClientInfo> results = new List<SyncClientInfo>();


            foreach (var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0]).Keys(15, "client:*"))
            {
                var info = JObject.Parse(await Redis.Db.StringGetAsync(key)).ToObject<SyncClientInfo>();




                string tasksKey = "tasks:" + info.Code;


                // 同步任务
                string content = await Redis.Db.StringGetAsync(tasksKey);
                if (string.IsNullOrEmpty(content))
                {
                    info.Tasks = new List<synconhub_info>();
                }
                else
                {
                    var parsedTasks = JArray.Parse(content);
                    List<synconhub_info> tasks = new List<synconhub_info>();
                    foreach (var item in parsedTasks)
                    {
                        tasks.Add(item.ToObject<synconhub_info>());
                    }

                    info.Tasks = tasks;
                }


                results.Add(info);
            }

            return results;
        }

        [HttpGet]
        public async Task<List<synconhub_info>> GetClientTasks(string code)
        {

            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            string tasksKey = "tasks:" + code;

            // 同步任务
            var tasks = new List<synconhub_info>(); 
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (string.IsNullOrEmpty(content))
            {
                tasks = new List<synconhub_info>();
            }
            else
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<synconhub_info>());
                }
            }

            // 检查状态

            List<synconhub_info> result = new List<synconhub_info>();
            foreach(var task in tasks)
            {
                string taskInfoContent = await Redis.Db.StringGetAsync(TaskInfoKey(task.Id));
                var taskInfo = JObject.Parse(taskInfoContent).ToObject<synconhub_info>();
                if (taskInfo != null)
                {
                    result.Add(taskInfo);
                }
            }


            return result;
        }


        [HttpGet]
        public async Task<Result<string>> RemoveTaskInClient(string code,string id)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }

            string tasksKey = "tasks:" + code;

            // 同步任务
            var tasks = new List<synconhub_info>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (string.IsNullOrEmpty(content))
            {
                tasks = new List<synconhub_info>();
            }
            else
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<synconhub_info>());
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

        [HttpGet]
        public async Task<Result<string>> AddTaskToClient(string code, string id)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }

            if (string.IsNullOrEmpty(code))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "运行节点不能为空"
                };
            }

            var addTaskContent = await Redis.Db.StringGetAsync(TaskInfoKey(id));
            if (string.IsNullOrWhiteSpace(addTaskContent))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务不存在"
                };
            }
            
            var addTask = JObject.Parse(addTaskContent).ToObject<synconhub_info>();
            if(addTask.Status == 2)
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务状态已完成无法添加"
                };
            }
        



            string tasksKey = "tasks:" + code;

            // 同步任务
            var tasks = new List<synconhub_info>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (string.IsNullOrEmpty(content))
            {
                tasks = new List<synconhub_info>();
            }
            else
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<synconhub_info>());
                }
            }

            if (tasks.Any(t => t.Id == addTask.Id))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "任务已存在"
                };
            }
            tasks.Add(addTask);

            await Redis.Db.StringSetAsync(tasksKey, JArray.FromObject(tasks).ToString());
            sendSyncTask(code);
            return new Result<string>()
            {
                Status = true,
                Message = "成功"
            };
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="Page">分页信息</param>
        /// <param name="Limit">每页个数</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetLogs(string id, int Page = 1, int Limit = -1)
        {
            try
            {
                // if (isLogin() == false)
                // {
                //     throw new Exception("need login");
                // }
                RedisValue[] list = null;
                if (Page > 0 && Limit > 0)
                {
                    //起始页=(页数-1)*每页个数 终止页=起始页+每页个数-1
                    list = await Redis.Db.ListRangeAsync("log:" + id, (Page - 1) * Limit, (Page-1) * Limit + Limit - 1);
                }
                else
                {
                    list = await Redis.Db.ListRangeAsync("log:" + id);
                }
                List<LogInfo> results = new List<LogInfo>();

                foreach (var item in list)
                {
                    results.Add(JObject.Parse(item).ToObject<LogInfo>());
                }

                return new JsonResult(results);

            }
            catch(Exception ex)
            {
                return new JsonResult(ex.ToString());
            }
        }
        

        [HttpGet]
        public async Task<IActionResult> DownloadLogs(string id)
        {
            RedisValue[] list = await Redis.Db.ListRangeAsync("log:" + id);
   
            StringBuilder text = new StringBuilder();
            foreach (var item in list)
            {
                LogInfo info = JObject.Parse(item).ToObject<LogInfo>();
                text.AppendLine($"[{info.Time}] ({info.Type} / {info.Code}) 任务Id:{info.TaskId}  {info.Content}");
                text.AppendLine("------------------------------");
            }


            return File(Encoding.UTF8.GetBytes(text.ToString()), "application/octet-stream", $"{id}.log");
        }


        /// <summary>
        /// 清空一次日志
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<string>> ClearLog(string id)
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            await Redis.Db.KeyDeleteAsync("log:" + id);
            return new Result<string>()
            {
                Status = true,
                Message = "成功"
            };
        }

        /// <summary>
        /// 获得成功列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<List<SuccessOrderInfo>> GetSuccessList()
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            var list = await Redis.Db.ListRangeAsync("successList");
            List<SuccessOrderInfo> results = new List<SuccessOrderInfo>();

            foreach (var item in list)
            {
                results.Add(JObject.Parse(item).ToObject<SuccessOrderInfo>());
            }

            return results;
        }
        /// <summary>
        /// 清空成功列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<Result<string>> ClearSuccessLog()
        {
            // if (isLogin() == false)
            // {
            //     throw new Exception("need login");
            // }
            await Redis.Db.KeyDeleteAsync("successList");
            return new Result<string>()
            {
                Status = true,
                Message = "成功"
            };
        }


        private void sendSyncTask(string clientCode)
        {
            Redis.Connection.GetSubscriber().Publish("channel:" + clientCode, JObject.FromObject(new { Type = "sync-task" }).ToString());
        }
        
        
    }
}
