﻿using System.Diagnostics;

namespace Infinity.Core.Net
{
    internal static class IPv6AddressHelper
    {

        // fields

        private const int NumberOfLabels = 8;
        // Upper case hex, zero padded to 4 characters
        private const string LegacyFormat = "{0:X4}:{1:X4}:{2:X4}:{3:X4}:{4:X4}:{5:X4}:{6:X4}:{7:X4}";
        // Lower case hex, no leading zeros
        private const string CanonicalNumberFormat = "{0:x}";
        private const string EmbeddedIPv4Format = ":{0:d}.{1:d}.{2:d}.{3:d}";
        private const string Separator = ":";

        // methods

        // RFC 5952 Section 4.2.3
        // Longest consecutive sequence of zero segments, minimum 2.
        // On equal, first sequence wins.
        // <-1, -1> for no compression.
        private unsafe static KeyValuePair<int, int> FindCompressionRange(ushort* numbers)
        {
            int longestSequenceLength = 0;
            int longestSequenceStart = -1;

            int currentSequenceLength = 0;
            for (int i = 0; i < NumberOfLabels; i++)
            {
                if (numbers[i] == 0)
                { // In a sequence 
                    currentSequenceLength++;
                    if (currentSequenceLength > longestSequenceLength)
                    {
                        longestSequenceLength = currentSequenceLength;
                        longestSequenceStart = i - currentSequenceLength + 1;
                    }
                }
                else
                {
                    currentSequenceLength = 0;
                }
            }

            if (longestSequenceLength >= 2)
            {
                return new KeyValuePair<int, int>(longestSequenceStart,
                    longestSequenceStart + longestSequenceLength - 1);
            }

            return new KeyValuePair<int, int>(-1, -1); // No compression
        }

        // Returns true if the IPv6 address should be formated with an embedded IPv4 address:
        // ::192.168.1.1
        private unsafe static bool ShouldHaveIpv4Embedded(ushort* numbers)
        {
            // 0:0 : 0:0 : x:x : x.x.x.x
            if (numbers[0] == 0 && numbers[1] == 0 && numbers[2] == 0 && numbers[3] == 0 && numbers[6] != 0)
            {
                // RFC 5952 Section 5 - 0:0 : 0:0 : 0:[0 | FFFF] : x.x.x.x
                if (numbers[4] == 0 && (numbers[5] == 0 || numbers[5] == 0xFFFF))
                {
                    return true;
                }
                // SIIT - 0:0 : 0:0 : FFFF:0 : x.x.x.x
                else if (numbers[4] == 0xFFFF && numbers[5] == 0)
                {
                    return true;
                }
            }
            // ISATAP
            if (numbers[4] == 0 && numbers[5] == 0x5EFE)
            {
                return true;
            }

            return false;
        }

        //
        // InternalIsValid
        //
        //  Determine whether a name is a valid IPv6 address. Rules are:
        //
        //   *  8 groups of 16-bit hex numbers, separated by ':'
        //   *  a *single* run of zeros can be compressed using the symbol '::'
        //   *  an optional string of a ScopeID delimited by '%'
        //   *  an optional (last) 1 or 2 character prefix length field delimited by '/'
        //   *  the last 32 bits in an address can be represented as an IPv4 address
        //
        // Inputs:
        //  <argument>  name
        //      Domain name field of a URI to check for pattern match with
        //      IPv6 address
        //  validateStrictAddress: if set to true, it expects strict ipv6 address. Otherwise it expects
        //      part of the string in ipv6 format.
        //
        // Outputs:
        //  Nothing
        //
        // Assumes:
        //  the correct name is terminated by  ']' character
        //
        // Returns:
        //  true if <name> has IPv6 format/ipv6 address based on validateStrictAddress, else false
        //
        // Throws:
        //  Nothing
        //

        //  Remarks: MUST NOT be used unless all input indexes are verified and trusted.
        //           start must be next to '[' position, or error is reported
        unsafe private static bool InternalIsValid(char* name, int start, ref int end, bool validateStrictAddress)
        {

            int sequenceCount = 0;
            int sequenceLength = 0;
            bool haveCompressor = false;
            bool haveIPv4Address = false;
            bool havePrefix = false;
            bool expectingNumber = true;
            int lastSequence = 1;

            // Starting with a colon character is only valid if another colon follows.
            if (name[start] == ':' && (start + 1 >= end || name[start + 1] != ':'))
            {
                return false;
            }

            int i;
            for (i = start; i < end; ++i)
            {
                if (havePrefix ? name[i] >= '0' && name[i] <= '9' : Uri.IsHexDigit(name[i]))
                {
                    ++sequenceLength;
                    expectingNumber = false;
                }
                else
                {
                    if (sequenceLength > 4)
                    {
                        return false;
                    }
                    if (sequenceLength != 0)
                    {
                        ++sequenceCount;
                        lastSequence = i - sequenceLength;
                    }
                    switch (name[i])
                    {
                        case '%':
                            while (true)
                            {
                                //accept anything in scopeID
                                if (++i == end)
                                {
                                    // no closing ']', fail
                                    return false;
                                }
                                if (name[i] == ']')
                                {
                                    goto case ']';
                                }
                                else if (name[i] == '/')
                                {
                                    goto case '/';
                                }
                            }
                        case ']':
                            start = i;
                            i = end;
                            //this will make i = end+1
                            continue;
                        case ':':
                            if (i > 0 && name[i - 1] == ':')
                            {
                                if (haveCompressor)
                                {

                                    //
                                    // can only have one per IPv6 address
                                    //

                                    return false;
                                }
                                haveCompressor = true;
                                expectingNumber = false;
                            }
                            else
                            {
                                expectingNumber = true;
                            }
                            break;

                        case '/':
                            if (validateStrictAddress)
                            {
                                return false;
                            }
                            if (sequenceCount == 0 || havePrefix)
                            {
                                return false;
                            }
                            havePrefix = true;
                            expectingNumber = true;
                            break;

                        case '.':
                            if (haveIPv4Address)
                            {
                                return false;
                            }

                            i = end;
                            if (!IPv4AddressHelper.IsValid(name, lastSequence, ref i, true, false, false))
                            {
                                return false;
                            }
                            // ipv4 address takes 2 slots in ipv6 address, one was just counted meeting the '.'
                            ++sequenceCount;
                            haveIPv4Address = true;
                            --i;            // it will be incremented back on the next loop
                            break;

                        default:
                            return false;
                    }
                    sequenceLength = 0;
                }
            }

            //
            // if the last token was a prefix, check number of digits
            //

            if (havePrefix && (sequenceLength < 1 || sequenceLength > 2))
            {
                return false;
            }

            //
            // these sequence counts are -1 because it is implied in end-of-sequence
            //

            int expectedSequenceCount = 8 + (havePrefix ? 1 : 0);

            if (!expectingNumber && sequenceLength <= 4 && (haveCompressor ? sequenceCount < expectedSequenceCount : sequenceCount == expectedSequenceCount))
            {
                if (i == end + 1)
                {
                    // ']' was found
                    end = start + 1;
                    return true;
                }
                return false;
            }
            return false;
        }

        //
        // IsValid
        //
        //  Determine whether a name is a valid IPv6 address. Rules are:
        //
        //   *  8 groups of 16-bit hex numbers, separated by ':'
        //   *  a *single* run of zeros can be compressed using the symbol '::'
        //   *  an optional string of a ScopeID delimited by '%'
        //   *  an optional (last) 1 or 2 character prefix length field delimited by '/'
        //   *  the last 32 bits in an address can be represented as an IPv4 address
        //
        // Inputs:
        //  <argument>  name
        //      Domain name field of a URI to check for pattern match with
        //      IPv6 address
        //
        // Outputs:
        //  Nothing
        //
        // Assumes:
        //  the correct name is terminated by  ']' character
        //
        // Returns:
        //  true if <name> has IPv6 format, else false
        //
        // Throws:
        //  Nothing
        //

        //  Remarks: MUST NOT be used unless all input indexes are are verified and trusted.
        //           start must be next to '[' position, or error is reported

        internal unsafe static bool IsValid(char* name, int start, ref int end)
        {
            return InternalIsValid(name, start, ref end, false);
        }

        //
        // IsValidStrict
        //
        //  Determine whether a name is a valid IPv6 address. Rules are:
        //
        //   *  8 groups of 16-bit hex numbers, separated by ':'
        //   *  a *single* run of zeros can be compressed using the symbol '::'
        //   *  an optional string of a ScopeID delimited by '%'
        //   *  the last 32 bits in an address can be represented as an IPv4 address
        //
        //  Difference between IsValid() and IsValidStrict() is that IsValid() expects part of the string to 
        //  be ipv6 address where as IsValidStrict() expects strict ipv6 address.
        //
        // Inputs:
        //  <argument>  name
        //      IPv6 address in string format
        //
        // Outputs:
        //  Nothing
        //
        // Assumes:
        //  the correct name is terminated by  ']' character
        //
        // Returns:
        //  true if <name> is IPv6  address, else false
        //
        // Throws:
        //  Nothing
        //

        //  Remarks: MUST NOT be used unless all input indexes are verified and trusted.
        //           start must be next to '[' position, or error is reported
        internal unsafe static bool IsValidStrict(char* name, int start, ref int end)
        {
            return InternalIsValid(name, start, ref end, true);
        }

        //
        // Parse
        //
        //  Convert this IPv6 address into a sequence of 8 16-bit numbers
        //
        // Inputs:
        //  <member>    Name
        //      The validated IPv6 address
        //
        // Outputs:
        //  <member>    numbers
        //      Array filled in with the numbers in the IPv6 groups
        //
        //  <member>    PrefixLength
        //      Set to the number after the prefix separator (/) if found
        //
        // Assumes:
        //  <Name> has been validated and contains only hex digits in groups of
        //  16-bit numbers, the characters ':' and '/', and a possible IPv4
        //  address
        //
        // Returns:
        //  true if this is a loopback, false otherwise. There is no falure indication as the sting must be a valid one.
        //
        // Throws:
        //  Nothing
        //

        unsafe internal static bool Parse(string address, ushort* numbers, int start, ref string scopeId)
        {

            int number = 0;
            int index = 0;
            int compressorIndex = -1;
            bool numberIsValid = true;

            //This used to be a class instance member but have not been used so far
            int PrefixLength = 0;
            if (address[start] == '[')
            {
                ++start;
            }

            for (int i = start; i < address.Length && address[i] != ']';)
            {
                ValidateIndex(index);
                switch (address[i])
                {
                    case '%':
                        if (numberIsValid)
                        {
                            numbers[index++] = (ushort)number;
                            numberIsValid = false;
                        }

                        start = i;
                        for (++i; address[i] != ']' && address[i] != '/'; ++i)
                        {
                            ;
                        }
                        scopeId = address.Substring(start, i - start);
                        // ignore prefix if any
                        for (; address[i] != ']'; ++i)
                        {
                            ;
                        }
                        break;

                    case ':':
                        numbers[index++] = (ushort)number;
                        number = 0;
                        ++i;
                        if (address[i] == ':')
                        {
                            compressorIndex = index;
                            ++i;
                        }
                        else if (compressorIndex < 0 && index < 6)
                        {

                            //
                            // no point checking for IPv4 address if we don't
                            // have a compressor or we haven't seen 6 16-bit
                            // numbers yet
                            //

                            break;
                        }

                        //
                        // check to see if the upcoming number is really an IPv4
                        // address. If it is, convert it to 2 ushort numbers
                        //

                        for (int j = i; address[j] != ']' &&
                                        address[j] != ':' &&
                                        address[j] != '%' &&
                                        address[j] != '/' &&
                                        j < i + 4; ++j)
                        {

                            if (address[j] == '.')
                            {

                                //
                                // we have an IPv4 address. Find the end of it:
                                // we know that since we have a valid IPv6
                                // address, the only things that will terminate
                                // the IPv4 address are the prefix delimiter '/'
                                // or the end-of-string (which we conveniently
                                // delimited with ']')
                                //

                                while (address[j] != ']' && address[j] != '/' && address[j] != '%')
                                {
                                    ++j;
                                }
                                number = IPv4AddressHelper.ParseHostNumber(address, i, j);
                                ValidateIndex(index);
                                numbers[index++] = (ushort)(number >> 16);
                                ValidateIndex(index);
                                numbers[index++] = (ushort)number;
                                i = j;

                                //
                                // set this to avoid adding another number to
                                // the array if there's a prefix
                                //

                                number = 0;
                                numberIsValid = false;
                                break;
                            }
                        }
                        break;

                    case '/':
                        if (numberIsValid)
                        {
                            numbers[index++] = (ushort)number;
                            numberIsValid = false;
                        }

                        //
                        // since we have a valid IPv6 address string, the prefix
                        // length is the last token in the string
                        //

                        for (++i; address[i] != ']'; ++i)
                        {
                            PrefixLength = PrefixLength * 10 + (address[i] - '0');
                        }
                        break;

                    default:
                        number = number * 16 + Uri.FromHex(address[i++]);
                        break;
                }
            }

            //
            // add number to the array if its not the prefix length or part of
            // an IPv4 address that's already been handled
            //

            if (numberIsValid)
            {
                ValidateIndex(index);
                numbers[index++] = (ushort)number;
            }

            //
            // if we had a compressor sequence ("::") then we need to expand the
            // numbers array
            //

            if (compressorIndex > 0)
            {

                int toIndex = NumberOfLabels - 1;
                int fromIndex = index - 1;

                for (int i = index - compressorIndex; i > 0; --i)
                {
                    ValidateIndex(fromIndex);
                    ValidateIndex(toIndex);
                    numbers[toIndex--] = numbers[fromIndex];
                    ValidateIndex(fromIndex);
                    numbers[fromIndex--] = 0;
                }
            }

            //
            // is the address loopback? Loopback is defined as one of:
            //
            //  0:0:0:0:0:0:0:1
            //  0:0:0:0:0:0:127.0.0.1       == 0:0:0:0:0:0:7F00:0001
            //  0:0:0:0:0:FFFF:127.0.0.1    == 0:0:0:0:0:FFFF:7F00:0001
            //

            return numbers[0] == 0
                            && numbers[1] == 0
                            && numbers[2] == 0
                            && numbers[3] == 0
                            && numbers[4] == 0
                           && (numbers[5] == 0
                                && numbers[6] == 0
                                && numbers[7] == 1
                               || numbers[6] == 0x7F00
                                    && numbers[7] == 0x0001
                                   && (numbers[5] == 0
                                       || numbers[5] == 0xFFFF));

        }

        [Conditional("DEBUG")]
        private static void ValidateIndex(int index)
        {
            Debug.Assert(index >= 0 && index < NumberOfLabels, "index = " + index.ToString());
        }
    }
}
