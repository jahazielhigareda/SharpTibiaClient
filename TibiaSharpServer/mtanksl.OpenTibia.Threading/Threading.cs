namespace mtanksl.OpenTibia.Threading;

/// <summary>
/// A simple thread-safe message queue / event loop used by the server's
/// main game thread. Callers post <see cref="Action"/> delegates; the game
/// loop drains and executes them on its own thread to avoid race conditions.
/// </summary>
public sealed class GameScheduler : IDisposable
{
    private readonly Queue<Action>       _queue  = new();
    private readonly SemaphoreSlim       _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread              _thread;

    public GameScheduler()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "GameScheduler" };
        _thread.Start();
    }

    /// <summary>
    /// Post an action to be executed on the game thread.
    /// Thread-safe; may be called from any thread.
    /// </summary>
    public void Post(Action action)
    {
        lock (_queue)
            _queue.Enqueue(action);
        _signal.Release();
    }

    private void Run()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _signal.Wait(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Action? action;
            lock (_queue)
                action = _queue.Count > 0 ? _queue.Dequeue() : null;

            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[GameScheduler] Unhandled exception: {ex}");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _signal.Release();   // unblock Wait() if sleeping
        // Wait up to 5 s for the scheduler thread to finish its current action.
        // If it doesn't exit, we abandon it (it is a background thread and will
        // not prevent process shutdown).
        _thread.Join(TimeSpan.FromSeconds(5));
        _cts.Dispose();
        _signal.Dispose();
    }
}
