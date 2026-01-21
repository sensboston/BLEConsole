# BLEConsole Changelog

## Version 2.0.0 - Major Refactoring & New Features

### 🏗️ Architecture Improvements

**Phase 1: Modularization**
- Split monolithic `Helpers.cs` (1131 lines) into 15 specialized modules
- Created clean directory structure:
  - `Core/` - Application infrastructure (BleContext, IOutputWriter)
  - `Models/` - Data models
  - `Enums/` - All enumerations (6 files)
  - `Utilities/` - Helper functions (5 files)
  - `Commands/` - Command Pattern implementation
  - `Services/` - BLE service abstractions

**Phase 2: Command Pattern**
- Introduced Command Pattern to replace 400+ line switch statement
- Each command is now a separate, testable class
- Easy to add new commands without modifying existing code
- Auto-generated help from registered commands

**Phase 3: New BLE Features**
- Services layer for BLE operations
- Enhanced GATT commands with better error handling

### ✨ New Features

**New Commands:**
- `desc <characteristic>` - List descriptors for a characteristic
- `read-desc <char>/<desc>` (aliases: `rd`) - Read descriptor value
- `write-desc <char>/<desc> <value>` (aliases: `wd`) - Write descriptor value
- `read-all [service]` (aliases: `ra`) - Batch read all characteristics in a service
- `device-info` (aliases: `di`, `info`) - Auto-read Device Information Service
- `mtu` - Show current MTU (Maximum Transmission Unit) size
- `write -nr <char> <value>` - Write without response (faster, no ACK)

**Enhanced Commands:**
- `read` - Improved error handling and format support
- `write` - Now supports both write with/without response
- `help` - Auto-generates from registered commands
- `list` - Improved formatting
- `stat` - Enhanced device status display

### 🔧 Technical Improvements

- **Testability**: Commands can now be unit tested independently
- **Maintainability**: Clear separation of concerns
- **Extensibility**: Simple pattern for adding new features
- **Error Handling**: Consistent error reporting across all commands
- **Async/Await**: Proper async patterns throughout

### 📦 Backward Compatibility

- 100% backward compatible with existing scripts
- Old functionality preserved via compatibility layer in `Helpers.cs`
- No breaking changes to command syntax

### 🎯 Design Patterns

- **Command Pattern** - For command dispatch
- **Strategy Pattern** - For data formatting
- **Dependency Injection** - For testability
- **Single Responsibility** - Each class has one job

### 📊 Statistics

- **45+ files** in organized structure
- **2500+ lines** of refactored code
- **15 command classes** (5 utility/device, 7 GATT, 3 config)
- **Zero breaking changes**

### 🎁 Quick Win Bundle Features

**Complete Descriptor Support:**
- Read/write any GATT descriptor (not just CCCD)
- Simple syntax: `read-desc Characteristic/Descriptor`
- Supports lookup by name, number, or UUID

**Device Information Helper:**
- Automatically reads all DIS characteristics
- Manufacturer, Model, Serial Number, Revisions, System ID, PnP ID
- Formatted output with proper data types

**Enhanced Properties Display:**
- Extended from 4 to 10 property flags
- Format: `RWwNIBAEra` (uppercase = common, lowercase = rare)
- Shows all GATT characteristic properties

**Batch Operations:**
- `read-all` reads all characteristics in one command
- Displays results in formatted table
- Success/failure summary with error details

---

## 📖 New Commands - Detailed Guide

### 🔍 `device-info` - Device Information Service Reader

**Description:** Automatically discovers and reads Device Information Service (DIS) characteristics if available on the device.

**Syntax:**
```
device-info
di          # Short alias
info        # Alternative alias
```

**What it reads:**
- Manufacturer Name String
- Model Number String
- Serial Number String
- Hardware Revision String
- Firmware Revision String
- Software Revision String
- System ID
- PnP ID (IEEE 11073-20601 Regulatory Certification Data List)

**Example Usage:**
```
BLE: open MyDevice
BLE: device-info

Device Information:
==================
  Manufacturer        : Acme Corporation
  Model Number        : BLE-Sensor-v3
  Serial Number       : SN123456789
  Hardware Revision   : 1.0
  Firmware Revision   : 2.1.5
  Software Revision   : 2.1.5-beta
  System ID           : 0x123456FFFE789ABC
  PnP ID              : 0x01...
```

**Use Cases:**
- **Device Identification**: Quickly identify device manufacturer and model
- **Firmware Verification**: Check firmware version before updates
- **Inventory Management**: Collect device information for asset tracking
- **Debugging**: Verify you're connected to the correct device
- **Compliance**: Read regulatory certification data

**Note:** Returns "Device Information Service not found" if device doesn't implement DIS (many custom devices don't).

---

### 📊 `read-all` - Batch Read All Characteristics

**Description:** Reads all readable characteristics in a service at once and displays results in a formatted table.

**Syntax:**
```
read-all              # Read all chars in currently selected service
read-all <service>    # Read all chars in specified service
ra                    # Short alias
```

**Example Usage:**
```
BLE: open MyDevice
BLE: set #1           # Select GenericAccess service
BLE: read-all

Reading 3 characteristic(s) from 'GenericAccess':
--------------------------------------------------------------------------------
  #00: DeviceName                               [  11 bytes] MySensorDevice
  #01: Appearance                               [   2 bytes] 0x0000
  #02: CentralAddressResolution                 [   1 bytes] 0x01
--------------------------------------------------------------------------------
Summary: 3 succeeded, 0 failed
```

**Advanced Usage:**
```
BLE: read-all GenericAccess    # Read without selecting service first
BLE: read-all #2               # Read all chars from service #2
```

**Use Cases:**
- **Quick Service Inspection**: See all data in a service at once
- **Data Collection**: Gather all sensor readings in one operation
- **Comparison**: Compare multiple characteristics side-by-side
- **Debugging**: Quickly identify which characteristics contain data
- **Configuration Review**: Check all settings/parameters at once
- **Performance**: Single command instead of multiple individual reads

**Output Format:**
- Index number for reference
- Characteristic name (or UUID if custom)
- Data size in bytes
- Formatted value (in current display format)
- Success/failure summary with counts

---

### 📋 `desc` - List Characteristic Descriptors

**Description:** Lists all GATT descriptors for a specific characteristic.

**Syntax:**
```
desc <characteristic>
descriptors <characteristic>   # Full name alias
```

**Example Usage:**
```
BLE: set #0
BLE: desc HeartRateMeasurement

Descriptors for characteristic 'HeartRateMeasurement':
  #00: ClientCharacteristicConfiguration (CCCD)
  #01: CharacteristicUserDescription

BLE: desc #3    # By number
```

**Common Descriptors:**
- **CCCD (0x2902)**: Client Characteristic Configuration - enables notifications/indications
- **User Description (0x2901)**: Human-readable characteristic description
- **Presentation Format (0x2904)**: Data format information
- **Aggregate Format (0x2905)**: Groups related characteristics
- **Valid Range (0x2906)**: Min/max valid values
- **External Report Reference (0x2907)**: Links to external reports
- **Report Reference (0x2908)**: HID report mapping
- **Number of Digitals (0x2909)**: Digital signal count
- **Trigger Setting (0x290A)**: Trigger condition configuration

**Use Cases:**
- **Notification Setup**: Find CCCD descriptor to enable notifications
- **Metadata Discovery**: Read characteristic descriptions and formats
- **Protocol Understanding**: Understand characteristic configuration options
- **Debugging**: Verify descriptor presence for notify/indicate characteristics

---

### 📖 `read-desc` - Read Descriptor Value

**Description:** Reads the value of a specific GATT descriptor.

**Syntax:**
```
read-desc <char>/<descriptor>
rd <char>/<descriptor>         # Short alias
```

**Lookup Methods:**
- By name: `read-desc HeartRate/CCCD`
- By number: `read-desc #0/#0`
- By UUID: `read-desc 2A37/2902`
- Mixed: `read-desc HeartRate/#0`

**Example Usage:**
```
BLE: set #0
BLE: read-desc HeartRate/CCCD
hex: 01 00

BLE: read-desc #0/UserDescription
utf8: Heart Rate Measurement

BLE: read-desc #2/2904    # By UUID
Presentation Format descriptor read successfully
```

**Common Use Cases:**

**1. Check Notification Status:**
```
BLE: read-desc SensorData/CCCD
hex: 01 00    # Notifications enabled
hex: 00 00    # Notifications disabled
hex: 02 00    # Indications enabled
```

**2. Read Characteristic Description:**
```
BLE: read-desc Temperature/UserDescription
utf8: Temperature in Celsius
```

**3. Check Data Format:**
```
BLE: read-desc Pressure/PresentationFormat
Format: float, Exponent: -1, Unit: Pascal
```

**CCCD Values:**
- `00 00` - Notifications and indications disabled
- `01 00` - Notifications enabled
- `02 00` - Indications enabled
- `03 00` - Both enabled (rare)

---

### ✍️ `write-desc` - Write Descriptor Value

**Description:** Writes a value to a specific GATT descriptor.

**Syntax:**
```
write-desc <char>/<descriptor> <value>
wd <char>/<descriptor> <value>          # Short alias
```

**Example Usage:**

**1. Enable Notifications:**
```
BLE: write-desc HeartRate/CCCD 01 00
Wrote 2 bytes to descriptor

# Now notifications are enabled for HeartRate characteristic
```

**2. Disable Notifications:**
```
BLE: write-desc HeartRate/CCCD 00 00
Wrote 2 bytes to descriptor
```

**3. Enable Indications:**
```
BLE: write-desc BatteryLevel/CCCD 02 00
Wrote 2 bytes to descriptor
```

**Common Use Cases:**

**Notification Control:**
```
# Enable notifications manually instead of using 'subs' command
BLE: write-desc SensorData/CCCD 01 00

# Disable when done
BLE: write-desc SensorData/CCCD 00 00
```

**Configuration:**
```
# Write custom descriptor values for device configuration
BLE: write-desc CustomChar/CustomDescriptor FF AA 55
```

**Note:** CCCD is the most commonly written descriptor. Other descriptors are usually read-only.

---

### 📡 `mtu` - Show MTU Size

**Description:** Displays the current Maximum Transmission Unit (MTU) size for the BLE connection.

**Syntax:**
```
mtu
```

**Example Usage:**
```
BLE: open MyDevice
BLE: mtu

Current MTU: 517 bytes
Effective payload: 514 bytes (MTU - 3 byte header)
```

**MTU Sizes:**
- **23 bytes**: Default minimum MTU (BLE 4.0)
- **27 bytes**: Common on older devices
- **185 bytes**: Common negotiated size
- **247 bytes**: Common maximum for BLE 4.2
- **512-517 bytes**: Extended MTU (BLE 5.0+)

**Why MTU Matters:**

**1. Data Transfer Speed:**
```
MTU 23:  Can send 20 bytes per packet
MTU 247: Can send 244 bytes per packet (12x faster!)
MTU 517: Can send 514 bytes per packet (25x faster!)
```

**2. Efficient Bulk Transfers:**
```
# With MTU 517, you can send larger write operations:
BLE: write DataCharacteristic <512 bytes of data>  # Single packet!

# With MTU 23, same data requires 26 packets
```

**3. Notification Performance:**
```
High MTU = More data per notification
         = Fewer notifications needed
         = Better battery life
         = Lower latency
```

**Use Cases:**
- **Performance Troubleshooting**: Check if MTU negotiation succeeded
- **Optimization**: Verify high MTU for bulk data transfer
- **Protocol Design**: Design payload sizes based on MTU
- **Debugging**: Understand packet fragmentation issues

**Tips:**
- MTU is negotiated during connection
- Both devices must support higher MTU
- iOS typically supports 185 bytes
- Android can support up to 517 bytes
- Windows 10/11 supports up to 517 bytes

---

### ⚡ `write -nr` - Write Without Response

**Description:** Enhanced write command with `-nr` flag for fast writes without waiting for ACK.

**Syntax:**
```
write <characteristic> <value>           # Normal write (with response)
write -nr <characteristic> <value>       # Fast write (no response)
w -nr <characteristic> <value>           # Short alias
```

**Comparison:**

| Feature | `write` (with response) | `write -nr` (no response) |
|---------|------------------------|---------------------------|
| Speed | Slower (~50-100ms) | **Faster (~7-10ms)** |
| Reliability | Guaranteed delivery | Best effort |
| Use case | Critical data | Streaming data |
| Acknowledgment | Yes | No |
| Error detection | Yes | No |

**Example Usage:**

**Normal Write:**
```
BLE: write Configuration 01 02 03
Write successful (3 bytes)
```

**Fast Write:**
```
BLE: write -nr SensorStream AA BB CC DD EE
Write successful (5 bytes, no response)
```

**Use Cases:**

**1. Streaming Data:**
```
# Send 100 samples rapidly
foreach sample in samples:
    write -nr StreamData <sample>
endfor
```

**2. Real-time Control:**
```
# Mouse/gamepad control with low latency
write -nr MouseX <position>
write -nr MouseY <position>
write -nr ButtonState <state>
```

**3. LED Control:**
```
# Rapid color changes without waiting for ACK
write -nr LED_R FF
write -nr LED_G 00
write -nr LED_B 80
```

**4. Bulk Data Transfer:**
```
# Send file chunks quickly
write -nr DataChunk <chunk1>
write -nr DataChunk <chunk2>
write -nr DataChunk <chunk3>
...
```

**When to Use:**
- ✅ Streaming sensor data
- ✅ Real-time control (LED, motor, servo)
- ✅ High-frequency updates
- ✅ Non-critical data
- ❌ Configuration changes (use normal write)
- ❌ Important commands (use normal write)
- ❌ Data that must be verified (use normal write)

**Performance Example:**
```
# Normal write: 100 writes * 50ms = 5 seconds
BLE: write Data <value>  # Repeated 100 times

# Write without response: 100 writes * 7ms = 0.7 seconds (7x faster!)
BLE: write -nr Data <value>  # Repeated 100 times
```

---

### 💡 `help` - Enhanced Auto-Generated Help

**Description:** Improved help system that automatically generates command list from registered commands.

**Syntax:**
```
help              # Show all commands
help <command>    # Show detailed help for specific command
?                 # Short alias
```

**Example Usage:**

**List All Commands:**
```
BLE: help

Available commands:

  close                               - Disconnect from currently connected device
  desc                 (descriptors)  - List descriptors for a characteristic
  device-info          (di, info)     - Read Device Information Service
  format               (fmt)          - Show or change data format
  help                 (?)            - Show available commands
  list                 (ls)           - List available BLE devices
  mtu                                 - Show current MTU size
  open                                - Connect to BLE device
  quit                 (q, exit)      - Exit the application
  read                 (r)            - Read value from characteristic
  read-all             (ra)           - Read all characteristics in a service
  read-desc            (rd)           - Read descriptor value
  set                                 - Set current service
  stat                 (st, status)   - Show current device status
  subs                 (sub)          - Subscribe to notifications
  unsubs                              - Unsubscribe from notifications
  write                (w)            - Write value to characteristic
  write-desc           (wd)           - Write descriptor value

Type 'help <command>' for detailed usage information.
```

**Detailed Command Help:**
```
BLE: help read-all

Command: read-all
Aliases: ra
Description: Read all characteristics in a service
Usage: read-all [service_name]
```

**What's New:**
- ✨ **Auto-generated**: List updates automatically when commands are added
- ✨ **Shows aliases**: See all available shortcuts
- ✨ **Detailed help**: Get usage info for any command
- ✨ **Always current**: No manual documentation updates needed

---

## 🎯 Usage Scenarios

### Scenario 1: Quick Device Inspection
```bash
# Connect and gather all basic information
BLE: open MyDevice
BLE: device-info        # Get manufacturer info
BLE: mtu                # Check connection performance
BLE: list               # See all services
BLE: set #0             # Select first service
BLE: read-all           # Read all characteristics at once
```

### Scenario 2: Enable Sensor Notifications
```bash
# Traditional way (multiple commands)
BLE: open Sensor
BLE: set SensorService
BLE: read Temperature/#0    # Find CCCD
BLE: write Temperature/CCCD 01 00
BLE: subs Temperature

# New way (using descriptors)
BLE: open Sensor
BLE: set SensorService
BLE: desc Temperature       # List descriptors
BLE: write-desc Temperature/CCCD 01 00  # Enable notifications
BLE: subs Temperature
```

### Scenario 3: Bulk Data Collection
```bash
# Collect all data from multiple services
BLE: open DataLogger
BLE: read-all GenericAccess    # Service info
BLE: read-all BatteryService   # Battery data
BLE: read-all SensorService    # All sensor readings
BLE: read-all ConfigService    # Configuration

# All data collected in 4 commands instead of 20+!
```

### Scenario 4: High-Performance Streaming
```bash
# Setup
BLE: open StreamDevice
BLE: mtu                       # Verify high MTU (400+ bytes)
BLE: set StreamService

# Stream data rapidly
BLE: write -nr StreamData <chunk1>
BLE: write -nr StreamData <chunk2>
BLE: write -nr StreamData <chunk3>
# ... continues with minimal latency
```

### Scenario 5: Debugging Unknown Device
```bash
# Systematic exploration
BLE: open UnknownDevice
BLE: device-info              # Try to identify
BLE: ls                       # List all services

# Explore each service
BLE: set #0
BLE: read-all                 # Read all data
BLE: desc #0                  # Check descriptors
BLE: desc #1
BLE: desc #2

# Next service
BLE: set #1
BLE: read-all
# ... repeat for all services
```

### Scenario 6: Configuration Management
```bash
# Read current configuration
BLE: open ConfigDevice
BLE: set ConfigService
BLE: read-all                 # See all current settings

# Verify MTU before large config write
BLE: mtu                      # Check if we can send large payload

# Write configuration rapidly
BLE: write -nr Config1 <value>
BLE: write -nr Config2 <value>
BLE: write -nr Config3 <value>

# Verify changes
BLE: read-all                 # Check all settings updated
```

---

## Version 1.6.1 (Previous)
- Extended pairing/unpairing implementation
- PIN-based pairing tested and verified
