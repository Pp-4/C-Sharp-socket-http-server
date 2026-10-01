using System.Text;

namespace Kestrel;

public enum Connection
{
    DontSpecify,
    KeepAlive,
    Close,
}
public enum HttpCodes
{
    _200 = 200,
    _403 = 403,
    _404 = 404,
    _413 = 413,
    _431 = 431,
}
public static class HttpMessages
{
    readonly static Dictionary<HttpCodes, string> CodeDescriptions = new()
        {
            { HttpCodes._200, "OK" },
            { HttpCodes._403, "Forbidden"},
            { HttpCodes._404, "Not Found"},
            { HttpCodes._413, "Content Too Large"},
            { HttpCodes._431, "Request Header Fields Too Large"},
        };
    public static byte[] BuildResponse(HttpCodes code, Connection connection = Connection.DontSpecify, string otherHeaders = "", string httpVersion = "HTTP/1.1")
    {
        string headers = $"{httpVersion} {(int)code} {CodeDescriptions[code]}\r\n";
        if (otherHeaders != "")
        {
            headers += otherHeaders;
            if (!otherHeaders.EndsWith("\r\n")) headers += "\r\n";
        }
        switch (connection)
        {
            case Connection.KeepAlive:
                headers += "Connection: keep-alive\r\n";

                break;
            case Connection.Close:
                headers += "Connection: close\r\n";
                break;
        }
        headers += "\r\n";
        return Encoding.ASCII.GetBytes(headers);
    }
}