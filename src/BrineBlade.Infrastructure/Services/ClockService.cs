using BrineBlade.Services.Abstractions;

namespace BrineBlade.Infrastructure.Services;

public sealed class ClockService : IClockService
{
    public void AdvanceMinutes(int minutes)
    {
        _ = minutes; // not used in console slice; keeps signature for API parity
    }
}

