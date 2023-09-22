using Jst.SynchubClient.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jst.SynchubClient.Cosco
{
    public class CoscoRequester
    {
        public static CoscoRequester Instance { get; private set; } = new CoscoRequester();
        private CoscoRequester()
        {

        }






        const string X_DATE = "X-Coscon-Date";
        const string CONTENT_MD5 = "X-Coscon-Content-Md5";
        const string AUTHORIZATION = "X-Coscon-Authorization";
        const string DIGEST = "X-Coscon-Digest";
        const string COSCON_HMAC_HEADER = "X-Coscon-Hmac";
        const string REQUEST_LINE = "request-line";


        private static string _host { get { return Config.APIHost; } }
        private static string _apiKey { get { return Config.APIKey; } }
        private static string _apiSecret { get { return Config.APISecret; } }
        public static int RequestTimeout { get; set; } = 600;



        private static string SHA256(byte[] str)
        {
            SHA256Managed Sha256 = new SHA256Managed();
            byte[] by = Sha256.ComputeHash(str);

            return Convert.ToBase64String(by);
        }
        private static string GenerateMD5(string txt)
        {
            using (MD5 mi = MD5.Create())
            {
                byte[] buffer = Encoding.Default.GetBytes(txt);
                //开始加密
                byte[] newBuffer = mi.ComputeHash(buffer);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < newBuffer.Length; i++)
                {
                    sb.Append(newBuffer[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static byte[] HMACSHA1Text(string text, string key)
        {
            //HMACSHA1加密
            HMACSHA1 hmacsha1 = new HMACSHA1();
            hmacsha1.Key = System.Text.Encoding.UTF8.GetBytes(key);

            byte[] dataBuffer = System.Text.Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = hmacsha1.ComputeHash(dataBuffer);


            return hashBytes;

        }




        private HttpClient _client = null;
        private object getClientMutex = new object();
        private HttpClient GetClient()
        {

            if (_client == null)
            {
                lock (getClientMutex)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

                    _client = new HttpClient();
                    _client.Timeout = TimeSpan.FromSeconds(RequestTimeout);

                }
            }
            return _client;
        }

        public async Task<SynconHubResponse> Request(string taskId,string urlPath, string body, string method = "POST", CancellationTokenSource cancellationTokenSource = null)
        {
            try
            {
                /*
                 接口接入文档:
                https://github.com/cop-cos/COP
                 */

                byte[] bodyContent = System.Text.Encoding.UTF8.GetBytes(body);
                string requestLine = $"{method} {urlPath} HTTP/1.1";
                if (string.IsNullOrEmpty(urlPath))
                {
                    throw new Exception("urlPath 不能为空");
                }
                Dictionary<string, string> headers = new Dictionary<string, string>();

                string guidMd5 = GenerateMD5(Guid.NewGuid().ToString());
                // 使用美国时间
                string date = DateTime.Now.Subtract(TimeSpan.FromHours(8)).ToString("r");

                string digest = "SHA-256=" + SHA256(bodyContent);
                StringBuilder encodedSignature = new StringBuilder();
                encodedSignature.AppendFormat("{0}: {1}\n", X_DATE, date);
                encodedSignature.AppendFormat("{0}: {1}\n", DIGEST, digest);
                encodedSignature.AppendFormat("{0}: {1}\n", CONTENT_MD5, guidMd5);
                encodedSignature.AppendFormat("{0}", requestLine);
                byte[] sha1 = HMACSHA1Text(encodedSignature.ToString(), _apiSecret);
                string encodedSignatureStr = Convert.ToBase64String(sha1);

                StringBuilder hmacAuth = new StringBuilder();
                hmacAuth.AppendFormat("hmac username=\"{0}\",algorithm=\"hmac-sha1\",", _apiKey);
                hmacAuth.AppendFormat("headers=\"{0} {1} {2} {3}\",", X_DATE, DIGEST, CONTENT_MD5, REQUEST_LINE);
                hmacAuth.AppendFormat("signature=\"{0}\"", encodedSignatureStr);

                headers.Add(X_DATE, date);
                headers.Add(CONTENT_MD5, guidMd5);
                headers.Add(DIGEST, digest);
                headers.Add(COSCON_HMAC_HEADER, guidMd5);
                headers.Add(AUTHORIZATION, hmacAuth.ToString());
                var client = GetClient();

                _ = Logger.LogText("请求链接", _host + urlPath + "   " + method, taskId);

                if (method == "POST")
                {
                    _ = Logger.LogText("body", body, taskId);
                    var contentReq = new StringContent(body, Encoding.UTF8, "application/json");
                    foreach (var item in headers)
                    {
                        contentReq.Headers.Add(item.Key, item.Value);
                    }

                    var res = await client.PostAsync(_host + urlPath, contentReq);


                    var response = await res.Content.ReadAsStringAsync();


                    string info = "[请求] HttpStatus: " + res.StatusCode.ToString();
                    if (res.Headers.TryGetValues("X-RateLimit-Remaining-Minute", out IEnumerable<string> values))
                    {
                        info += " 剩余请求次数: " + (values.FirstOrDefault() ?? "");
                    }
                    _ = Logger.LogText("res", info, taskId);

                    _ = Logger.LogText("Content", response, taskId);

                    var parsed = JObject.Parse(response);

                    return parsed.ToObject<SynconHubResponse>();
                }
                else
                {
                    var req = new HttpRequestMessage()
                    {
                        RequestUri = new Uri(_host + urlPath),
                        Method = HttpMethod.Get
                    };
                    foreach (var item in headers)
                    {
                        req.Headers.Add(item.Key, item.Value);
                    }

                    if (cancellationTokenSource == null)
                    {
                        var res = await client.SendAsync(req);
                        var response = await res.Content.ReadAsStringAsync();
                        var parsed = JObject.Parse(response);
                        string info = "[请求] HttpStatus: " + res.StatusCode.ToString();
                        if (res.Headers.TryGetValues("X-RateLimit-Remaining-Minute", out IEnumerable<string> values))
                        {
                            info += " 剩余请求次数: " + (values.FirstOrDefault() ?? "");
                        }
                        _ = Logger.LogText("res", info, taskId);
                        _ = Logger.LogText("Content", response, taskId);
                        return parsed.ToObject<SynconHubResponse>();
                    }
                    else
                    {
                        var res = await client.SendAsync(req, cancellationTokenSource.Token);
                        var response = await res.Content.ReadAsStringAsync();
                        var parsed = JObject.Parse(response);
                        string info = "[请求] HttpStatus: " + res.StatusCode.ToString();
                        if (res.Headers.TryGetValues("X-RateLimit-Remaining-Minute", out IEnumerable<string> values))
                        {
                            info += " 剩余请求次数: " + (values.FirstOrDefault() ?? "");
                        }
                        _ = Logger.LogText("res", info, taskId);
                        _ = Logger.LogText("Content", response, taskId);
                        return parsed.ToObject<SynconHubResponse>();
                    }


                }


            }
            catch (Exception ex)
            {
                await LocalLogger.Main.LogText("Requestor",ex.ToString());
                throw ex;
            }

        }








    }
}
