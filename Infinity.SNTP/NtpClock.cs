namespace Infinity.SNTP
{
    public class NtpClock
    {
        private NtpResponse response;

        public NtpClock(NtpResponse _response)
        {
            response = _response;
        }

        ~NtpClock()
        {
            response.Recycle();
        }

        public bool Synchronized
        {
            get
            {
                if (response.LeapIndicator == NtpLeapIndicator.AlarmCondition ||
                    response.Stratum == 0 ||
                    response.RootDelay.TotalSeconds > 1 ||
                    response.RootDispersion.TotalSeconds > 1 ||
                    RoundTripTime.TotalSeconds > 1)
                    return false;

                return true;
            }
        }

        public TimeSpan CorrectionOffset
        {
            get => 0.5 * (response.ReceiveTimestamp - response.OriginTimestamp - (response.DestinationTimestamp - response.TransmitTimestamp));

        }

        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow + CorrectionOffset;

        public DateTimeOffset Now => DateTimeOffset.Now + CorrectionOffset;

        public TimeSpan RoundTripTime
        {
            get => (response.ReceiveTimestamp - response.OriginTimestamp) + (response.DestinationTimestamp - response.TransmitTimestamp);
        }

        public static readonly NtpClock LocalFallback;

        static NtpClock()
        {
            var time = DateTime.UtcNow;
            LocalFallback = new NtpClock(new NtpResponse
            {
                LeapIndicator = NtpLeapIndicator.AlarmCondition,
                OriginTimestamp = time,
                ReceiveTimestamp = time,
                TransmitTimestamp = time,
                DestinationTimestamp = time,
            });
        }
    }
}