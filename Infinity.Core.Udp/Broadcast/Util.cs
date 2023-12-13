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

            var validInterfaces = new List<NetworkInterface>(nics.Length);

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
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null)
                    {
                        // Yes it does, add this network interface.
                        validInterfaces.Add(adapter);
                        break;
                    }
                }
            }

            if (validInterfaces.Count == 0 && best != null)
            {
                validInterfaces.Add(best);
            }

            return validInterfaces;
        }

        public static ICollection<UnicastIPAddressInformation> GetAddressesFromNetworkInterfaces(AddressFamily addressFamily)
        {
            var unicastAddresses = new List<UnicastIPAddressInformation>();

            foreach (NetworkInterface adapter in GetValidNetworkInterfaces())
            {
                IPInterfaceProperties properties = adapter.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress != null && unicastAddress.Address != null
                        && unicastAddress.Address.AddressFamily == addressFamily)
                    {
                        unicastAddresses.Add(unicastAddress);
                        break;
                    }
                }
            }

            return unicastAddresses;
        }

        public static IPAddress? GetBroadcastAddress(UnicastIPAddressInformation unicastAddress)
        {
            if (unicastAddress != null && unicastAddress.Address != null
                && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
            {
                var mask = unicastAddress.IPv4Mask;

                byte[] ipAdressBytes = unicastAddress.Address.GetAddressBytes();
                byte[] subnetMaskBytes = mask.GetAddressBytes();

                if (ipAdressBytes.Length != subnetMaskBytes.Length)
                {
                    throw new ArgumentException("Lengths of IP address and subnet mask do not match.");
                }

                byte[] broadcastAddress = new byte[ipAdressBytes.Length];

                for (int i = 0; i < broadcastAddress.Length; i++)
                {
                    broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
                }

                return new IPAddress(broadcastAddress);
            }

            return null;
        }
    }
}
