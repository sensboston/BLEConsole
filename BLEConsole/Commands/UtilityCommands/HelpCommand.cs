using System.Reflection;
using System.Threading.Tasks;
using BLEConsole.Core;

namespace BLEConsole.Commands.UtilityCommands
{
    public class HelpCommand : ICommand
    {
        private readonly IOutputWriter _output;

        public string Name => "help";
        public string[] Aliases => new[] { "?" };
        public string Description => "Show available commands";
        public string Usage => "help";

        public HelpCommand(CommandRegistry registry, IOutputWriter output)
        {
            _output = output;
        }

        public Task<int> ExecuteAsync(BleContext context, string parameters)
        {
            var name = Assembly.GetCallingAssembly().GetName();
            var versionInfo = $"{name.Name} ver. {name.Version.Major}.{name.Version.Minor}";

            _output.WriteLine(versionInfo +
                "\n\n  help, ?\t\t\t: show help information\n" +
                "  quit, q\t\t\t: quit from application\n" +
                "  list, ls [w]\t\t\t: show available BLE devices\n" +
                "  open <name|#|addr> [pin]\t: connect to BLE device, with optional pairing PIN\n" +
                "  delay <msec>\t\t\t: pause execution for a certain number of milliseconds\n" +
                "  timeout <sec>\t\t\t: show/change connection timeout, default value is 3 sec\n" +
                "  close\t\t\t\t: disconnect from currently connected device\n" +
                "  stat, st\t\t\t: shows current BLE device status\n" +
                "  print, p <text&vars>*\t\t: prints text and variables to stdout, where are variables are\n" +
                "  \t\t\t\t: %id - BlueTooth device ID\n" +
                "  \t\t\t\t: %addr - device BT address\n" +
                "  \t\t\t\t: %mac - device MAC address\n" +
                "  \t\t\t\t: %name - device BlueTooth name\n" +
                "  \t\t\t\t: %stat - device connection status\n" +
                "  \t\t\t\t: %NOW, %now, %HH, %hh, %mm, %ss, %D, %d, %T, %t, %z - date/time variables\n" +
                "  format [data_format], fmt\t: show/change display format for received and sent data, can be ASCII/UTF8/Dec/Hex/Bin\n" +
                "  format_send [data_format],\n  fmts\t\t\t\t: show/change data format for sending data, can be ASCII/UTF8/Dec/Hex/Bin\n" +
                "  format_rec [data_format,...],\n  fmtr\t\t\t\t: show/change display format for received data, comma separated list of type ASCII/UTF8/Dec/Hex/Bin\n" +
                "  endian [little|big], bo\t: show/change byte order (endianness) for read/write, default is Little Endian\n" +
                "  set <service_name> or <#>\t: set current service (for read/write operations)\n" +
                "  read, r <name>**\t\t: read value from specific characteristic\n" +
                "  write, w <name>**<value>\t: write value to specific characteristic\n" +
                "  subs <name>**\t\t\t: subscribe to value change for specific characteristic\n" +
                "  unsubs [<name>**|all]\t\t: unsubscribe from specific characteristic, or all subscriptions (unsubs = unsubs all)\n" +
                "  wait\t\t\t\t: wait for notification event on value change (you must be subscribed, see above)\n" +
                "  pair [<mode> [<params>]]\t: pair the currently connected BLE device\n" +
                "  \t\t\t\t: no mode               just pair\n" +
                "  \t\t\t\t: mode=ProvidePin <pin> pair with supplied pin \n" +
                "  \t\t\t\t: mode=ConfirmOnly      pair and confirm\n" +
                "  \t\t\t\t: mode=ConfirmPinMatch  pair and confirm that pin matches\n" +
                "  \t\t\t\t: mode=DisplayPin       pair and confirm that displayed pin matches\n" +
                "  \t\t\t\t: mode=ProvidePasswordCredential <username> <password>\n" +
                "  \t\t\t\t                        pair with supplied username and password\n" +
                "  unpair \t\t\t: unpair currently connected BLE device\n" +
                "  foreach [device_mask]\t\t: starts devices enumerating loop\n" +
                "  endfor\t\t\t: end foreach loop\n" +
                "  if <cmd> <params>\t\t: start conditional block dependent on function returning w\\o error\n" +
                "    elif\t\t\t: another conditionals block\n" +
                "    else\t\t\t: if condition == false block\n" +
                "  endif\t\t\t\t: end conditional block\n\n" +
                "   * You can also use standard C language string formating characters like \\t, \\n etc. \n" +
                "  ** <name> could be \"service/characteristic\", or just a char name or # (for selected service)\n\n" +
                "  For additional information and examples please visit https://github.com/sensboston/BLEConsole \n"
            );

            return Task.FromResult(0);
        }
    }
}
