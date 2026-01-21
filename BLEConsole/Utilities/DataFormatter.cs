using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

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
        /// <returns></returns>
        public static string FormatValue(IBuffer buffer, Enums.DataFormat format)
        {
            byte[] data;
            CryptographicBuffer.CopyToByteArray(buffer, out data);

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
        /// This function converts IBuffer data to string by specified list of formats
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="formatList"></param>
        /// <returns></returns>
        public static string FormatValueMultipleFormattes(IBuffer buffer, List<Enums.DataFormat> formatList)
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
                        stringBuffer += $"dec:\t{string.Join(" ", data.Select(b => b.ToString("00")))}";
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
        /// <returns></returns>
        public static IBuffer FormatData(string data, Enums.DataFormat format)
        {
            try
            {
                // For text formats, use CryptographicBuffer
                if (format == Enums.DataFormat.ASCII || format == Enums.DataFormat.UTF8)
                {
                    return CryptographicBuffer.ConvertStringToBinary(Regex.Unescape(data), BinaryStringEncoding.Utf8);
                }
                else
                {
                    string[] values = data.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    byte[] bytes = new byte[values.Length];

                    for (int i = 0; i < values.Length; i++)
                        bytes[i] = Convert.ToByte(values[i], (format == Enums.DataFormat.Dec ? 10 : (format == Enums.DataFormat.Hex ? 16 : 2)));

                    var writer = new DataWriter();
                    writer.ByteOrder = ByteOrder.LittleEndian;
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
