using System.Runtime.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Access control mode of operation.
    /// </summary>
    public enum AccessControlMode
    {
        /// <summary>
        /// Permit requests from any endpoint by default.
        /// </summary>
        [EnumMember(Value = "DefaultPermit")]
        DefaultPermit,
        /// <summary>
        /// Deny requests from any endpoint by default.
        /// </summary>
        [EnumMember(Value = "DefaultDeny")]
        DefaultDeny
    }
}
