using System;

namespace Shinobu.Helpers
{
    public class UIColorHelper
    {
        public static uint Djb2(string str)
        {
            var hash = 5381;
            for (var i = 0; i < str.Length; i++)
            {
                hash = ((hash << 5) + hash) + str[i];
            }
            return (uint)hash;
        }

        public static string HashStringToColor(string str)
        {
            var hash = Djb2(str);
            var r = (hash & 0xFF0000) >> 16;
            var g = (hash & 0x00FF00) >> 8;
            var b = hash & 0x0000FF;
            string rHex = r.ToString("X2");
            string gHex = g.ToString("X2");
            string bHex = b.ToString("X2");
            return "#" + rHex + gHex + bHex;
        }

        public static bool DarkOrLightColor(string hexColor)
        {
            var r = Convert.ToByte(hexColor.Substring(1, 2), 16);
            var g = Convert.ToByte(hexColor.Substring(3, 2), 16);
            var b = Convert.ToByte(hexColor.Substring(5, 2), 16);
            var brightness = (r * 299 + g * 587 + b * 114) / 1000;
            return brightness < 128;
        }
    }
}
