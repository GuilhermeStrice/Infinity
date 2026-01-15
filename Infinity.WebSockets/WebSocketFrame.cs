using System.Buffers.Binary;
using System.Security.Cryptography;
using Infinity.Core;
using Infinity.WebSockets.Enums;

namespace Infinity.WebSockets
{
	public static class WebSocketFrame
	{
		public static MessageWriter CreateFrame(WebSocketConnection connection, ReadOnlySpan<byte> _payload, int _length, WebSocketOpcode _opcode, bool _fin, bool _mask)
		{
			int headerLen = 2;
			int extendedLen = 0;
			if (_length >= 126 && _length <= ushort.MaxValue)
			{
				extendedLen = 2;
			}
			else if (_length > ushort.MaxValue)
			{
				extendedLen = 8;
			}

			int maskLen = _mask ? 4 : 0;
			int totalLen = headerLen + extendedLen + maskLen + _length;
			
			MessageWriter writer = new MessageWriter(connection.allocator);
			writer.Position = 0; // Use from start
			
			// First byte: FIN flag and opcode
			writer.Write((byte)((_fin ? 0x80 : 0x00) | ((byte)_opcode & 0x0F)));

			// Second byte: MASK flag and length
			byte lenByte;
			if (_length < 126)
			{
				lenByte = (byte)_length;
			}
			else if (_length <= ushort.MaxValue)
			{
				lenByte = 126;
			}
			else
			{
				lenByte = 127;
			}

			writer.Write((byte)((_mask ? 0x80 : 0x00) | lenByte));

			// Extended length if needed
			if (lenByte == 126)
			{
				ushort len16 = (ushort)_length;
				writer.Write((byte)(len16 >> 8));
				writer.Write((byte)(len16 & 0xFF));
			}
			else if (lenByte == 127)
			{
				ulong len64 = (ulong)_length;
				for (int i = 7; i >= 0; i--)
				{
					writer.Write((byte)((len64 >> (i * 8)) & 0xFF));
				}
			}

			// Mask key (for masking only)
			if (_mask)
			{
				RandomNumberGenerator.Fill(writer.Buffer.Slice(writer.Position, 4));
				writer.Position += 4;
			}

			// Payload
			if (_mask)
			{
				Span<byte> maskKey = writer.Buffer.Slice(writer.Position - 4, 4);
				for (int i = 0; i < _length; i++)
				{
					writer.Write((byte)(_payload[i] ^ maskKey[i % 4]));
				}
			}
			else
			{
				_payload.Slice(0, _length).CopyTo(writer.Buffer.Slice(writer.Position));
				writer.Position += _length;
			}

			writer.Length = writer.Position;
			return writer;
		}

		public static bool TryReadFrame(Stream _stream, out WebSocketOpcode _opcode, out bool _fin, out bool _masked, out byte[] _payload)
		{
			_opcode = WebSocketOpcode.Binary;
			_fin = true;
			_masked = false;
			_payload = Array.Empty<byte>();

			int b1 = _stream.ReadByte();
			if (b1 == -1)
			{
				return false;
			}
			int b2 = _stream.ReadByte();
			if (b2 == -1)
			{
				return false;
			}

			_fin = (b1 & 0x80) != 0;
			_opcode = (WebSocketOpcode)(b1 & 0x0F);

			bool masked = (b2 & 0x80) != 0;
			ulong lenIndicator = (ulong)(b2 & 0x7F);
			_masked = masked;

			ulong payloadLen = lenIndicator;
			if (lenIndicator == 126)
			{
				Span<byte> len2 = stackalloc byte[2];
				if (_stream.Read(len2) != 2) return false;
				payloadLen = BinaryPrimitives.ReadUInt16BigEndian(len2);
			}
			else if (lenIndicator == 127)
			{
				Span<byte> len8 = stackalloc byte[8];
				if (_stream.Read(len8) != 8) return false;
				payloadLen = BinaryPrimitives.ReadUInt64BigEndian(len8);
			}

			Span<byte> maskKey = stackalloc byte[4];
			if (masked)
			{
				if (_stream.Read(maskKey) != 4) return false;
			}

			if (payloadLen > int.MaxValue)
			{
				return false;
			}

			byte[] payload = new byte[(int)payloadLen];
			int totalRead = 0;
			while (totalRead < (int)payloadLen)
			{
				int read = _stream.Read(payload, totalRead, (int)payloadLen - totalRead);
				if (read <= 0) return false;
				totalRead += read;
			}

			if (masked)
			{
				for (int i = 0; i < payload.Length; i++)
				{
					payload[i] = (byte)(payload[i] ^ maskKey[i % 4]);
				}
			}

			_payload = payload;
			return true;
		}
	}
}


