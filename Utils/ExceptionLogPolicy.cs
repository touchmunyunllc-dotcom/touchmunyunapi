namespace ECommerce.Utils;

/// <summary>
/// Determines whether an exception represents an important system failure worth persisting to the database.
/// Expected client/business errors and cancelled requests are excluded.
/// </summary>
public static class ExceptionLogPolicy
{
    public static bool IsImportant(Exception exception)
    {
        if (exception is BusinessException)
        {
            return false;
        }

        if (IsCancelledRequest(exception))
        {
            return false;
        }

        return exception switch
        {
            UnauthorizedAccessException => false,
            ArgumentException => false,
            KeyNotFoundException => false,
            InvalidOperationException => false,
            _ => true
        };
    }

    private static bool IsCancelledRequest(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is OperationCanceledException or TaskCanceledException)
            {
                return true;
            }
        }

        return false;
    }
}
