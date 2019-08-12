using System;
using PolicyLib;
using Newtonsoft.Json.Linq;

namespace SamplePolicyProject
{
    public class PolicySourceCode
    {
        public static IProxyRequestContext context { get; set; }

        public static bool CheckForToken()
        {
            return context.Variables.ContainsKey("tokens")
                && ((JObject)context.Variables["tokens"]).GetValue("Token", StringComparison.OrdinalIgnoreCase) != null
                && !string.IsNullOrEmpty((string)((JObject)((JObject)context.Variables["tokens"]).GetValue("Token", StringComparison.OrdinalIgnoreCase)).GetValue("AccessToken", StringComparison.OrdinalIgnoreCase));
        }

        public static string GetAuthHeaderValue()
        {
            return " Bearer " + (string)((JObject)((JObject)context.Variables["tokens"]).GetValue("Token", StringComparison.OrdinalIgnoreCase)).GetValue("AccessToken", StringComparison.OrdinalIgnoreCase);
        }

        public static string GenerateCorrelationId()
        {
            var guidBinary = new byte[16];
            Array.Copy(Guid.NewGuid().ToByteArray(), 0, guidBinary, 0, 10);
            long time = DateTime.Now.Ticks;
            byte[] bytes = new byte[6];
            unchecked
            {
                bytes[5] = (byte)(time >> 40);
                bytes[4] = (byte)(time >> 32);
                bytes[3] = (byte)(time >> 24);
                bytes[2] = (byte)(time >> 16);
                bytes[1] = (byte)(time >> 8);
                bytes[0] = (byte)(time);
            }
            Array.Copy(bytes, 0, guidBinary, 10, 6);
            return new Guid(guidBinary).ToString();
        }
    }
}
