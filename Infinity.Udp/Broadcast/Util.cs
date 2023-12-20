using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace Infinity.Core.Udp.Broadcast
{
    internal static class Util
    {
        public static IList<NetworkInterface> GetValidNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            if (nics == null || nics.Length < 1)
                return new NetworkInterface[0];

            var valid_interfaces = new List<NetworkInterface>(nics.Length);

            NetworkInterface best = null;
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || adapter.NetworkInterfaceType == NetworkInterfaceType.Unknown)
                {
                    continue;
                }

                if (!adapter.Supports(NetworkInterfaceComponent.IPv4)
                    && !adapter.Supports(NetworkInterfaceComponent.IPv6))
                {
                    continue;
                }

                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (best == null)
                {
                    best = adapter;
                }

                // make sure this adapter has any ip addresses
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicast_address in properties.UnicastAddresses)
                {
                    if (unicast_address != null && unicast_address.Address != null)
                    {
                        // Yes it does, add this network interface.
                        valid_interfaces.Add(adapter);
                        break;
                    }
                }
            }

            if (valid_interfaces.Count == 0 && best != null)
            {
                valid_interfaces.Add(best);
            }

            return valid_interfaces;
        }

        public static ICollection<UnicastIPAddressInformation> GetAddressesFromNetworkInterfaces(AddressFamily _address_family)
        {
            var unicast_ddresses = new List<UnicastIPAddressInformation>();

            foreach (NetworkInterface adapter in GetValidNetworkInterfaces())
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicast_address in properties.UnicastAddresses)
                {
                    if (unicast_address != null && unicast_address.Address != null
                        && unicast_address.Address.AddressFamily == _address_family)
                    {
                        unicast_ddresses.Add(unicast_address);
                        break;
                    }
                }
            }

            return unicast_ddresses;
        }

        public static IPAddress? GetBroadcastAddress(UnicastIPAddressInformation _unicast_address)
        {
            if (_unicast_address != null && _unicast_address.Address != null
                && _unicast_address.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                var mask = _unicast_address.IPv4Mask;

                byte[] ip_adress_bytes = _unicast_address.Address.GetAddressBytes();
                byte[] subnet_mask_bytes = mask.GetAddressBytes();

                if (ip_adress_bytes.Length != subnet_mask_bytes.Length)
                {
                    throw new ArgumentException("Lengths of IP address and subnet mask do not match.");
                }

                byte[] broadcast_address = new byte[ip_adress_bytes.Length];

                for (int i = 0; i < broadcast_address.Length; i++)
                {
                    broadcast_address[i] = (byte)(ip_adress_bytes[i] | (subnet_mask_bytes[i] ^ 255));
                }

                return new IPAddress(broadcast_address);
            }

            return null;
        }
    }
}
