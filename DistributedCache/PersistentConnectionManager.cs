using DistributedCacheLibrary;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DistributedCacheClient
{
    public class PersistentConnectionManager : IAsyncDisposable
    {
        private TcpClient _tcpClient = null; 
        private string _ip;
        private int _port;
        private int _buffersize;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private NetworkStream? _stream;
        private DateTime _lastActivity = DateTime.UtcNow;
        private bool _commandBatching;
        private int _batchSize;

        public PersistentConnectionManager(string ip, int port, int bufferSize)
        {
            _ip = ip;
            _port = port;
            _buffersize = bufferSize;
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            _commandBatching = Convert.ToBoolean(config["BatchCommands"]);
            _batchSize = Convert.ToInt32(config["BatchSize"]);
        }

        private async Task<bool> EnsureConnectionAsync()
        {
            
            await _connectionLock.WaitAsync();
            
            try
            {
                if(_tcpClient != null  && _tcpClient.Connected && _stream != null)
                {
                    return true;
                }

                _stream?.Close();
                _tcpClient?.Close();

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ip,_port);
                _stream = _tcpClient.GetStream();
                _tcpClient.ReceiveTimeout = 5000;
                _tcpClient.SendTimeout = 5000;

                Console.WriteLine($"Connected to {_ip}:{_port}");
                _lastActivity = DateTime.UtcNow;
                return true;
            }
            catch(Exception e)
            {
                Console.WriteLine($"Connection failed: {e.Message}");
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }

        }

        public async Task<byte[]> SendAsync(byte[] sendbytes)
        {
            if (!await EnsureConnectionAsync())
            {
                throw new Exception($"Could not Establish Connection with {_ip}:{_port}");
            }
            try
            {
                return await SendCommandAsync(sendbytes);

            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private async Task<byte[]> SendCommandAsync(byte[] sendbytes)
        {
            await _stream.WriteAsync(sendbytes, 0, sendbytes.Length);
            var buffer = new byte[_buffersize];
            int bytesReadSize = await _stream.ReadAsync(buffer, 0, _buffersize);
            Console.WriteLine("Response Received");
            return buffer.Take(bytesReadSize).ToArray(); // take only bytes read, skip empty or 0 buffer
        }

        public async Task<byte[]> SendBatchAsync(byte[] sendbytes)
        {

            if (!await EnsureConnectionAsync())
            {
                throw new Exception($"Could not Establish Connection with {_ip}:{_port}");
            }
            
            ConcurrentQueue<byte[]> commands = new();
            
            if(commands.Count == _batchSize)
                await _connectionLock.WaitAsync();

            commands.Enqueue(sendbytes);
            try 
             {
                var allCommands = new List<byte[]>();
                foreach (var command in commands)
                {
                    allCommands.Add(command);
                }

                await _stream!.WriteAsync(allCommands.SelectMany( a => a).ToArray());

               
                var buffer = new byte[_buffersize];
                int bytesRead = await _stream!.ReadAsync(buffer, 0, _buffersize);
                
                _lastActivity = DateTime.UtcNow;
                return buffer.Take(bytesRead).ToArray();
            }
            catch( Exception e)
            {
                throw e;
            }
            finally
            {
                _connectionLock.Release();

            }
        }

        public string GetConnectionStatus()
        {
            var connected = _tcpClient?.Connected == true;
            var timeSinceActivity = DateTime.UtcNow - _lastActivity;
            return $"Connected: {connected}, Last Activity: {timeSinceActivity.TotalSeconds:F1}s ago";
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionLock.WaitAsync();
            try
            {
                _stream?.Close();
                _tcpClient?.Close();
                Console.WriteLine("Connection closed");
            }
            finally
            {
                _connectionLock.Release();
                _connectionLock.Dispose();
            }
        }
    }
}
