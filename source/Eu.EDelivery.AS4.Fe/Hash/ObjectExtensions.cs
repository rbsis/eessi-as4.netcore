using System.Security.Cryptography;
using System.Text;

namespace Eu.EDelivery.AS4.Fe.Hash;

public static class ObjectExtensions
{
    public static string GetMd5Hash(this string obj)
    {
        var inputBytes = Encoding.ASCII.GetBytes(obj.Replace("\r", "").Replace("\n", "").Replace("\t", ""));
        var sb = new StringBuilder();
        var hash = MD5.HashData(inputBytes);
        foreach (var t in hash)
        {
            sb.Append(t.ToString("X2"));
        }

        return sb.ToString();
    }
}
