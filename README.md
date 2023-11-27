# BLEConsole
Windows command-line tool for interacting with Bluetooth LE devices

![alt text](https://raw.githubusercontent.com/sensboston/BLEConsole/master/BLEConsole/BLEConsole.png)

## [Install via ClickOnce](https://senssoft.com/BLEConsole/BLEConsole.application)

### Requirements:

Windows 10, BT 4.0 adapter

### Console commands:

- **help**, **?**                      : show help information
- **quit**, **q**                      : quit from application
- **list**, **ls** [w]                 : show available BLE devices
- **open** <name> or <#>           : connect to BLE device
- **timeout** <sec>                    : show/change connection timeout, default value is 3 sec
- **delay** <msec>                 : pause execution for a certain number of milliseconds
- **close**                        : disconnect from currently connected device
- **stat**, **st**                     : shows current BLE device status
- **print**, **p** <text&vars>*     : prints text and variables to stdout, where are variables are:
	* %id : BlueTooth device ID
	* %addr : device BT address
	* %mac : device MAC address
	* %name : device BlueTooth name
	* %stat : device connection status
	* %NOW, %now, %HH, %hh, %mm, %ss, %D, %d, %T, %t, %z : date/time variables
- **format** [data_format], **fmt**    : show/change display format, can be ASCII/UTF8/Dec/Hex/Bin
- **set** <service_name> or <#>    : set current service (for read/write operations)
- **read**, **r** <name>**              : read value from specific characteristic
- **write**, **w** <name>** <value>     : write value to specific characteristic
- **subs** <name>**                 : subscribe to value change for specific characteristic
- **unsubs** <name>** [all]         : unsubscribe from value change for specific characteristic or unsubs all for all
- **wait** : wait <timeout> seconds for notification event on value change (you must be subscribed, see above)
- **foreach** [device_mask]        : starts devices enumerating loop
- **endfor**                       : end foreach loop<br/>
- **if** <cmd> <params>            : start conditional block dependent on function returning w/o error
     - **elif**                      : another conditionals block
     - **else**                      : if condition == false block
- **endif**			   : end conditional block
	
  _* you can also use standard C language string formating characters like \\t, \\n etc._
  
  _** <name> could be "service/characteristic", or just a char name or # (for selected service)_

### Example of usage:

#### Lookup, connect and print all BLE devices names

**BLEConsole.exe < cmd.txt**, where is cmd.txt is a simple text file with content:

```
foreach 
	if open $
		read #0/#0
		close
	endif
endfor
```

### Below is an example of interactive use of the BLEConsole:

You can use BT name or # provided by **list** command. For example, run BLEConsole, type **ls** and it should list available BT devices, like
```
BLE: ls
#00: F2
#01: TOZO-S2
```
Than use command **open #1** or **open TOZO-S2** (you can also use partial name, like TOZ if no more BLE devices with tat name exist), you'll get an output like
```
BLE: open #1
Connecting to TOZO-S2.
Found 3 services:
#00: GenericAccess
#01: GenericAttribute
#02: 2800
```

Now you can set active service and list characteristics, by issuing command **set #0** 
```
BLE: set #0
Selected service GenericAccess.
#00: DeviceName RW
#01: Appearance R
#02: PeripheralPreferredConnectionParameters    R
#03: 10918      R
```

Now you can read characteristic by # or name, like **read #0**
```
BLE: read #0
TOZO-S2
```

If you already knew your service name/#, you can avoid previous step and read characteristic after successful  connection to BLE device, like **read #1/#0**

