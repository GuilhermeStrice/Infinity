using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ScopeUnion
    {
        [FieldOffset(0)]
        public int sin6_scope_id;

        [FieldOffset(0)]
        public ScopeId sin6_scope_struct;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SocketAddressInputIPv6
    {
        public short sin6_family;
        public short sin6_port;
        public int sin6_flowinfo;
        public InputIPv6SocketAddress sin6_addr;
        public ScopeUnion Scope;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputIPv6SocketAddressUnion
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16, ArraySubType = UnmanagedType.I1)]
        [FieldOffset(0)]
        public byte[] Byte;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8, ArraySubType = UnmanagedType.I2)]
        [FieldOffset(0)]
        public ushort[] Word;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputIPv6SocketAddress
    {
        public InputIPv6SocketAddressUnion u;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DUMMYSTRUCTNAME
    {
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
        [FieldOffset(0)]
        public DUMMYSTRUCTNAME DUMMYSTRUCTNAME;

        [FieldOffset(0)]
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ScopeId
    {
        public ScopeIdUnion Union1;
    }

}
