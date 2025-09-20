using DistributedCacheClient;
using DistributedCacheLibrary;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using static System.Runtime.InteropServices.JavaScript.JSType;
var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // load config


Console.WriteLine("CACHE Client:");
Console.WriteLine("+-------------------------------------------+");
Console.WriteLine("get [key] | set [key] [value] [seconds*] | batch-set [count] | batch-get [count] | status | exit");
Console.WriteLine("+-------------------------------------------+");


const int ReadBufferSize = 4096 * 2;
int Port = Convert.ToInt32(config["ServerPort"]);
string IP = config["ServerIP"];
IPAddress IPaddr = IPAddress.Parse(IP);
ConcurrentQueue<TcpClient> _connectionPool = new();
object _lock = new();
var connection = new PersistentConnectionManager(IP, Port, ReadBufferSize);

var tasks = new List<Task>();
int _batchSize = Convert.ToInt32(config["BatchSize"]);




while (true)
{
    Console.Write(">>  ");
    var input = Console.ReadLine();
    
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
            case "batch-set":
                ProcessBatchSet(input);
                break;
            case "batch-get":
                ProcessBatchGet(input);
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

void ProcessBatchGet(string input)
{
    throw new NotImplementedException();
}

void ProcessBatchSet(string input)
{

    try
    {
        List<string> commands = new List<string>();
        int count = Convert.ToInt32(input.Split(' ')[1]);
        int processed = 0;
        for (int i = 1; i <= count; i++)
        {
            string command = $"set k{i} {i}";

            int currentbatchSize = Math.Min(_batchSize, count - processed);
            
            if (commands.Count < currentbatchSize-1)
            {
                   commands.Add(command);
                   continue;
            }
            commands.Add(command);
                
            processed += currentbatchSize;
            var bytes = RESP.SerializeBatch(commands.ToArray());
            var response = connection.SendBatchAsync(bytes).Result;

            commands.Clear();
            if (response.Length > 0)
            {
                var output = RESP.DeserializeBulkCommands(response);
                foreach(var o in output)
                {
                    Console.WriteLine(string.Join(" ", o));
                }
            }
            else
            {
                Console.WriteLine("Empty Response");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("!!!Exception: " + ex.Message);
    }
}

void ProcessSetCommand(string command)
{
    try
    {
        var bytes = RESP.SerializeBatch(new[] { command });
        var response =  connection.SendAsync(bytes).Result;

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
        var bytes = RESP.SerializeBatch(new [] { command});
        var response = connection.SendAsync(bytes).Result;
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