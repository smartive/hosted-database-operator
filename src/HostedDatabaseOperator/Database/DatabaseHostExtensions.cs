using System;
using System.Text;

namespace HostedDatabaseOperator.Database
{
    public static class DatabaseHostExtensions
    {
        public static string GenerateRandomPassword(this IDatabaseHost _)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                                 "abcdefghijklmnopqrstuvwxyz" +
                                 "0123456789";
            var rnd = new Random(DateTime.Now.Millisecond);

            var stringBuilder = new StringBuilder();
            for (var x = 0; x < 16; x++)
            {
                var index = rnd.Next(0, chars.Length - 1);
                stringBuilder.Append(chars[index]);
            }

            return stringBuilder.ToString();
        }
    }
}
