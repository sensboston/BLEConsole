using System.Threading.Tasks;
using BLEConsole.Core;
using Windows.Storage.Streams;

namespace BLEConsole.Commands.ConfigCommands
{
    /// <summary>
    /// Set or display byte order (endianness) for read/write operations
    /// </summary>
    public class EndianCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "endian";
        public string[] Aliases => new[] { "bo" };
        public string Description => "Set or display byte order (endianness)";
        public string Usage => "endian [little|big]";

        public EndianCommand(IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                // Display current byte order
                string current = context.ByteOrder == ByteOrder.LittleEndian ? "Little Endian" : "Big Endian";
                _output.WriteLine($"Current byte order: {current}");
                return Task.FromResult(0);
            }

            string param = parameters.Trim().ToLower();

            switch (param)
            {
                case "little":
                case "le":
                case "l":
                    context.ByteOrder = ByteOrder.LittleEndian;
                    _output.WriteLine("Byte order set to Little Endian");
                    break;

                case "big":
                case "be":
                case "b":
                    context.ByteOrder = ByteOrder.BigEndian;
                    _output.WriteLine("Byte order set to Big Endian");
                    break;

                default:
                    _output.WriteLine("Invalid parameter. Use: endian [little|big]");
                    _output.WriteLine("  Shortcuts: le/l for Little Endian, be/b for Big Endian");
                    return Task.FromResult(1);
            }

            return Task.FromResult(0);
        }
    }
}
