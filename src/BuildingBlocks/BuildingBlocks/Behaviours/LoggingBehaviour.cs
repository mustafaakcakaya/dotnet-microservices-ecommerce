using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Behaviours;

public class LoggingBehaviour<TRequest, TResponse>
(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, IRequest<TResponse>
    where TResponse : notnull

{
    private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromSeconds(3);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Only the contract names are logged, never the request itself: commands
        // such as Payment's TokenizeCardCommand carry the card number and CVV,
        // and serializing the whole object would write them to the logs in clear
        // text - undoing the care taken to keep them inside the Payment service.
        logger.LogInformation("[START] Handle request={Request} - Response={Response}",
            typeof(TRequest).Name, typeof(TResponse).Name);

        var timer = Stopwatch.StartNew();

        var response = await next();

        timer.Stop();
        var timeTaken = timer.Elapsed;
        if (timeTaken > SlowRequestThreshold)
            logger.LogWarning("[PERFORMANCE] The request={Request} took {TimeTaken:0.###} seconds",
                typeof(TRequest).Name, timeTaken.TotalSeconds);

        logger.LogInformation("[END] Handled request={Request} with Response={Response}",
            typeof(TRequest).Name, typeof(TResponse).Name);

        return response;
    }
}
