# MediaDeviceCopier

MediaDeviceCopier is a Windows command-line utility for copying files to and from phones and other devices connected via MTP (Media Transfer Protocol). It's a .NET 9.0 C# console application with full unit test coverage using mocked devices.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Bootstrap and Build
- Install .NET 9.0 SDK (REQUIRED - project targets net9.0-windows):
  - `curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir ~/.dotnet`
  - `export PATH="$HOME/.dotnet:$PATH"`
  - Verify: `dotnet --version` (should show 9.0.x)
- Restore packages: `dotnet restore` -- takes 2-10 seconds. **NEVER CANCEL**. Set timeout to 10+ minutes.
- Build Debug: `dotnet build` -- takes 3-8 seconds. **NEVER CANCEL**. Set timeout to 10+ minutes.
- Build Release: `dotnet build --configuration Release` -- takes 3-4 seconds. **NEVER CANCEL**. Set timeout to 10+ minutes.

### Testing
- Run mocked tests only: `dotnet test MediaDeviceCopier.Tests.Mocked` -- takes 2-4 seconds. **NEVER CANCEL**. Set timeout to 10+ minutes.
- Run all tests: `dotnet test` -- takes 2-5 seconds, but real device tests WILL FAIL on non-Windows platforms (expected behavior). **NEVER CANCEL**. Set timeout to 10+ minutes.
- ALWAYS run mocked tests to validate core logic changes.

### Run the Application
- Show help: `dotnet run --project MediaDeviceCopier -- --help`
- List devices: `dotnet run --project MediaDeviceCopier -- list-devices` (Windows only - fails on Linux/macOS with NullReferenceException)
- Commands: list-devices (l), upload-files (u), download-files (d)

## Platform Requirements
- **Development**: Works on any platform with .NET 9.0 SDK for building and mocked testing
- **Runtime functionality**: Windows ONLY - requires MTP support via MediaDevices library
- On non-Windows platforms, application builds and CLI help works, but device operations fail with NullReferenceException (expected)

## Manual Validation Scenarios

After making any code changes, ALWAYS run these complete validation scenarios:

### Scenario 1: Core Development Workflow (Required for all changes)
```bash
# Complete validation sequence - run ALL steps
export PATH="$HOME/.dotnet:$PATH"
dotnet restore                                # Should complete in 2-10s
dotnet build                                  # Should complete in 3-8s  
dotnet build --configuration Release         # Should complete in 3-4s
dotnet test MediaDeviceCopier.Tests.Mocked  # Should pass 2 tests in 2-4s
dotnet run --project MediaDeviceCopier -- --help  # Should show help output
dotnet format                                # Fix any formatting issues
```

### Scenario 2: CLI Command Validation (Required for CLI changes)
```bash
# Test all CLI command help outputs
dotnet run --project MediaDeviceCopier -- --help
dotnet run --project MediaDeviceCopier -- list-devices --help
dotnet run --project MediaDeviceCopier -- upload-files --help  
dotnet run --project MediaDeviceCopier -- download-files --help
```

### Scenario 3: Windows Device Testing (Windows only - for device logic changes)
```bash
# Only run on Windows with MTP device connected
dotnet run --project MediaDeviceCopier -- list-devices  # Should list connected devices
# Test actual upload/download with real device (use test files)
```

### Expected Results:
- All builds must succeed without warnings
- Mocked tests must pass (2/2 tests successful)
- CLI help commands must display proper usage information
- Real device tests will fail on non-Windows (expected)
- Code formatting must pass `dotnet format --verify-no-changes`

## Validation
- ALWAYS run `dotnet test MediaDeviceCopier.Tests.Mocked` after code changes to ensure core logic works
- Test CLI help command: `dotnet run --project MediaDeviceCopier -- --help` 
- ALWAYS run `dotnet format` before committing to fix whitespace formatting issues
- Verify build succeeds in both Debug and Release configurations
- On Windows, test actual device functionality with `dotnet run --project MediaDeviceCopier -- list-devices`

## Code Quality
- Run `dotnet format` to fix whitespace formatting issues (project has mixed tabs/spaces)
- Check formatting compliance: `dotnet format --verify-no-changes` (will fail if formatting needed)
- All mocked unit tests must pass - they validate core file copying logic without requiring real devices

## Project Structure

### Main Projects
- **MediaDeviceCopier**: Console application (.exe) with CLI commands
- **MediaDeviceCopier.Tests.Mocked**: Unit tests using mock devices (work on any platform)  
- **MediaDeviceCopier.Tests.RealDevice**: Integration tests requiring real MTP devices (Windows only)

### Key Files
- `MediaDeviceCopier/Program.cs`: CLI command setup and main entry point
- `MediaDeviceCopier/MtpDevice.cs`: Core MTP device wrapper and file operations
- `MediaDeviceCopier/MediaDeviceWrapper.cs`: Device abstraction layer
- `MediaDeviceCopier.Tests.Mocked/MockMediaDevice.cs`: Mock device implementation for testing

## Common Commands Reference

```bash
# Environment setup
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 9.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"

# Build and test workflow
dotnet restore
dotnet build
dotnet test MediaDeviceCopier.Tests.Mocked
dotnet format

# Run application  
dotnet run --project MediaDeviceCopier -- --help
dotnet run --project MediaDeviceCopier -- list-devices  # Windows only

# Release build
dotnet build --configuration Release
```

## Expected Timings
- Package restore: 2-10 seconds (**NEVER CANCEL** - set 10+ minute timeout)
- Debug build: 3-8 seconds (**NEVER CANCEL** - set 10+ minute timeout)
- Release build: 3-4 seconds (**NEVER CANCEL** - set 10+ minute timeout)
- Mocked tests: 2-4 seconds (**NEVER CANCEL** - set 10+ minute timeout)
- Real device tests: 2-5 seconds (fail on non-Windows)
- Code formatting: 1-2 seconds

## Troubleshooting
- **"NETSDK1045" error**: Install .NET 9.0 SDK (project requires net9.0-windows)
- **NullReferenceException on list-devices**: Expected on non-Windows platforms - MTP requires Windows
- **Test failures in RealDevice tests**: Expected on non-Windows - use mocked tests instead
- **Whitespace formatting errors**: Run `dotnet format` to fix automatically
- **Build timeout**: Increase timeout to 10+ minutes, **NEVER CANCEL** builds mid-process - even if they appear to hang, wait for completion