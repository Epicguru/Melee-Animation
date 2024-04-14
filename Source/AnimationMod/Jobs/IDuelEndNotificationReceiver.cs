namespace AM.Jobs;

public interface IDuelEndNotificationReceiver
{
    void Notify_OnDuelEnd(bool didWin);
}
