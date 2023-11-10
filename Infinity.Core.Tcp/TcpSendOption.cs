using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infinity.Core.Tcp
{
    /// <summary>
    ///     Extra public states for SendOption enumeration when using TCP.
    /// </summary>
    public enum TcpSendOption : byte
    {
        /// <summary>
        ///     Hello message for initiating communication.
        /// </summary>
        Connect = 8,

        /// <summary>
        ///     Message for discontinuing communication.
        /// </summary>
        Disconnect = 9,

        MessageUnordered,
        MessageOrdered,
    }
}
