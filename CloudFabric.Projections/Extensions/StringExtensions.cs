using System.Text.RegularExpressions;

namespace CloudFabric.Projections.Extensions;

public static partial class StringExtensions
{
    //TODO: define all special characters list
    //private const string ESCAPE_LIST = "[\\+\\-\\=\\&\\|\\!\\(\\)\\{\\}\\[\\]\\^\\\"\\~\\*\\<\\>\\?\\:\\\\\\/]";
    
    private const string ESCAPE_LIST = "[\\\\\\/]";
    public static string EscapeCharacters(this string instance)
    {
        return Regex.Replace(instance, ESCAPE_LIST, m => $@"\{m.Value}", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }
}
