using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace SynchubServer.utils
{
    public class EasyHttpUtil
    {
        public static EasyHttpUtil Instance = new EasyHttpUtil();
        private  readonly HttpClient client;
        public EasyHttpUtil()
        {
            HttpClientHandler clientHandler = new HttpClientHandler();
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            client = new HttpClient(new HttpClientHandler
                {
                    MaxConnectionsPerServer = 100 // 设置最大连接数为 10
                }
            );
        }

        public async Task<Dictionary<string, object>> GetAsync(string url, Dictionary<string, string> headers,
            Dictionary<string, object> parameters)
        {
            try
            {
                string queryString = BuildQueryString(parameters);
                var req = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url + queryString),
                    Method = HttpMethod.Get
                };
                req.Headers.Add("user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
                if (headers != null)
                {
                    foreach (var item in headers)
                    {
                        req.Headers.Add(item.Key, item.Value);
                    }

                }
               
                var res = await client.SendAsync(req);
                var response = await res.Content.ReadAsStringAsync();
                // var parsed = JObject.Parse(response);
                var result = new Dictionary<string, object>();
                result["code"] = (int) res.StatusCode;
                result["data"] = response;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        
        
        
        private static string BuildQueryString(Dictionary<string, object> parameters)
        {
            var queryBuilder = new StringBuilder();

            foreach (var parameter in parameters)
            {
                string encodedKey = HttpUtility.UrlEncode(parameter.Key);
                string encodedValue = HttpUtility.UrlEncode(parameter.Value.ToString());

                if (queryBuilder.Length > 0)
                {
                    queryBuilder.Append("&");
                }

                queryBuilder.Append($"{encodedKey}={encodedValue}");
            }

            return queryBuilder.Length > 0 ? "?" + queryBuilder.ToString() : "";
        }
        
        public async Task<Dictionary<string, object>> PostFormAsync(string url, Dictionary<string, string> headers,
            Dictionary<string, object> parameters)
        {
            HttpClientHandler handler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            HttpClient httpClient = new HttpClient(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");

            foreach (var header in headers)
            {
                httpClient.DefaultRequestHeaders
                    .Add(header.Key, header.Value);
            }

            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            foreach (var item in parameters)
            {
                list.Add(new KeyValuePair<string, string>(item.Key, item.Value.ToString()));
            }


            var content = new FormUrlEncodedContent(list);
            try
            {
                HttpResponseMessage res = await httpClient.PostAsync(url, content);
                var response = await res.Content.ReadAsStringAsync();
                var parsed = JObject.Parse(response);
                var result = new Dictionary<string, object>();
                result["code"] = (int) res.StatusCode;
                result["data"] = parsed;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url">请求路径</param>
        /// <param name="headers">请求头</param>
        /// <param name="parameters">请求参数</param>
        /// <returns>结果集合有 code 和 data两个key</returns>
        public async Task<Dictionary<string, object>> PostAsync(string url, Dictionary<string, string> headers,
            Dictionary<string, object> parameters)
        {
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            try
            {
                request.Headers.Add("user-agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
                foreach (var item in headers)
                {
                    request.Headers.Add(item.Key, item.Value);
                }

                request.Content = new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8,
                    "application/json");
                HttpResponseMessage res = await client.SendAsync(request);
                var response = await res.Content.ReadAsStringAsync();
                // var parsed = JObject.Parse(response);
                var result = new Dictionary<string, object>();
                result["code"] = (int) res.StatusCode;
                result["data"] = response;
                return result;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Error(e.Message);
                return null;
            }
        }
    }
}