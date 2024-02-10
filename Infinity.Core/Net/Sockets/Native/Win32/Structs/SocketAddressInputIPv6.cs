using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ScopeUnion
    {

        /// ULONG->int
        [FieldOffset(0)]
        public int sin6_scope_id;

        /// SCOPE_ID->Anonymous_bce729ec_842a_4c37_af40_15f7178b075b
        [FieldOffset(0)]
        public ScopeId sin6_scope_struct;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SocketAddressInputIPv6
    {

        /// ADDRESS_FAMILY->USHORT->short
        public short sin6_family;

        /// USHORT->short
        public short sin6_port;

        /// ULONG->int
        public int sin6_flowinfo;

        /// IN6_ADDR->in6_addr
        public InputIPv6SocketAddress sin6_addr;

        /// Anonymous_fa72d4f1_a4a7_4f2b_af13_d4f4410f9873
        public ScopeUnion Scope;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputIPv6SocketAddressUnion
    {

        /// UCHAR[16]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.I1)]
        [FieldOffset(0)]
        public byte[] Byte;

        /// USHORT[8]
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.I2)]
        [FieldOffset(0)]
        public short[] Word;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputIPv6SocketAddress
    {

        /// Anonymous_61d846d8_2e3a_4c56_ad5c_e484bd003ec0
        public InputIPv6SocketAddressUnion u;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DUMMYSTRUCTNAME
    {

        /// Zone : 28
        ///Level : 4
        public uint bitvector1;

        public uint Zone
        {
            get
            {
                return bitvector1 & 268435455u;
            }
            set
            {
                bitvector1 = (value | bitvector1);
            }
        }

        public uint Level
        {
            get
            {
                return (uint)((bitvector1 & 4026531840u) / 268435456D);
            }
            set
            {
                bitvector1 = (value * (uint)268435456D) | bitvector1;
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ScopeIdUnion
    {

        /// Anonymous_a13b53f0_80f9_41db_8b52_f10ab85b3c60
        [FieldOffset(0)]
        public DUMMYSTRUCTNAME DUMMYSTRUCTNAME;

        /// ULONG->int
        [FieldOffset(0)]
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ScopeId
    {

        /// Anonymous_da529b55_beae_4b36_b89d_6da74a009963
        public ScopeIdUnion Union1;
    }

}
