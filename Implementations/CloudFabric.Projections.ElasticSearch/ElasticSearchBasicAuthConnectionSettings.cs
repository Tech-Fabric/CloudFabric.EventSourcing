namespace CloudFabric.Projections.ElasticSearch;

public record ElasticSearchBasicAuthConnectionSettings(string Uri, string Username, string Password, string CertificateThumbprint)
{
    public readonly string Uri = Uri;
    public readonly string Username = Username;
    public readonly string Password = Password;
    public readonly string CertificateThumbprint = CertificateThumbprint;
}