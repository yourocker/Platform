using System;

namespace Core.Data.Extensions
{
    public static class NpgsqlJsonExtensions
    {
        public static string JsonExtractPathText(string jsonColumn, string key)
        {
            throw new InvalidOperationException("This method is for use with Entity Framework Core only and has no in-memory implementation.");
        }
    }
}