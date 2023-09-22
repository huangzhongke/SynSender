using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
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

        public async Task<JObject> GetAsync(string url, Dictionary<string, string> headers,
            Dictionary<string, string> parameters)
        {
            HttpClient client = new HttpClient();
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
                foreach (var item in headers)
                {
                    req.Headers.Add(item.Key, item.Value);
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

        public async Task<Dictionary<string, object>> PostAsync(string url, Dictionary<string, string> headers,
            Dictionary<string, object> parameters)
        {
            HttpClient client = new HttpClient();
            // HttpContent httpContent = new StringContent(content, Encoding.UTF8, contentType);
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

        public async Task<Dictionary<string, object>> PutAsync(string url, Dictionary<string, string> headers, Dictionary<string, object> parameters)
        {
            HttpClient client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Put, url);

            request.Headers.Add("user-agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.0.0 Safari/537.36");
            foreach (var item in headers)
            {
                request.Headers.Add(item.Key, item.Value);
            }

            request.Content =
                new StringContent(JsonConvert.SerializeObject(parameters), Encoding.UTF8, "application/json");
            try
            {
                HttpResponseMessage res = await client.SendAsync(request);
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