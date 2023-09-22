using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using SynchubServer.Models;

namespace SynchubServer.utils
{
    public class MyHttpClientUtil
    {
        // private  HttpClient client;
        public static MyHttpClientUtil Instance = new MyHttpClientUtil();
        // public MyHttpClientUtil()
        // {
        //     client = new HttpClient();
        // }

        public async Task<JObject> GetAsync(string url,Dictionary<string, string> headers,Dictionary<string, string> parameters)
        {   HttpClient client = new HttpClient();
            try
            {
              
                string queryString = BuildQueryString(parameters);
                var req = new HttpRequestMessage()
                {
                    RequestUri = new Uri(url + queryString),
                    Method = HttpMethod.Get
                };
                req.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
                if (headers !=null)
                {
                    foreach (var item in headers)
                    {
                        req.Headers.Add(item.Key, item.Value);
                    }  
                }
               
                var res = await client.SendAsync(req);
                var response = await res.Content.ReadAsStringAsync();
                var parsed = JObject.Parse(response);
                return parsed;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        public async Task<string> PostAsync(string url, string content, string contentType = "application/json")
        {
            HttpClient client = new HttpClient();
            HttpContent httpContent = new StringContent(content, Encoding.UTF8, contentType);
            HttpResponseMessage response = await client.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> PutAsync(string url, string content, string contentType = "application/json")
        {
            HttpClient client = new HttpClient();
            HttpContent httpContent = new StringContent(content, Encoding.UTF8, contentType);
            HttpResponseMessage response = await client.PutAsync(url, httpContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

       
        private static string BuildQueryString(Dictionary<string, string> parameters)
        {
            var queryBuilder = new StringBuilder();

            foreach (var parameter in parameters)
            {
                string encodedKey = HttpUtility.UrlEncode(parameter.Key);
                string encodedValue = HttpUtility.UrlEncode(parameter.Value);

                if (queryBuilder.Length > 0)
                {
                    queryBuilder.Append("&");
                }

                queryBuilder.Append($"{encodedKey}={encodedValue}");
            }

            return queryBuilder.Length > 0 ? "?" + queryBuilder.ToString() : "";
        }
    }
}