using DistributedCacheLibrary;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // load config


Console.WriteLine("CACHE Client:");
Console.WriteLine("+-------------------------------------------+");
Console.WriteLine("get [key] | set [key] [value] [seconds*] | status | exit");
Console.WriteLine("+-------------------------------------------+");


const int ReadBufferSize = 1024 * 2;
int Port = Convert.ToInt32(config["ServerPort"]);
string IP = config["ServerIP"];
IPAddress IPaddr = IPAddress.Parse(IP);
ConcurrentQueue<TcpClient> _connectionPool = new();
object _lock = new();


var tasks = new List<Task>();
const int BatchSize = 100;
int MaxParallelism = Environment.ProcessorCount * 2;

//for (int batch = 0; batch < 1_000_000; batch += BatchSize)
//{
//    if (tasks.Count >= MaxParallelism)
//    {
//        await Task.WhenAny(tasks);
//        tasks.RemoveAll(t => t.IsCompleted);
//    }

//    var batchTask = Task.Run(() => ProcessBatch(batch, Math.Min(BatchSize, 1_000_000 - batch)));
//    tasks.Add(batchTask);
//}



while (true)
{
    Console.Write(">>  ");
    var input = Console.ReadLine();
    for (int i = 0; i < 1_000_00; i++)
    {
        await Task.Delay(10);
        input = $"SET k{i} {i}";
        Console.WriteLine(input);
        string command = input.Split(" ")[0].Trim().ToLower();
        switch (command)
        {

            case "get":
                ProcessGetCommand(input);
                break;
            case "set":
                ProcessSetCommand(input);
                break;
            case "status":
                //  status of node connected
                Console.WriteLine("To be implemented");
                break;
            case "exit":
                System.Environment.Exit(0);
                break;
            default:
                Console.WriteLine("Unknown command");
                break;
        }
    }
}




void ProcessSetCommand(string command)
{
    try
    {
        var bytes = RESP.Serialize(command);
        var response = Send(bytes);

        if (response.Length > 0)
        {
            Print(RESP.Deserialize(response));
        }
        else
        {
            Console.WriteLine("Empty Response");
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine("!!!Exception: " + ex.Message);
    }
}


void ProcessGetCommand(string command)
{
    try
    {
        var bytes = RESP.Serialize(command);
        var response = Send(bytes);
        if (response.Length > 0)
        {
            Print(RESP.Deserialize(response));
        }
        else
        {
            Console.WriteLine("Empty Response");
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine("!!!Exception: " + ex.Message);
    }
    
}

byte[] Send(byte[] bytes)
{
    TcpClient client = null;

    try
    {
        if (!_connectionPool.TryDequeue(out client) || !client.Connected)
            client = new TcpClient(IP, Port);

        using (var stream = client.GetStream())
        {
            stream.Write(bytes, 0, bytes.Length);
            var buffer = new byte[ReadBufferSize];

            int bytesReadSize = stream.Read(buffer, 0, ReadBufferSize);
            Console.WriteLine("Response Received");
            stream.Close();

            if (client.Connected) // preserve connection
                _connectionPool.Enqueue(client);

            return buffer.Take(bytesReadSize).ToArray(); // take only bytes read, skip empty or 0 buffer


        }
    }
    catch
    {
        client?.Close();
        throw;
    }

}

void Print(List<object> items)
{
    foreach(var item in items)
    {
        if (item != null)
            Console.Write(item + " ");
        else
            Console.Write("null" + " "); // foribly printing null
    }
    Console.WriteLine();
}

//void ProcessBatch(int startIndex, int Count)
//{
//    using var client = new TcpClient(IP, Port);
//    using var stream = client.GetStream();

//    for (int i = 0; i < Count; i++)
//    {
//        var input = $"SET k{startIndex + i} {startIndex + i}";
//        var bytes = RESP.Serialize(input);

//        stream.Write(bytes, 0, bytes.Length);

//        var buffer = new byte[ReadBufferSize];
//        int bytesRead = stream.Read(buffer, 0, ReadBufferSize);
//        Console.WriteLine("Response Received");
//        var response = buffer.Take(bytesRead).ToArray();
//        if (response.Length > 0)
//        {
//            Print(RESP.Deserialize(response));
//        }
//        else
//        {
//            Console.WriteLine("Empty Response");

//        }
//    }
//    stream.Close();

//}