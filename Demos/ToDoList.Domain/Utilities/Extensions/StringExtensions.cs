using System.Text;

namespace ToDoList.Domain.Utilities.Extensions;

public static class StringExtensions
{
    public static Guid HashGuid(this string str)
    {
        // Super fast non-cryptographic hash function: 
        // https://cyan4973.github.io/xxHash/
        // 128 version is used because that's what Guid uses for it's value
        var hash = new System.IO.Hashing.XxHash128();

        hash.Append(Encoding.UTF8.GetBytes(str));

        return new Guid(hash.GetCurrentHash());
    }
}
