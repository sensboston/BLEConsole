namespace BLEConsole.Enums
{
    public enum ProtocolErrorCode : byte
    {
        Invalid_Handle = 0x01,
        Read_Not_Permitted = 0x02,
        Write_Not_Permitted = 0x03,
        Invalid_PDU = 0x04,
        Insufficient_Authentication = 0x05,
        Request_Not_Supported = 0x06,
        Invalid_Offset = 0x07,
        Insufficient_Authorization = 0x08,
        Prepare_Queue_Full = 0x09,
        Attribute_Not_Found = 0x0A,
        Attribute_Not_Long = 0x0B,
        Insufficient_Encryption_Key_Size = 0x0C,
        Invalid_Attribute_Value_Length = 0x0D,
        Unlikely_Error = 0x0E,
        Insufficient_Encryption = 0x0F,
        Unsupported_Group_Type = 0x10,
        Insufficient_Resource = 0x11,
        Database_Out_Of_Sync = 0x12,
        Value_Not_Allowed = 0x13,
        Write_Request_Rejected = 0xFC,
        Client_Characteristic_Configuration_Descriptor_Improperly_Configured = 0xFD,
        Procedure_Already_in_Progress = 0xFE,
        Out_of_Range = 0xFF
    }
}
