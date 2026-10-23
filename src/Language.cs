using System;

namespace PocketGrid
{
    internal static class Lang
    {
        static string current="zh";
        public static string Current { get { return current; } set { current=Normalize(value); } }
        public static string Normalize(string value)
        {
            return value=="en"||value=="ja"?value:"zh";
        }
        public static string Next(string value)
        {
            return value=="zh"?"en":value=="en"?"ja":"zh";
        }
        public static string Text(string zh,string en,string ja)
        {
            return current=="en"?en:current=="ja"?ja:zh;
        }
    }
}
