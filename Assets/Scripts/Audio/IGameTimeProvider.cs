using System;

public interface IGameTimeProvider
{
    int CurrentHour { get; }
    event Action TimeChanged;
}
