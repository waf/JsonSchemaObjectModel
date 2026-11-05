namespace JsonSchemaObjectModel;

/// <summary>
/// The format names that draft 2020-12 defines for the <c>format</c> keyword.
/// </summary>
public static class Formats
{
    /// <summary>An RFC 3339 date-time.</summary>
    public const string DateTime = "date-time";

    /// <summary>An RFC 3339 full-date.</summary>
    public const string Date = "date";

    /// <summary>An RFC 3339 full-time.</summary>
    public const string Time = "time";

    /// <summary>An RFC 3339 duration.</summary>
    public const string Duration = "duration";

    /// <summary>An RFC 5321 mailbox.</summary>
    public const string Email = "email";

    /// <summary>An RFC 6531 mailbox.</summary>
    public const string IdnEmail = "idn-email";

    /// <summary>An RFC 1123 host name.</summary>
    public const string Hostname = "hostname";

    /// <summary>An RFC 5890 internationalized host name.</summary>
    public const string IdnHostname = "idn-hostname";

    /// <summary>An IPv4 address.</summary>
    public const string Ipv4 = "ipv4";

    /// <summary>An IPv6 address.</summary>
    public const string Ipv6 = "ipv6";

    /// <summary>An RFC 3986 URI.</summary>
    public const string Uri = "uri";

    /// <summary>An RFC 3986 URI reference.</summary>
    public const string UriReference = "uri-reference";

    /// <summary>An RFC 3987 IRI.</summary>
    public const string Iri = "iri";

    /// <summary>An RFC 3987 IRI reference.</summary>
    public const string IriReference = "iri-reference";

    /// <summary>An RFC 4122 UUID.</summary>
    public const string Uuid = "uuid";

    /// <summary>An RFC 6570 URI template.</summary>
    public const string UriTemplate = "uri-template";

    /// <summary>An RFC 6901 JSON Pointer.</summary>
    public const string JsonPointer = "json-pointer";

    /// <summary>A relative JSON Pointer.</summary>
    public const string RelativeJsonPointer = "relative-json-pointer";

    /// <summary>An ECMA-262 regular expression.</summary>
    public const string Regex = "regex";
}
