using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EntMtextOrDimToSumOrCount
{
    public static class IniFile
    {
        public static Dictionary<string, string> Read(string path)
        {
            var data = new Dictionary<string, string>();

           

            foreach (var line in File.ReadAllLines(path))
            {
                if (line.Contains('='))
                {
                    var parts = line.Split(new string[] { "=" }, 2, StringSplitOptions.None);
                    data[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return data;
        }

        public static void Write(string path, Dictionary<string, string> data)
        {
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("[Settings]");
                foreach (var entry in data)
                {
                    writer.WriteLine($"{entry.Key}={entry.Value}");
                }
            }
        }

     
    }
}
