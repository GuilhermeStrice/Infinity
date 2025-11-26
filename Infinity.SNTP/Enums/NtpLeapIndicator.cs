namespace Infinity.SNTP
{
    public enum NtpLeapIndicator : byte
    {
        NoWarning,
        LastMinuteHas61Seconds,
        LastMinuteHas59Seconds,
        AlarmCondition
    }
}