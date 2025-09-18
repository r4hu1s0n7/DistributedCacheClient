using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DistributedCacheClient
{
    
    public class CacheException : Exception
    {
        public string Message { get; private set; }
        public CacheException(string Message)
        {
            this.Message = $"-ERR {Message}";
        }
    }
}
