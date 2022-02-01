namespace AAM.Events;

public abstract class TimedEvent : EventBase
{
    public EventTime When;
}

public enum EventTime
{
    Now,
    AtEnd
}