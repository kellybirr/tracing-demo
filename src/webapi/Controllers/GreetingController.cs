using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TracingDemo.Protobuf;
using TracingDemo.WebApi.Models;

namespace TracingDemo.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GreetingController : ControllerBase
    {
        private readonly ILogger<GreetingController> _logger;
        private readonly Greeter.GreeterClient _greeterClient;
        private readonly ActivitySource _activitySource;

        public GreetingController(ActivitySource activitySource, Greeter.GreeterClient greeterClient, ILogger<GreetingController> logger)
        {
            _activitySource = activitySource;
            _greeterClient = greeterClient;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(typeof(GreetingOut), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> Post([FromBody]GreetingIn input)
        {
            _logger.LogTrace("Request Input: {@Input}", input);

            // this silly logic is so we can show a trace that never went past this point
            if (input.Name == "string")
            {
                _logger.LogWarning("Tricksy hackerses, we hates them!");
                
                // add a tag to the current activity, so we know why the request died
                Activity.Current?.AddTag("illegal.input", input.Name);

                // I realize this is mixing movie quotes
                return Ok( 
                    new GreetingOut {Message = "You're trying to trick me into giving something away...  It won't work!"} 
                    );
            }

            // start a new child activity since we're about to call the service (for request/reply capture)
            using Activity callRpcActivity = _activitySource.StartActivity("Call-Grpc-Service");
            try
            {
                // capture rpc request parameters
                var rpcRequest = new HelloRequest {Name = input.Name};
                callRpcActivity?.AddTag("rpc.request", rpcRequest.ToString());

                // send greeting via gRPC
                HelloReply reply = await _greeterClient.SayHelloAsync(
                    request: rpcRequest,
                    deadline: DateTime.UtcNow.AddSeconds(8)
                );

                _logger.LogDebug("Service replied with {@reply}", reply);

                // capture reply - not usually appropriate in production
                callRpcActivity?.AddTag("rpc.reply", reply.ToString());

                // all good
                return Ok(
                    new GreetingOut {Message = reply.Message}
                );
            }
            catch (RpcException ex)
            {
                // record error information in the trace
                callRpcActivity?.AddTag("error.status", ex.StatusCode.ToString());
                callRpcActivity?.AddEvent(new ActivityEvent(ex.Message));

                // not really the right thing to do, but fine for this sample
                return BadRequest();
            }
        }
    }
}
