using System.Collections;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Infinity.Core
{
    public class IpMatcher
    {
        ILogger logger = null;

        private string header = "[IpMatcher] ";
        private readonly object address_lock = new object();
        private List<Address> addresses = new List<Address>();
        private FasterConcurrentDictionary<string, DateTime> cache = new FasterConcurrentDictionary<string, DateTime>();
        private static readonly byte[] contiguous_patterns = { 0x80, 0xC0, 0xE0, 0xF0, 0xF8, 0xFC, 0xFE, 0xFF };

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

            string base_address = GetBaseIpAddress(_ip, _netmask);

            if (Exists(base_address, _netmask))
            {
                return;
            }

            lock (address_lock)
            {
                addresses.Add(new Address(base_address, _netmask));
            }

            Log(base_address + " " + _netmask + " added");
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

            if (cache.ContainsKey(_ip))
            {
                Log(_ip + " " + _netmask + " exists in cache");
                return true;
            }

            lock (address_lock)
            {
                Address curr = addresses.Where(d => d.Ip.Equals(_ip) && d.Netmask.Equals(_netmask)).FirstOrDefault();
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

            cache.Remove(_ip, out DateTime _);
            Log(_ip + " removed from cache");

            lock (address_lock)
            {
                addresses = addresses.Where(d => !d.Ip.Equals(_ip)).ToList();
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

            if (cache.ContainsKey(_ip))
            {
                Log(_ip + " found in cache");
                return true;
            }

            List<Address> networks = new List<Address>();

            lock (address_lock)
            {
                Address direct_match = addresses.Where(d => d.Ip.Equals(_ip) && d.Netmask.Equals("255.255.255.255")).FirstOrDefault();
                if (direct_match != default(Address))
                {
                    Log(_ip + " found in address list");
                    return true;
                }

                networks = addresses.Where(d => !d.Netmask.Equals("255.255.255.255")).ToList();
            }

            if (networks.Count < 1)
            {
                return false;
            }

            foreach (Address curr in networks)
            {
                IPAddress masked_address;
                if (!ApplySubnetMask(parsed, curr.ParsedNetmask, out masked_address))
                {
                    continue;
                }

                if (curr.ParsedAddress.Equals(masked_address))
                {
                    Log(_ip + " matched from address list");

                    if (!cache.ContainsKey(_ip))
                    {
                        cache.TryAdd(_ip, DateTime.Now);
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

            lock (address_lock)
            {
                foreach (Address addr in addresses)
                {
                    ret.Add(addr.Ip + "/" + addr.Netmask);
                }
            }

            return ret;
        }

        private void Log(string msg)
        {
            logger?.WriteInfo(header + msg);
        }

        private bool ApplySubnetMask(IPAddress _address, IPAddress _mask, out IPAddress _masked)
        {
            _masked = null;
            byte[] address_bytes = _address.GetAddressBytes();
            byte[] mask_bytes = _mask.GetAddressBytes();

            if (!ApplySubnetMask(address_bytes, mask_bytes, out byte[] masked_address_bytes))
            {
                return false;
            }

            _masked = new IPAddress(masked_address_bytes);
            return true;
        }

        private bool ApplySubnetMask(byte[] _value, byte[] _mask, out byte[] _masked)
        {
            _masked = new byte[_value.Length];
            for (int i = 0; i < _value.Length; i++)
            {
                _masked[i] = 0x00;
            }

            if (!VerifyContiguousMask(_mask))
            {
                return false;
            }

            for (int i = 0; i < _masked.Length; ++i)
            {
                _masked[i] = (byte)(_value[i] & _mask[i]);
            }

            return true;
        }

        private bool VerifyContiguousMask(byte[] _mask)
        {
            int i;

            // Check leading one bits 
            for (i = 0; i < _mask.Length; ++i)
            {
                byte cur_byte = _mask[i];
                if (cur_byte == 0xFF)
                {
                    // Full 8-bits, check next bytes. 
                }
                else if (cur_byte == 0)
                {
                    // A full byte of 0s. 
                    // Check subsequent bytes are all zeros. 
                    break;
                }
                else if (Array.IndexOf<byte>(contiguous_patterns, cur_byte) != -1)
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
            for (i += 1/*next*/; i < _mask.Length; ++i)
            {
                byte cur_byte = _mask[i];
                if (cur_byte != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private string GetBaseIpAddress(string _ip, string _netmask)
        {
            IPAddress ip_addr = IPAddress.Parse(_ip);
            IPAddress mask = IPAddress.Parse(_netmask);

            byte[] ip_addr_bytes = ip_addr.GetAddressBytes();
            byte[] mask_bytes = mask.GetAddressBytes();

            byte[] after_and = And(ip_addr_bytes, mask_bytes);
            IPAddress base_addr = new IPAddress(after_and);
            return base_addr.ToString();
        }

        private byte[] And(byte[] _addr, byte[] _mask)
        {
            if (_addr.Length != _mask.Length)
            {
                throw new ArgumentException("Supplied arrays are not of the same length.");
            }

            BitArray ba_addr = new BitArray(_addr);
            BitArray ba_mask = new BitArray(_mask);
            BitArray ba_result = ba_addr.And(ba_mask);
            byte[] result = new byte[_addr.Length];
            ba_result.CopyTo(result, 0);

            return result;
        }

        private byte[] ExclusiveOr(byte[] _addr, byte[] _mask)
        {
            if (_addr.Length != _mask.Length)
            {
                throw new ArgumentException("Supplied arrays are not of the same length.");
            }

            byte[] result = new byte[_addr.Length];

            for (int i = 0; i < _addr.Length; ++i)
            {
                result[i] = (byte)(_addr[i] ^ _mask[i]);
            }

            return result;
        }

        private string ByteArrayToHexString(byte[] _bytes)
        {
            StringBuilder result = new StringBuilder(_bytes.Length * 2);
            string hex_alphabet = "0123456789ABCDEF";

            foreach (byte B in _bytes)
            {
                result.Append(hex_alphabet[B >> 4]);
                result.Append(hex_alphabet[B & 0xF]);
            }

            return result.ToString();
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