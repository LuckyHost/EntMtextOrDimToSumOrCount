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

        private static readonly string configPath = Path.Combine(GetDllDirectory(), "myConfig.ini");

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
            // Используем StreamWriter для прямой записи в файл
            using (var writer = new StreamWriter(configPath))
            {
                writer.WriteLine("[Settings]");
                writer.WriteLine(); // Пустая строка для наглядности

                // Комментарий для настройки roundResult
                writer.WriteLine("; Количество знаков для округления итогового результата (например: 2)");
                writer.WriteLine($"roundResult={this.roundResult}");
                writer.WriteLine();

                // Комментарий для настройки defaultCoefMultiplexResult
                writer.WriteLine("; Коэффициент умножения результата по умолчанию (разделитель - запятая, например: 1,04)");
                writer.WriteLine($"defaultCoefMultiplexResult={this.defaultCoefMultiplexResult}");
                writer.WriteLine();

                // Комментарий для настройки isShowCoefMultiplex
                writer.WriteLine("; Показывать ли пользователю запрос на ввод коэффициента? (1 = да, 0 = нет)");
                writer.WriteLine($"isShowCoefMultiplex={(this.isShowCoefMultiplex ? "1" : "0")}");
            }
        }

        private static string GetDllDirectory()
        {
            string path = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(path);
        }
    }
}
