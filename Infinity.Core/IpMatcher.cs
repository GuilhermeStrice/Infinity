using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Infinity.Core
{
    public class IpMatcher
    {
        ILogger logger = null;

        private string _Header = "[IpMatcher] ";
        private readonly object _AddressLock = new object();
        private List<Address> _Addresses = new List<Address>();
        private ConcurrentDictionary<string, DateTime> _Cache = new ConcurrentDictionary<string, DateTime>();
        private static readonly byte[] _ContiguousPatterns = { 0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 0xFF };

        public IpMatcher()
        {
        }

        public void Add(string _ip, string _netmask)
        {
            if (string.IsNullOrEmpty(_ip) || string.IsNullOrEmpty(_netmask) ||
                !IPAddress.TryParse(_ip, out IPAddress addr) || !IPAddress.TryParse(_netmask, out addr))
            {
                throw new ArgumentException("One or more arguments are invalid");
            }

            string baseAddress = GetBaseIpAddress(_ip, _netmask);

            if (Exists(baseAddress, _netmask))
            {
                return;
            }

            lock (_AddressLock)
            {
                _Addresses.Add(new Address(baseAddress, _netmask));
            }

            Log(baseAddress + " " + _netmask + " added");
            return;
        }

        /// <summary>
        /// Check if an entry exists in the match list.
        /// </summary>
        /// <param name="_ip">The IP address, i.e. 192.168.1.0.</param>
        /// <param name="_netmask">The netmask, i.e. 255.255.255.0.</param>
        /// <returns>True if entry exists.</returns>
        public bool Exists(string _ip, string _netmask)
        {
            if (string.IsNullOrEmpty(_ip) || string.IsNullOrEmpty(_netmask) ||
                !IPAddress.TryParse(_ip, out IPAddress addr) || !IPAddress.TryParse(_netmask, out addr))
            {
                throw new ArgumentException("One or more arguments are invalid");
            }

            if (_Cache.ContainsKey(_ip))
            {
                Log(_ip + " " + _netmask + " exists in cache");
                return true;
            }

            lock (_AddressLock)
            {
                Address curr = _Addresses.Where(d => d.Ip.Equals(_ip) && d.Netmask.Equals(_netmask)).FirstOrDefault();
                if (curr == default(Address))
                {
                    Log(_ip + " " + _netmask + " does not exist in address list");
                    return false;
                }
                else
                {
                    Log(_ip + " " + _netmask + " exists in address list");
                    return true;
                }
            }
        }

        /// <summary>
        /// Remove an entry from the match list.
        /// </summary>
        /// <param name="_ip">The IP address, i.e 192.168.1.0.</param>
        public void Remove(string _ip)
        {
            if (string.IsNullOrEmpty(_ip) ||
                !IPAddress.TryParse(_ip, out IPAddress addr))
            {
                throw new ArgumentException("One or more arguments are invalid");
            }

            _Cache.Remove(_ip, out DateTime _);
            Log(_ip + " removed from cache");

            lock (_AddressLock)
            {
                _Addresses = _Addresses.Where(d => !d.Ip.Equals(_ip)).ToList();
                Log(_ip + " removed from address list");
            }

            return;
        }

        /// <summary>
        /// Check if an IP address matches something in the match list.
        /// </summary>
        /// <param name="_ip">The IP address, i.e. 192.168.1.34.</param>
        /// <returns>True if a match is found.</returns>
        public bool MatchExists(string _ip)
        {
            if (string.IsNullOrEmpty(_ip) ||
                !IPAddress.TryParse(_ip, out IPAddress addr))
            {
                throw new ArgumentException("One or more arguments are invalid");
            }

            IPAddress parsed = IPAddress.Parse(_ip);

            if (_Cache.ContainsKey(_ip))
            {
                Log(_ip + " found in cache");
                return true;
            }

            List<Address> networks = new List<Address>();

            lock (_AddressLock)
            {
                Address directMatch = _Addresses.Where(d => d.Ip.Equals(_ip) && d.Netmask.Equals("255.255.255.255")).FirstOrDefault();
                if (directMatch != default(Address))
                {
                    Log(_ip + " found in address list");
                    return true;
                }

                networks = _Addresses.Where(d => !d.Netmask.Equals("255.255.255.255")).ToList();
            }

            if (networks.Count < 1)
            {
                return false;
            }

            foreach (Address curr in networks)
            {
                IPAddress maskedAddress;
                if (!ApplySubnetMask(parsed, curr.ParsedNetmask, out maskedAddress))
                {
                    continue;
                }

                if (curr.ParsedAddress.Equals(maskedAddress))
                {
                    Log(_ip + " matched from address list");

                    if (!_Cache.ContainsKey(_ip))
                    {
                        _Cache.TryAdd(_ip, DateTime.Now);
                    }

                    Log(_ip + " added to cache");

                    return true;
                }
            }

            return false;
        }

        public List<string> All()
        {
            List<string> ret = new List<string>();

            lock (_AddressLock)
            {
                foreach (Address addr in _Addresses)
                {
                    ret.Add(addr.Ip + "/" + addr.Netmask);
                }
            }

            return ret;
        }

        private void Log(string msg)
        {
            logger?.WriteInfo(_Header + msg);
        }

        private bool ApplySubnetMask(IPAddress address, IPAddress mask, out IPAddress masked)
        {
            masked = null;
            byte[] addrBytes = address.GetAddressBytes();
            byte[] maskBytes = mask.GetAddressBytes();

            if (!ApplySubnetMask(addrBytes, maskBytes, out byte[] maskedAddressBytes))
            {
                return false;
            }

            masked = new IPAddress(maskedAddressBytes);
            return true;
        }

        private bool ApplySubnetMask(byte[] value, byte[] mask, out byte[] masked)
        {
            masked = new byte[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                masked[i] = 0x00;
            }

            if (!VerifyContiguousMask(mask))
            {
                return false;
            }

            for (int i = 0; i < masked.Length; ++i)
            {
                masked[i] = (byte)(value[i] & mask[i]);
            }

            return true;
        }

        private bool VerifyContiguousMask(byte[] mask)
        {
            int i;

            // Check leading one bits 
            for (i = 0; i < mask.Length; ++i)
            {
                byte curByte = mask[i];
                if (curByte == 0xFF)
                {
                    // Full 8-bits, check next bytes. 
                }
                else if (curByte == 0)
                {
                    // A full byte of 0s. 
                    // Check subsequent bytes are all zeros. 
                    break;
                }
                else if (Array.IndexOf<byte>(_ContiguousPatterns, curByte) != -1)
                {
                    // A bit-wise contiguous ending in zeros. 
                    // Check subsequent bytes are all zeros. 
                    break;
                }
                else
                {
                    // A non-contiguous pattern -> Fail. 
                    return false;
                }
            }

            // Now check that all the subsequent bytes are all zeros. 
            for (i += 1/*next*/; i < mask.Length; ++i)
            {
                byte curByte = mask[i];
                if (curByte != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private string GetBaseIpAddress(string ip, string netmask)
        {
            IPAddress ipAddr = IPAddress.Parse(ip);
            IPAddress mask = IPAddress.Parse(netmask);

            byte[] ipAddrBytes = ipAddr.GetAddressBytes();
            byte[] maskBytes = mask.GetAddressBytes();

            byte[] afterAnd = And(ipAddrBytes, maskBytes);
            IPAddress baseAddr = new IPAddress(afterAnd);
            return baseAddr.ToString();
        }

        private byte[] And(byte[] addr, byte[] mask)
        {
            if (addr.Length != mask.Length)
            {
                throw new ArgumentException("Supplied arrays are not of the same length.");
            }

            BitArray baAddr = new BitArray(addr);
            BitArray baMask = new BitArray(mask);
            BitArray baResult = baAddr.And(baMask);
            byte[] result = new byte[addr.Length];
            baResult.CopyTo(result, 0);

            return result;
        }

        private byte[] ExclusiveOr(byte[] addr, byte[] mask)
        {
            if (addr.Length != mask.Length)
            {
                throw new ArgumentException("Supplied arrays are not of the same length.");
            }

            byte[] result = new byte[addr.Length];

            for (int i = 0; i < addr.Length; ++i)
            {
                result[i] = (byte)(addr[i] ^ mask[i]);
            }

            return result;
        }

        private string ByteArrayToHexString(byte[] Bytes)
        {
            StringBuilder Result = new StringBuilder(Bytes.Length * 2);
            string HexAlphabet = "0123456789ABCDEF";

            foreach (byte B in Bytes)
            {
                Result.Append(HexAlphabet[B >> 4]);
                Result.Append(HexAlphabet[B & 0xF]);
            }

            return Result.ToString();
        }

        internal class Address
        {
            internal string GUID { get; set; }
            internal string Ip { get; set; }
            internal string Netmask { get; set; }
            internal IPAddress ParsedAddress { get; set; }
            internal IPAddress ParsedNetmask { get; set; }

            internal Address(string ip, string netmask)
            {
                GUID = Guid.NewGuid().ToString();
                Ip = ip;
                Netmask = netmask;
                ParsedAddress = IPAddress.Parse(ip);
                ParsedNetmask = IPAddress.Parse(netmask);
            }
        }
    }
}