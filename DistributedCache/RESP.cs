using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributedCacheLibrary
{
    public class RESP
    {
        private const string CrLf = "\r\n";

        public static byte[] Serialize(string input)
        {
            string[] inputItems = input.Split(" ");
            string command;
            if (inputItems.Length == 1)
            {
                command = SerializeString(inputItems[0]);
            }
            else
            {
                command = SerializeArray(inputItems);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(command);

            return bytes;
        }

        private static string SerializeString(string input)
        {
            return $"+{input}{CrLf}";
        }

        private static string SerializeInt(string input)
        {
            return $":{input}{CrLf}";
        }

        private static string SerializeBulkString(string input)
        {
            int len = Encoding.UTF8.GetByteCount(input);
            return $"${len}{CrLf}{input}{CrLf}";
        }

        private static string SerializeArray(string[] input)
        {
            int len = input.Length;
            StringBuilder sb = new StringBuilder();
            sb.Append($"*{len}{CrLf}");
            foreach (var item in input)
            {
                sb.Append(SerializeBulkString(item));
            }
            return sb.ToString();
        }


        public static List<object> Deserialize(byte[] bytes)
        {

            var obj = DeserializeObj(bytes);
            return obj;
        }

        private static List<object> DeserializeObj(byte[] bytes)
        {
            List<object> args = new List<object>();
            int pos = 0;
            string response = Encoding.UTF8.GetString(bytes);
            while (pos < response.Length)
            {
                char start = response[pos];
                if (start == '+')
                {
                    pos++;
                    object value = DeserializeString(pos, response);
                    if (value != null) args.Add(value);
                }
                else if (start == ':')
                {
                    pos++;
                    object value = DeserializeInt(pos, response);
                    if (value != null) args.Add(value);

                }
                else if (start == '$')
                {
                    pos++;
                    object value = DeserializeBulkString(ref pos, response);
                    if (value != null) args.Add(value);
                }
                else if (start == '*')
                {
                    // array
                    pos++;
                    List<string> values = DeserializeArray(ref pos, response);
                    args.AddRange(values);
                }else if (start == '_')
                {
                    args.Add(null);
                }
                
                else
                {
                    throw new Exception("Unknown RESP format");
                }

                

                return args;
            }
            throw new Exception("Unexpected end of data");
        }

        private static List<string> DeserializeArray(ref int pos, string response)
        {
            int end = response.IndexOf(CrLf, pos);
            string lengthStr = response.Substring(pos, end - pos);
            int length = int.Parse(lengthStr);
            List<string> values = new List<string>();
            pos += lengthStr.Length + CrLf.Length;
            for (int i = 0; i < length; i++)
            {
                // read each bulk string
                end = response.IndexOf(CrLf, pos);

                pos++; // increment pos becauuse want to skip prefix '$'
                string bulkLengthStr = response.Substring(pos, end - pos);
                int bulkLength = int.Parse(bulkLengthStr);
                pos += bulkLengthStr.Length + CrLf.Length;
                string value = response.Substring(pos, bulkLength);
                values.Add(value);
                pos += bulkLength + CrLf.Length;
            }

            return values;
        }

        private static object DeserializeBulkString(ref int pos, string response)
        {
            int end = response.IndexOf(CrLf, pos);
            string lengthStr = response.Substring(pos, end - pos);
            int length = int.Parse(lengthStr);
            pos += lengthStr.Length + CrLf.Length;
            string value = response.Substring(pos, length);
           
            return value;
        }

        private static object DeserializeString(int pos, string response)
        {
            int end = response.IndexOf(CrLf, pos);
            string value = response.Substring(pos, end - pos);
            return value;
        }

        private static object DeserializeInt(int pos, string response)
        {
            int end = response.IndexOf(CrLf, pos);
            string value = response.Substring(pos, end - pos);
            return int.Parse(value);
        }
    }
}
