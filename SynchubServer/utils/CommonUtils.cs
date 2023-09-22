using System;
using System.IO;

namespace SynchubServer.utils
{
    public class CommonUtils
    {
        public static string readJsonToStr(string fileName)
        {
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // 拼接JSON文件的完整路径
            string jsonFilePath = Path.Combine("./", fileName);

            // 检查文件是否存在
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    // 读取JSON文件内容
                    string jsonContent = File.ReadAllText(jsonFilePath);

                    // 打印JSON内容
                    Console.WriteLine(jsonContent);
                    return jsonContent;
                }
                catch (IOException e)
                {
                    Console.WriteLine($"读取文件时发生错误: {e.Message}");
                }
            }
            else
            {
                Console.WriteLine("JSON文件不存在");
            }

            return null;
        }
    }
}