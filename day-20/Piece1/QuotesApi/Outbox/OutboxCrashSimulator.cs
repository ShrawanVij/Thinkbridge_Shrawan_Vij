namespace QuotesApi.Outbox;

// Test-only hook: lets an HTTP call arm a one-shot crash inside the relay,
// right after a publish succeeds but before the outbox row is marked sent —
// the exact window a real process crash/restart would land in.
public class OutboxCrashSimulator
{
    private volatile bool _armed;

    public void ArmCrashAfterNextPublish() => _armed = true;

    public bool TryConsumeCrash()
    {
        if (!_armed)
        {
            return false;
        }

        _armed = false;
        return true;
    }
}
