using System;

namespace BLEConsole.Utilities
{
    public static class ProtocolErrorFormatter
    {
        public static string FormatProtocolError(byte? protocolError)
        {
            if (protocolError == null)
                return "";

            string protocolErrorCodeName = Enum.GetName(typeof(Enums.ProtocolErrorCode), protocolError);
            if (protocolErrorCodeName != null)
            {
                protocolErrorCodeName = protocolErrorCodeName.Replace("_", " ");
                return String.Format("0x{0:X2}: {1}", protocolError, protocolErrorCodeName);
            }
            return String.Format("0x{0:X2}: Unknown", protocolError);
        }
    }
}
