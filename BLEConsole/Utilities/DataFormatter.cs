using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;
using ByteOrder = Windows.Storage.Streams.ByteOrder;

namespace BLEConsole.Utils
{
    public static class DataFormatter
    {
        /// <summary>
        /// Converts from a buffer to a properly sized byte array
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public static byte[] ReadBufferToBytes(IBuffer buffer)
        {
            var dataLength = buffer.Length;
            var data = new byte[dataLength];
            using (var reader = DataReader.FromBuffer(buffer))
            {
                reader.ReadBytes(data);
            }
            return data;
        }

        /// <summary>
        /// This function converts IBuffer data to string by specified format
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="format"></param>
        /// <param name="byteOrder"></param>
        /// <returns></returns>
        public static string FormatValue(IBuffer buffer, Enums.DataFormat format, ByteOrder byteOrder = ByteOrder.LittleEndian)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

            // For numeric formats, respect byte order
            if (format == Enums.DataFormat.Dec && data.Length > 1)
            {
                byte[] orderedData = (byte[])data.Clone();
                if (byteOrder == ByteOrder.BigEndian)
                    Array.Reverse(orderedData);
                return FormatNumericValue(orderedData);
            }

            switch (format)
            {
                case Enums.DataFormat.ASCII:
                    return Encoding.ASCII.GetString(data);

                case Enums.DataFormat.UTF8:
                    return Encoding.UTF8.GetString(data);

                case Enums.DataFormat.Dec:
                    return string.Join(" ", data.Select(b => b.ToString("00")));

                case Enums.DataFormat.Hex:
                    return BitConverter.ToString(data).Replace("-", " ");

                case Enums.DataFormat.Bin:
                    var s = string.Empty;
                    foreach (var b in data) s += Convert.ToString(b, 2).PadLeft(8, '0') + " ";
                    return s;

                default:
                    return Encoding.ASCII.GetString(data);
            }
        }

        /// <summary>
        /// Format numeric value based on byte array length
        /// </summary>
        private static string FormatNumericValue(byte[] data)
        {
            switch (data.Length)
            {
                case 1:
                    return data[0].ToString();
                case 2:
                    return BitConverter.ToInt16(data, 0).ToString();
                case 4:
                    return BitConverter.ToInt32(data, 0).ToString();
                case 8:
                    return BitConverter.ToInt64(data, 0).ToString();
                default:
                    // For other lengths, show as individual bytes
                    return string.Join(" ", data.Select(b => b.ToString("00")));
            }
        }

        /// <summary>
        /// This function converts IBuffer data to string by specified list of formats
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="formatList"></param>
        /// <param name="byteOrder"></param>
        /// <returns></returns>
        public static string FormatValueMultipleFormattes(IBuffer buffer, List<Enums.DataFormat> formatList, ByteOrder byteOrder = ByteOrder.LittleEndian)
        {
            byte[] data;
            String stringBuffer = "";

            CryptographicBuffer.CopyToByteArray(buffer, out data);
            for (int dataFormatIdx = 0; dataFormatIdx < formatList.Count; dataFormatIdx++)
            {
                Enums.DataFormat dataFormat = formatList[dataFormatIdx];
                switch (dataFormat)
                {
                    case Enums.DataFormat.ASCII:
                        stringBuffer += $"ascii: {Encoding.ASCII.GetString(data)}";
                        break;

                    case Enums.DataFormat.UTF8:
                        stringBuffer += $"utf8:\t{Encoding.UTF8.GetString(data)}";
                        break;

                    case Enums.DataFormat.Dec:
                        if (data.Length > 1)
                        {
                            byte[] orderedData = (byte[])data.Clone();
                            if (byteOrder == ByteOrder.BigEndian)
                                Array.Reverse(orderedData);
                            stringBuffer += $"dec:\t{FormatNumericValue(orderedData)}";
                        }
                        else
                        {
                            stringBuffer += $"dec:\t{string.Join(" ", data.Select(b => b.ToString("00")))}";
                        }
                        break;

                    case Enums.DataFormat.Hex:
                        stringBuffer += $"hex:\t{BitConverter.ToString(data).Replace("-", " ")}";
                        break;

                    case Enums.DataFormat.Bin:
                        var s = string.Empty;
                        foreach (var b in data) s += Convert.ToString(b, 2).PadLeft(8, '0') + " ";
                        stringBuffer += $"bin:\t{s}";
                        break;

                    default:
                        stringBuffer += $"ascii: {Encoding.ASCII.GetString(data)}";
                        break;
                }
                if (dataFormatIdx != formatList.Count - 1)
                {
                    stringBuffer += "\n";
                }
            }
            return stringBuffer;
        }

        /// <summary>
        /// Format data for writing by specific format
        /// </summary>
        /// <param name="data"></param>
        /// <param name="format"></param>
        /// <param name="byteOrder"></param>
        /// <returns></returns>
        public static IBuffer FormatData(string data, Enums.DataFormat format, ByteOrder byteOrder = ByteOrder.LittleEndian)
        {
            try
            {
                // For text formats, use CryptographicBuffer
                if (format == Enums.DataFormat.ASCII || format == Enums.DataFormat.UTF8)
                {
                    return CryptographicBuffer.ConvertStringToBinary(Regex.Unescape(data), BinaryStringEncoding.Utf8);
                }
                else if (format == Enums.DataFormat.Dec)
                {
                    // For decimal format, try to parse as single number first
                    var writer = new DataWriter();
                    writer.ByteOrder = byteOrder;

                    string trimmedData = data.Trim();

                    // Check if it's a single number or space-separated bytes
                    if (!trimmedData.Contains(" "))
                    {
                        // Single value - determine size automatically
                        if (long.TryParse(trimmedData, out long longVal))
                        {
                            if (longVal >= sbyte.MinValue && longVal <= byte.MaxValue)
                                writer.WriteByte((byte)longVal);
                            else if (longVal >= short.MinValue && longVal <= ushort.MaxValue)
                                writer.WriteInt16((short)longVal);
                            else if (longVal >= int.MinValue && longVal <= uint.MaxValue)
                                writer.WriteInt32((int)longVal);
                            else
                                writer.WriteInt64(longVal);
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        // Space-separated byte values
                        string[] values = trimmedData.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        byte[] bytes = new byte[values.Length];
                        for (int i = 0; i < values.Length; i++)
                            bytes[i] = Convert.ToByte(values[i], 10);
                        writer.WriteBytes(bytes);
                    }

                    return writer.DetachBuffer();
                }
                else
                {
                    // Hex or Binary format - parse as byte array
                    string[] values = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    byte[] bytes = new byte[values.Length];

                    int numBase = format == Enums.DataFormat.Hex ? 16 : 2;
                    for (int i = 0; i < values.Length; i++)
                        bytes[i] = Convert.ToByte(values[i], numBase);

                    var writer = new DataWriter();
                    writer.ByteOrder = byteOrder;
                    writer.WriteBytes(bytes);

                    return writer.DetachBuffer();
                }
            }
            catch (Exception error)
            {
                Console.WriteLine(error.Message);
                return null;
            }
        }
    }
}
