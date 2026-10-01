using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Kestrel;

class Program
{
    // i gave up here and asked ai how to get the correct path for html dir when debugging in vscode
    //TODO read and understand this later
    static string SourceDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

#if DEBUG
    static string root = Path.Combine(SourceDir(), "html");                    // files from source
#else
    static string root = Path.Combine(AppContext.BaseDirectory, "html");       // copy made for prod
#endif

    static async Task Main(string[] args)
    {

        if (args.Length > 0) root = args[0];


        Console.WriteLine("Starting server!");
        await Task.WhenAll(
            Listen(1234, WebpageHttp),
            Listen(1235, WebpageHttps)
        );

    }
    async static Task Listen(int port, Func<Socket, Task> handleClient)
    {
        using Socket listener = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint endPoint = new(IPAddress.Any, port);
        listener.Bind(endPoint);
        listener.Listen();
        while (true)
        {
            Socket client = await listener.AcceptAsync();
            _ = Task.Run(async () =>
            {
                using (client)
                {
                    try
                    {
                        await handleClient(client);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Client error: {client.RemoteEndPoint}, {ex.Message}");
                    }
                    client.Shutdown(SocketShutdown.Send);
                }
            });
        }

    }
    async static Task WebpageHttp(Socket client)
    { //TODO, separate between POST and GET, handle both
        byte[]? buffer = new byte[256];
        MemoryStream wholeRequest = new();
        bool firstChunk = true;
        int dataLength;
        int maxRequestSize = 8192;
        while (true)
        {
            dataLength = await client.ReceiveAsync(buffer, SocketFlags.None);
            if (dataLength == 0) return;
            if (firstChunk)
            {
                if (buffer[0] == 0x16)// in case of client using https on http port
                    return;
                Console.WriteLine(client.RemoteEndPoint);
                firstChunk = false;
            }
            int begin = (int)Math.Max(0, wholeRequest.Length - 3);
            wholeRequest.Write(buffer, 0, dataLength);
            Console.Write(Encoding.UTF8.GetString(buffer, 0, dataLength));
            if (wholeRequest.Length > maxRequestSize)//request too large, closing connection
            {
                await client.SendAsync(HttpMessages.BuildResponse(HttpCodes._413, Connection.Close), SocketFlags.None);
                return;
            }
            var span = wholeRequest.GetBuffer().AsSpan(begin, (int)wholeRequest.Length - begin);
            if (span.IndexOf("\r\n\r\n"u8) > -1)//end of headers
            {
                break;
            }
        }
        string resourceURI = GetURIFromHeaders(Encoding.UTF8.GetString(wholeRequest.GetBuffer().AsSpan(0, wholeRequest.GetBuffer().IndexOf("\r\n"u8))));
        Console.WriteLine("end of headers");
        bool allowed = VerifyURI(resourceURI);
        byte[] body = [];
        byte[] headers;
        if (allowed)
        {
            body = GetFileFromURI(resourceURI);
            if (body.Length == 0)
                headers = HttpMessages.BuildResponse(HttpCodes._404, Connection.Close);
            else
                headers = Create200HttpHeader(body.Length);
        }
        else
        {
            headers = HttpMessages.BuildResponse(HttpCodes._403, Connection.Close);
        }

        await client.SendAsync(headers, SocketFlags.None);
        if (body.Length > 0)
            await client.SendAsync(body, SocketFlags.None);

        //client.Shutdown(SocketShutdown.Send); shutdown should be handled by whomever called this function
    }
    private static string GetURIFromHeaders(string headers)
    {
        string[] a = headers.Split(' ');
        if (a.Length < 2) return "404.html";
        if(a[1][0] == '/') a[1] = a[1][1..^0];
        if (a[1] == "") return "index.html";
        return a[1];
    }

    private static bool VerifyURI(string resourceURI)
    { //TODO - verify if URI is a correct one
        return true;
        //throw new NotImplementedException();
    }

    //important! verify uri BEFORE passing it to this function, as it does not check if the uri is correct and points to allowed resource
    private static byte[] GetFileFromURI(string resourceURI = "404.html")
    {
        string path = Path.Combine(root, resourceURI);
        try
        {
            var resource = File.ReadAllBytes(path);
            return resource;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            try
            {
                var notFound = File.ReadAllBytes(Path.Combine(root, "404.html"));
                return notFound;
            }
            catch
            {
                return [];
            }
        }
    }

    static async Task WebpageHttps(Socket client)
    {//tls handshake, i don't want to deal with it, http only
        await Task.CompletedTask;
    }
    static byte[] Create200HttpHeader(int contentLength)
    {
        string headers =
        "Content-Type: text/html; charset=utf-8\r\n" +
        $"Content-Length: {contentLength}\r\n";
        return HttpMessages.BuildResponse(HttpCodes._200, Connection.Close, headers);
    }
}