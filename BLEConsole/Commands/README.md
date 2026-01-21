# Command Pattern Architecture

## Overview
This directory contains the Command Pattern implementation for BLEConsole.
Each command is a separate class implementing `ICommand` interface.

## Structure
```
Commands/
├── ICommand.cs              # Command interface
├── CommandRegistry.cs       # Command registry and dispatcher
├── UtilityCommands/         # help, quit, etc.
├── DeviceCommands/          # list, open, close, stat
├── GattCommands/            # set, read, write, subs
└── ConfigCommands/          # format, timeout, delay
```

## Creating a New Command

1. Create a class implementing `ICommand`:
```csharp
public class MyCommand : ICommand
{
    public string Name => "mycommand";
    public string[] Aliases => new[] { "mc" };
    public string Description => "Does something cool";
    public string Usage => "mycommand <param>";

    public Task<int> ExecuteAsync(BleContext context, string parameters)
    {
        // Implementation here
        return Task.FromResult(0); // 0 = success
    }
}
```

2. Register it in Program.cs:
```csharp
registry.Register(new MyCommand(output));
```

## Benefits
- **Separation of Concerns**: Each command is isolated
- **Testable**: Commands can be unit tested independently
- **Extensible**: Easy to add new commands without modifying existing code
- **Maintainable**: Clear structure and responsibilities

## Current Status
Phase 2 - Partial implementation. Example commands created as proof of concept.
Full migration from Program.cs switch statement is in progress.
