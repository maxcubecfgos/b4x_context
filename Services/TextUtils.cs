namespace B4XContext.Services
{
    public static class TextUtils
    {
        public static string AppendBlock(string? current, string block)
        {
            if (string.IsNullOrWhiteSpace(block))
                return current ?? "";
            return string.IsNullOrWhiteSpace(current) ? block : current + "\n\n" + block;
        }
    }
}