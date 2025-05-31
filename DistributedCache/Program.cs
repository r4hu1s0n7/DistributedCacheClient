using DistributedCacheLibrary;
using Microsoft.Extensions.Configuration;
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

while (true)
{
    Console.Write(">>  ");
    var input = Console.ReadLine();
    string command = input.Split(" ")[0].Trim();
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
            break;
        case "exit":
            System.Environment.Exit(0);
            break;
        default:
            Console.WriteLine("Unknown command");
            break;
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
    using(var client = new TcpClient(IP,Port))
    using (var stream = client.GetStream())
    {
        stream.Write(bytes,0,bytes.Length);
        var buffer = new byte[ReadBufferSize];

        int bytesReadSize = stream.Read(buffer, 0, ReadBufferSize);
        Console.WriteLine("Response Received");
        stream.Close();
        return buffer.Take(bytesReadSize).ToArray(); // take only bytes read, skip empty or 0 buffer
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