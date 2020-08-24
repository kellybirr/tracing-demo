using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Microsoft.Extensions.Logging;

namespace TracingDemo.GrpcService.Services
{
    public class HealthCheckService : Health.HealthBase
    {
        private readonly ILogger _logger;

        public HealthCheckService(ILogger<HealthCheckService> logger)
        {
            _logger = logger;
        }

        public override Task<HealthCheckResponse> Check(HealthCheckRequest request, ServerCallContext context)
        {
            var result = new HealthCheckResponse
            {
                Status = HealthCheckResponse.Types.ServingStatus.Serving
            };

            _logger.LogDebug("Health Check Result: {@Result}", result);

            return Task.FromResult( result );
        }
    }
}
