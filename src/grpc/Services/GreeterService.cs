using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using TracingDemo.GrpcService.Providers;
using TracingDemo.Protobuf;

namespace TracingDemo.GrpcService.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        private readonly IGreetingRepository _repository;
        private readonly ActivitySource _activitySource;
        private readonly IProtoCache _cache;

        public GreeterService(ActivitySource activitySource, IProtoCache cache, IGreetingRepository repository, ILogger<GreeterService> logger)
        {
            _activitySource = activitySource;
            _cache = cache;
            _repository = repository;
            _logger = logger;
        }

        public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            // check inputs
            if (context.Deadline == DateTime.MaxValue) 
            {   // enforce deadline
                context.Status = new Status(StatusCode.Cancelled, "No Deadline Supplied");
                _logger.LogWarning("Caller '{Peer}' Omitted Deadline", context.Peer);
            }
            else if (string.IsNullOrWhiteSpace(request.Name))
            {   // require name
                context.Status = new Status(StatusCode.InvalidArgument, "'Name' is Required");
            }
            else if (request.Name.Equals("Mud", StringComparison.InvariantCultureIgnoreCase))
            {   // Does anyone else remember this song?
                context.Status = new Status(StatusCode.InvalidArgument, "Your name is not Mud");
            }

            // are we OK?
            if (context.Status.StatusCode != StatusCode.OK)
            {
                // record validation failure as trace event
                Activity.Current?.AddEvent(new ActivityEvent(context.Status.ToString()));
                
                if (context.Status.StatusCode == StatusCode.InvalidArgument)
                    _logger.LogWarning("Input Validation Failed: {@Request}", request);

                return null;
            }

            // try to get our reply from the cache
            using (Activity tryCacheActivity = _activitySource.StartActivity("Check-Cache"))
            {
                tryCacheActivity?.AddTag("cache.searchKey", request.Name);
                HelloReply cacheHit = await _cache.GetProto<HelloReply>(request.Name);
                if (cacheHit != null)
                {
                    tryCacheActivity?.AddTag("cache.result", "hit");
                    return cacheHit;
                }
                
                tryCacheActivity?.AddTag("cache.result", "miss");
            }

            // build new record add to database
            var record = new GreetingRecord { Name = request.Name, Utc = DateTime.UtcNow };

            using (Activity insertActivity = _activitySource.StartActivity("Insert-Sql-Record"))
            {
                int recId = await _repository.InsertGreeting(record, context.CancellationToken);
                insertActivity?.AddTag("record.id", recId.ToString());

                _logger.LogInformation("Inserted new greeting record {@record}", record);
            }
            
            // create reply and add to cache
            var reply = new HelloReply { Message = $"{record.Name} said hello at {record.Utc:HH:mm:ss.fff} UTC." };

            using (_activitySource.StartActivity("Add-To-Cache"))
                await _cache.SetProto(request.Name, reply, TimeSpan.FromMinutes(1));

            // OK
            return reply;
        }
    }
}
