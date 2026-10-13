using System;

// No bank SDK, URLs, card input or real payment credentials. State guards prevent double approval.
public sealed class DemoCheckoutSession
{
    public enum State { Review, Authorizing, Processing, Paid, Declined, Cancelled, Expired }
    public string OrderId { get; } = "DEMO-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
    public State Status { get; private set; } = State.Review;
    public double ExpiresAt { get; }
    public DemoCheckoutSession(double now) { ExpiresAt = now + 300; }
    public bool Expire(double now)
    {
        if ((Status == State.Review || Status == State.Authorizing) && now >= ExpiresAt) Status = State.Expired;
        return Status == State.Expired;
    }
    public bool Authorize(double now)
    { if (Expire(now) || Status != State.Review) return false; Status = State.Authorizing; return true; }
    public bool Approve(double now)
    { if (Expire(now) || Status != State.Authorizing) return false; Status = State.Processing; return true; }
    public bool Decline(double now)
    { if (Expire(now) || Status != State.Authorizing) return false; Status = State.Declined; return true; }
    public void Cancel()
    { if (Status == State.Review || Status == State.Authorizing) Status = State.Cancelled; }
    public void Complete(bool success)
    { if (Status == State.Processing) Status = success ? State.Paid : State.Declined; }
}
