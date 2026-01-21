using System;
using System.Linq;
using System.Threading.Tasks;
using BLEConsole.Core;
using BLEConsole.Enums;

namespace BLEConsole.Commands.ConfigCommands
{
    public class FormatCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "format";
        public string[] Aliases => new[] { "fmt" };
        public string Description => "Show or change data format";
        public string Usage => "format [ASCII|UTF8|Dec|Hex|Bin]  (multiple formats supported with '+')";

        public FormatCommand(IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
            {
                // Show current format
                _output.WriteLine($"Current send data format: {context.SendDataFormat}");
                _output.WriteLine($"Current received data format: {string.Join("+", context.ReceivedDataFormats)}");
                return Task.FromResult(0);
            }

            // Parse format(s)
            var formats = parameters.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .ToList();

            context.ReceivedDataFormats.Clear();

            foreach (var formatStr in formats)
            {
                if (Enum.TryParse<DataFormat>(formatStr, true, out var format))
                {
                    context.SendDataFormat = format;
                    context.ReceivedDataFormats.Add(format);
                }
                else
                {
                    _output.WriteLine($"Unknown format: {formatStr}");
                    _output.WriteLine("Valid formats: ASCII, UTF8, Dec, Hex, Bin");
                    return Task.FromResult(1);
                }
            }

            _output.WriteLine($"Data format set to: {string.Join("+", context.ReceivedDataFormats)}");
            return Task.FromResult(0);
        }
    }
}
