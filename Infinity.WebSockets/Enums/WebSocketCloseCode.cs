namespace Infinity.WebSockets.Enums
{
	public enum WebSocketCloseCode : ushort
	{
		NormalClosure = 1000,
		GoingAway = 1001,
		ProtocolError = 1002,
		UnsupportedData = 1003,
		NoStatusRcvd = 1005,
		AbnormalClosure = 1006,
		InvalidPayloadData = 1007,
		PolicyViolation = 1008,
		MessageTooBig = 1009,
		MandatoryExtension = 1010,
		InternalServerError = 1011,
		TLSHandshake = 1015
	}
}


