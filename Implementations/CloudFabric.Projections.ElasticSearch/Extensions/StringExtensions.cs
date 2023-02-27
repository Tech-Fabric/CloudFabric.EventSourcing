using System.Text.RegularExpressions;

namespace CloudFabric.Projections.ElasticSearch.Extensions;

public static partial class StringExtensions
{
    //TODO: define all special characters list
    //private const string ESCAPE_LIST = "[\\+\\-\\=\\&\\|\\!\\(\\)\\{\\}\\[\\]\\^\\\"\\~\\*\\<\\>\\?\\:\\\\\\/]";
    
    private const string ESCAPE_LIST = "[\\\\\\/]";
    internal static string EscapeElasticUnsupportedCharacters(this string instance)
    {
        return Regex.Replace(instance, ESCAPE_LIST, m => $@"\{m.Value}", RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }
}
