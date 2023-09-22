using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Jst.SynchubClient.Kmtc.entity;
using Jst.SynchubClient.One;
using Newtonsoft.Json.Linq;
using SynchubServer.entity;

namespace Jst.SynchubClient.Kmtc.work
{
    public class KmtcTaskManager
    {
         public static KmtcTaskManager Instance { get; private set; } = new KmtcTaskManager();
        // Id 
        private Dictionary<string, KmtcWorker> _workers = new Dictionary<string, KmtcWorker>();
        
        
        // 同步任务的锁
        object syncMutex = new object();
        public void SyncTasks()
        {
            lock (syncMutex)
            {
                
                // 同步任务
                string content = Redis.Db.StringGet(RedisKeys.FetchTasks());
                // string content = Redis.Db.StringGet("tasks:tester");


                // 空任务  清空所有
                if (string.IsNullOrEmpty(content))
                {
                    foreach(var item in _workers)
                    {
                        try
                        {
                            DisposeWorker(item.Key);
                        }
                        catch
                        {
                            
                        }

                    }
                    return;
                }


                var parsedTasks = JArray.Parse(content);
                List<KmtcFormVo> tasks = new List<KmtcFormVo>();
                foreach (var item in parsedTasks)
                {
                    tasks.Add(item.ToObject<KmtcFormVo>());
                }
                // 忽略已经完成的
                tasks = tasks.Where(t=>t.Status != 2).ToList();
                
                
                // 新增
                foreach(var item in tasks)
                {
                    if (_workers.ContainsKey(item.Id) == false)
                    {
                        try
                        {
                            CancellationTokenSource source = new CancellationTokenSource();
                            var worker = new KmtcWorker(item,source);
                            _workers.Add(item.Id, worker);
                            // 初始化
                            _ = Logger.LogText("初始化任务", item.GetInfoString(), item.Id );
                            worker.Init();
                        }
                        catch
                        {
                            
                        }
                    }
                }

                // 删除
                var deleteId = _workers.Select(t => t.Key).Where(t =>
                {
                    // 不存在于tasks的
                    return tasks.Any(v => v.Id == t) == false;
                }).ToList();
                
                foreach (var id in deleteId)
                {

                    try
                    {
                        DisposeWorker(id);
                    }
                    catch
                    {

                    }
                }

                
                // 所有完成同步 

                // 更新不做处理

            }
        }
        public void DisposeWorker(string id)    
        {
            if (_workers.TryGetValue(id,out KmtcWorker value))
            {
                // 初始化
                _ = Logger.LogText("移除任务", value.Info.GetInfoString(), value.Info.Id);
                value.Dispose();
                _workers.Remove(id);
            }
        }
    }
}