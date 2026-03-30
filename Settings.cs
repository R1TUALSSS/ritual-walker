using LowLevelInput.Hooks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RitualWalker
{
    public class Settings
    {
        public int ActivationKey { get; set; } = (int)VirtualKeyCode.C;
        public double PingCompensation { get; set; } = 0.02; // 20ms default ping compensation

        public void CreateNew(string path)
        {
            using (StreamWriter sw = new StreamWriter(File.Create(path)))
            {
                sw.WriteLine("/* All Corresponding Key Bind Key Codes");
                foreach (int i in Enum.GetValues(typeof(VirtualKeyCode)))
                {
                    sw.WriteLine($"* \t{i} - {(VirtualKeyCode)i}");
                }
                sw.WriteLine("*/");
                sw.WriteLine("/* PingCompensation: Ping'inize göre ayarlayın (saniye cinsinden, örn: 0.03 = 30ms) */");
                sw.WriteLine(JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public void Load(string path)
        {
            var loaded = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path));
            ActivationKey = loaded.ActivationKey;
            PingCompensation = loaded.PingCompensation;
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
