using Infinity.Core;

namespace Infinity.SNTP
{
    public class Pools
    {
        public static ObjectPool<NtpResponse> NtpResponsePool = new ObjectPool<NtpResponse>(() => new NtpResponse());
        public static ObjectPool<NtpRequest> NtpRequestPool = new ObjectPool<NtpRequest>(() => new NtpRequest());
    }
}
