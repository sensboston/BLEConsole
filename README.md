# BLEConsole
Windows command-line tool for interacting with Bluetooth LE devices

![alt text](https://github.com/sensboston/BLEConsole/blob/master/BLEConsole/BLEConsole.png)

## [Install via ClickOnce](http://senssoft.com/BLEConsole/BLEConsole.application)

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
