using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public bool IsLogin()
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
        /// 登录
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Result<string>> Login(LoginModel request)
        {

            if (string.IsNullOrEmpty(request.Account) || string.IsNullOrEmpty(request.Password))
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "账号或密码不能为空"
                };
            }

            if (request.Account == "jst" && request.Password == "jst#jiat_0519")
            {
                HttpContext.Session.Set("login", Encoding.UTF8.GetBytes("success"));
                
                return new Result<string>()
                {
                    Status = true,
                    Message = "登录成功",
                };
            }
            else
            {
                return new Result<string>()
                {
                    Status = false,
                    Message = "账号或密码错误"
                };
            }
        }


        


        



        

        

        


    }
}
