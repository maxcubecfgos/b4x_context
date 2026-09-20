using System;
using SharpToken;

namespace B4XContext.Services
{
    public static class TokenCounter
    {
        private static readonly GptEncoding? Encoding = TryCreateEncoding();

        private static GptEncoding? TryCreateEncoding()
        {
            try
            {
                return GptEncoding.GetEncoding("cl100k_base");
            }
            catch
            {
                return null;
            }
        }

        public static int Count(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            if (Encoding != null)
            {
                try
                {
                    return Encoding.CountTokens(text);
                }
                catch
                {
                }
            }
            return Math.Max(0, text.Length / 4);
        }
    }
}