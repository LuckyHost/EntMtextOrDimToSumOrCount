using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace EntMtextOrDimToSumOrCount
{
    public class Config
    {
        public int roundResult { get; set; } = 9;
        public double defaultCoefMultiplexResult { get; set; } = 1;
        public bool isShowCoefMultiplex{ get; set; } = true;

        private static readonly string pathDLLFile = GetDllDirectory();
        private static readonly string configPath = Path.Combine(pathDLLFile, "myConfig.ini");

        public static Config LoadConfig()
        {
            // Проверяем, существует ли INI-файл
            if (!File.Exists(configPath))
            {
                // Если файла нет, создаём его с дефолтными параметрами
                var defaultConfig = new Config();
                defaultConfig.SaveConfig();
                return defaultConfig;
            }

            // Если файл существует, читаем его
            var data = IniFile.Read(configPath);

            return new Config


            {
                defaultCoefMultiplexResult = data.ContainsKey("defaultCoefMultiplexResult") ? double.Parse(data["defaultCoefMultiplexResult"]) : 1,
                roundResult = data.ContainsKey("roundResult") ? int.Parse(data["roundResult"]) : 9,
                isShowCoefMultiplex = data.ContainsKey("isShowCoefMultiplex") ? data["isShowCoefMultiplex"] == "1" : true


            };
        }

        public void SaveConfig()
        {
            var data = new Dictionary<string, string>
        {
            { "defaultCoefMultiplexResult", defaultCoefMultiplexResult.ToString() },
            { "roundResult", roundResult.ToString() },
            { "isShowCoefMultiplex", isShowCoefMultiplex ? "1" : "0" }
        };

            IniFile.Write(configPath, data);
        }

        private static string GetDllDirectory()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(path);
        }
    }
}
