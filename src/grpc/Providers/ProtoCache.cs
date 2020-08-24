using System;
using System.Threading.Tasks;
using Google.Protobuf;
using StackExchange.Redis;

namespace TracingDemo.GrpcService.Providers
{
    public interface IProtoCache
    {
        Task<TOut> GetProto<TOut>(string key) where TOut : IMessage, new();
        Task SetProto(string key, IMessage obj, TimeSpan expires);
    }

    sealed class RedisProtoCache : IProtoCache
    {
        public ConnectionMultiplexer Connection { get; }

        public RedisProtoCache(string connectionString)
        {
            Connection = ConnectionMultiplexer.Connect(connectionString);
        }

        public async Task<TOut> GetProto<TOut>(string key) where TOut : IMessage, new()
        {
            IDatabase cache = Connection.GetDatabase();

            var bytes = (byte[])await cache.StringGetAsync(key);
            if (bytes == null) return default;

            var obj = new TOut();
            obj.MergeFrom(bytes);

            return obj;
        }

        public async Task SetProto(string key, IMessage obj, TimeSpan expires)
        {
            IDatabase cache = Connection.GetDatabase();

            byte[] bytes = obj.ToByteArray();
            await cache.StringSetAsync(key, bytes, expires);
        }
    }
}
