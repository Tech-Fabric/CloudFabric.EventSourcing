namespace CloudFabric.EventSourcing.Tests;

public static class TestHelpers
{
    public static async Task<T?> RepeatUntilNotNull<T>(Func<Task<T?>> lambdaToRepeat, TimeSpan timeout, int millisecondsRetryDelay = 500)
    {
        var startTime = DateTime.UtcNow;
        var result = default(T);

        while (result == null || DateTime.UtcNow - startTime > timeout)
        {
            result = await lambdaToRepeat();
            if (result != null)
            {
                return result;
            }

            await Task.Delay(millisecondsRetryDelay);
        }

        throw new Exception("Function failed to return non-null value within timeout");
    }
    
    /// <summary>
    /// Calls `functionToRepeat` and passes result value to `conditionCheckFunction` until it returns true.
    /// Returns result of `functionToRepeat` when `conditionCheckFunction` returns true or null when timeout passes.
    /// </summary>
    /// <param name="functionToRepeat"></param>
    /// <param name="conditionCheckFunction"></param>
    /// <param name="timeout"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static async Task<T?> RepeatUntil<T>(Func<Task<T?>> functionToRepeat, Func<T?, bool> conditionCheckFunction, TimeSpan timeout, int millisecondsRetryDelay = 500)
    {
        var startTime = DateTime.UtcNow;
        var result = default(T);
        bool? conditionResult = null;

        while (DateTime.UtcNow - startTime < timeout)
        {
            result = await functionToRepeat();

            conditionResult = conditionCheckFunction(result);
            if (conditionResult == true)
            {
                return result;
            }
            
            await Task.Delay(millisecondsRetryDelay);
        }

        throw new Exception("Function failed to return non-null value within timeout");
    }
}