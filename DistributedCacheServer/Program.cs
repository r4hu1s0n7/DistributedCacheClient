using DistributedCacheLibrary;
using System.Net;
using System.Net.Sockets;
using System.Text;


const int ReadBufferSize = 1024 * 2;
const int Port = 7001;
IPAddress IPaddr = IPAddress.Parse("127.0.0.1");
var listener = new TcpListener(IPaddr, Port);


listener.Start();


while (true)
{
    using (var client = listener.AcceptTcpClient())
    using (var stream = client.GetStream())
    {
        Console.WriteLine("Client connected.");
        var buffer = new byte[ReadBufferSize];

        int bytesReadSize = stream.Read(buffer,0,ReadBufferSize);
        Console.WriteLine("Command Received");

        var command = RESP.Deserialize(buffer.Take(bytesReadSize).ToArray());

        Console.WriteLine(command);

    }
}