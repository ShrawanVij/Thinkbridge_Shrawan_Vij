namespace QuotesApi.Resilience;

// Test-only fault injection: a controllable stand-in for "an outbound
// dependency that's currently failing." Toggled via HTTP so the resilience
// pipeline (retry/circuit breaker/timeout/bulkhead) can be exercised
// deterministically instead of hoping a real third-party service misbehaves
// on cue.
public class FlakyDependencyState
{
    private volatile bool _forceFailure;

    public bool ForceFailure => _forceFailure;

    public void Fail() => _forceFailure = true;

    public void Recover() => _forceFailure = false;
}
