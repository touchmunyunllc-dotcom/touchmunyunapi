using System.Collections.Concurrent;

namespace ECommerce.Services;

public class NotificationQueueService : INotificationQueueService
{
    private readonly ConcurrentQueue<OrderStatusNotificationRequest> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public Task EnqueueOrderStatusNotificationAsync(OrderStatusNotificationRequest request)
    {
        _queue.Enqueue(request);
        _signal.Release();
        return Task.CompletedTask;
    }

    public async Task<OrderStatusNotificationRequest?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        
        if (_queue.TryDequeue(out var request))
        {
            return request;
        }
        
        return null;
    }
}

