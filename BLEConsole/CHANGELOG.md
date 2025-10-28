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

## Version 1.6.1 (Previous)
- Extended pairing/unpairing implementation
- PIN-based pairing tested and verified
