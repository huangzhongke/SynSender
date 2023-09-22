using System;
using SynchubServer.entity;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SynchubServer.constant;
using SynchubServer.Models;
using SynchubServer.utils;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SynchubServer.Controllers

{
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class OneController : ControllerBase
    {
        const string MAX_ID_KEY = "one:maxTaskId";

        private string TaskInfoKey(string id)
        {
            return $"one:taskInfo:" + id;
        }

        [HttpGet]
        public async Task<Result<JObject>> GetPort(string location = "", string orgDest = "")

        {
            Redis.Init("192.168.1.10:6379,password=123456", 15);
            string token = "Bearer " + Redis.Db.StringGet(OneConstant.ONE_SPIDER_TOKEN);
            string cookie = Redis.Db.StringGet(OneConstant.ONE_SPIDER_COOKIE);
            if (token == null || cookie == null)
            {
                return new Result<JObject>()
                {
                    Status = false,
                    Message = "cookie 和 token为空"
                };
            }

            List<CookieModel> cookieList = JsonConvert.DeserializeObject<List<CookieModel>>(cookie);
            // List<CookieModel> cookieList = JsonSerializer.Deserialize<List<CookieModel>>(cookie);
            string cookieVal = "";
            for (var i = 0; i < cookieList.Count; i++)
            {
                if (cookieList[i].name.Equals("gnossJSESSIONID"))
                {
                    cookieVal = cookieList[i].name + "=" + cookieList[i].value + ";";
                    break;
                }
            }

            string url = OneConstant.ONE_URL_GETPORT;
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("location", location);
            parameters.Add("orgDest", orgDest);
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("authorization", token);
            headers.Add("referer", "https://ecomm.one-line.com/");
            headers.Add("cookie", cookieVal);
            var res = await MyHttpClientUtil.Instance.GetAsync(url, headers, parameters);
            return new Result<JObject>()
            {
                Status = true,
                Data = res,
                Message = "ok"
            };
        }

        [HttpPost]
        public async Task<Result<string>> CreateTask(SearchInfo info)
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

            if (info.quantity < 0)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "没票下单数至少为1"
                };
            }

            if (info.originPort == null || info.destinationPort == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "起始港和目的港 信息不能为空"
                };
            }

            if (info.equipmentType == null)
            {
                return new Result<string>()
                {
                    Status = false,
                    Data = null,
                    Message = "柜子信息不能为空"
                };
            }

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

        [HttpGet]
        public async Task<List<SearchInfo>> GetList(int status = -1)
        {
            List<SearchInfo> infos = new List<SearchInfo>();
            foreach (var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0])
                .Keys(15, TaskInfoKey("") + "*"))
            {
                string val = await Redis.Db.StringGetAsync(key);
                infos.Add(JObject.Parse(val).ToObject<SearchInfo>());
            }

            if (status != -1)
            {
                infos = infos.Where(x => x.Status == status).ToList();
            }

            infos = infos.OrderByDescending(t => int.Parse(t.Id)).ToList();
            return infos;
        }

        /**
         * 查询运行中的节点
         * 目前节点就一个cline:one
         */
        [HttpGet]
        public async Task<List<SyncOneClientInfo>> RunningClients()
        {
            //返回带任务的结果
            List<SyncOneClientInfo> results = new List<SyncOneClientInfo>();

            foreach (var key in Redis.Connection.GetServer(Redis.Connection.GetEndPoints()[0]).Keys(15, "client:*"))
            {
                SyncOneClientInfo info = JObject.Parse(await Redis.Db.StringGetAsync(key))
                    .ToObject<SyncOneClientInfo>();
                //tasks:one
                //client:* 查到的是 client:one 里面的code = one
                string tasksKey = "tasks:" + info.Code;
                //同步任务 获取 tasks:one 里的集合数据
                string content = await Redis.Db.StringGetAsync(tasksKey);
                if (string.IsNullOrEmpty(content))
                {
                    info.Tasks = new List<SearchInfo>();
                }
                else
                {
                    var parsedTasks = JArray.Parse(content);
                    List<SearchInfo> tasks = new List<SearchInfo>();
                    foreach (var item in parsedTasks)
                    {
                        tasks.Add(item.ToObject<SearchInfo>());
                    }

                    info.Tasks = tasks;
                }

                results.Add(info);
            }


            return results;
        }

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

            SearchInfo addTask = JObject.Parse(addTaskContent).ToObject<SearchInfo>();
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
            List<SearchInfo> tasks = new List<SearchInfo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (string.IsNullOrEmpty(content))
            {
                tasks = new List<SearchInfo>();
            }
            else
            {
                //client:one 内集合数据不为空 List<SearchInfo>
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<SearchInfo>());
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

        [HttpGet]
        public async Task<List<SearchInfo>> GetClientTasks(string code)
        {
            //获取已经加入运行节点的任务
            string tasksKey = "tasks:" + code;
            List<SearchInfo> searchInfos = new List<SearchInfo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (!string.IsNullOrEmpty(content))
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    searchInfos.Add(item.ToObject<SearchInfo>());
                }
            }

            //检查每个任务的status如果为0，2过滤掉
            List<SearchInfo> result = new List<SearchInfo>();
            foreach (var task in searchInfos)
            {
                string taskInfoContent = await Redis.Db.StringGetAsync(TaskInfoKey(task.Id));
                if (!string.IsNullOrEmpty(taskInfoContent))
                {
                    var taskInfo = JObject.Parse(taskInfoContent).ToObject<SearchInfo>();
                    if (taskInfo != null)
                    {
                        result.Add(taskInfo);
                    }
                }
            }
            return result;
        }

        private void sendSyncTask(string clientCode)
        {
            Redis.Connection.GetSubscriber().Publish("channel:" + clientCode,
                JObject.FromObject(new {Type = "sync-task"}).ToString());
        }

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

        [HttpGet]
        public async Task<Result<string>> RemoveTaskInClient(string code, string id)
        {
            string tasksKey = "tasks:" + code;
            // 同步任务
            var tasks = new List<SearchInfo>();
            string content = await Redis.Db.StringGetAsync(tasksKey);
            if (!string.IsNullOrEmpty(content))
            {
                var parsedTasks = JArray.Parse(content);
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<SearchInfo>());
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
    }
}