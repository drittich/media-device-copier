# MTP Download Strategy Architecture

## Current State Analysis

### Existing Implementation
- Location: [`MediaDeviceCopier/MtpDevice.cs`](MediaDeviceCopier/MtpDevice.cs:361)
- Current flow in [`TryResilientDownload()`](MediaDeviceCopier/MtpDevice.cs:361):
  1. Standard `_device.DownloadFile()` attempt
  2. Stream-based retry (delay + retry)
  3. Chunked download retry (delay + retry)
  4. Thumbnail resource extraction (delay + retry)
  
- All strategies execute for all file types (no extension-based gating)
- COM Exception 0x80004005 triggers fallback, other exceptions rethrow
- Failures result in `FileCopyStatus.SkippedBecauseUnsupported`

### Test Coverage Gaps
- No tests for resilient download strategy execution
- No tests for failure chain progression
- No tests validating strategy ordering
- [`FilteringTests.cs`](MediaDeviceCopier.Tests.Mocked/FilteringTests.cs:108) covers regex filtering only
- [`MoveTests.cs`](MediaDeviceCopier.Tests.Mocked/MoveTests.cs:9) covers move semantics only

### Known Issues
- THM files fail on GoPro devices (documented)
- WAV files reported failing by QA
- Generic failure handling lacks diagnostics
- Thumbnail strategy runs unconditionally (inefficient for non-visual formats)

## Approved Architecture

### Design Principles
1. **Universal Strategy Pipeline**: Extension-agnostic fallback sequence
2. **Graceful Degradation**: Each strategy failure triggers next attempt
3. **Diagnostic Transparency**: Log each strategy attempt with timing/error details
4. **Future Extensibility**: Classification hook for potential specialization
5. **No Telemetry** (deferred per approval)

### Strategy Abstraction

```csharp
// Strategy context passed to each attempt
public class DownloadStrategyContext
{
    public string SourceFilePath { get; init; }
    public string TargetFilePath { get; init; }
    public string FileExtension { get; init; }
    public IMediaDevice Device { get; init; }
}

// Strategy delegate signature
public delegate bool DownloadStrategy(DownloadStrategyContext context);
```

### Strategy Pipeline (Ordered)

1. **Standard**: Direct `_device.DownloadFile()` call
2. **StreamRetry**: Delay 100ms + retry (timing-sensitive files)
3. **ChunkedRetry**: Delay 200ms + retry (buffer issues)
4. **ThumbnailOrMetadata**: Delay 500ms + retry (last resort)

### Classification Hook (Optional/Future)

```csharp
// Lightweight file classifier for future specialization
public enum FileMediaClass
{
    Unknown,
    Image,      // jpg, png, gif, bmp, etc.
    Video,      // mp4, mov, avi, etc.
    Audio,      // wav, mp3, flac, etc.
    Metadata,   // thm, lrv, etc.
    Document    // pdf, txt, etc.
}

public static FileMediaClass ClassifyFile(string extension, ulong? size = null)
{
    // Simple extension mapping, size heuristics optional
    // Enables future per-class strategy customization without hardcoding
}
```

### Enhanced Diagnostics

Each strategy attempt logs:
- Strategy name
- Start timestamp
- Outcome (success/failure)
- Elapsed milliseconds
- Exception type and HResult (if COM exception)

Example output:
```
[Standard] Started
[Standard] Failed: COMException 0x80004005 (12ms)
[StreamRetry] Started
[StreamRetry] Success (156ms)
```

## Implementation Plan

### Phase 1: Core Refactor
1. Define `DownloadStrategyContext` and `DownloadStrategy` delegate
2. Create strategy collection in `MtpDevice`
3. Refactor `TryResilientDownload()` to iterate strategy list
4. Add per-strategy diagnostic logging with timing

### Phase 2: Classification Hook (Stub)
1. Add `FileMediaClass` enum
2. Implement `ClassifyFile()` with basic extension mapping
3. Store classification in context (unused initially)
4. Document extension points for future specialization

### Phase 3: Testing
1. Create `StrategyTestMock` in test project:
   - Scriptable failure/success sequences
   - Track invoked strategy names
2. Add test: Failure chain progression (fail N, succeed N+1)
3. Add test: All strategies fail → `SkippedBecauseUnsupported`
4. Add test: Move semantics unchanged after refactor
5. Add test: `skipExisting` bypasses strategy execution
6. Add test: Custom strategy order (inject list)
7. Update existing tests if needed

### Phase 4: Documentation
1. Update README with strategy pipeline explanation
2. Document extension-neutral philosophy
3. Add troubleshooting section for diagnostic log interpretation
4. Document classification hook for future contributors

## File Changes

### Modified Files
- [`MediaDeviceCopier/MtpDevice.cs`](MediaDeviceCopier/MtpDevice.cs:1)
  - Add strategy abstraction types
  - Add classification enum/method
  - Refactor `TryResilientDownload()` and helper methods
  - Enhanced diagnostic logging

### New Test Files
- `MediaDeviceCopier.Tests.Mocked/StrategyTests.cs`
  - Strategy execution tests
- `MediaDeviceCopier.Tests.Mocked/StrategyTestMock.cs`
  - Helper mock for strategy testing

### Updated Files
- [`README.md`](README.md:1)
  - Strategy pipeline documentation
  - Troubleshooting guide

## Risk Mitigation

### Risks
1. **Performance**: Multiple retries add latency on failures
   - Mitigation: Stop-on-first-success, configurable delays
2. **Behavior Change**: Existing edge cases may behave differently
   - Mitigation: Comprehensive test coverage, preserve move semantics
3. **Thumbnail Retry Waste**: Non-visual files still attempt
   - Mitigation: Classification hook enables future filtering

### Rollback Plan
- All changes in `TryResilientDownload()` and helpers only
- Existing `CopyFile()` interface unchanged
- Tests validate backward compatibility
- Git revert if regression detected

## Future Enhancements (Not in Scope)

1. Per-class strategy customization via classification
2. Adaptive reordering based on historical success rates
3. Configurable retry delays/timeouts
4. Telemetry counters for strategy effectiveness
5. Real WPD thumbnail resource API integration

## Success Criteria

- [ ] All strategies execute in defined order
- [ ] Stop-on-first-success behavior validated
- [ ] Diagnostic logs include timing and error details
- [ ] Test coverage ≥90% for strategy execution paths
- [ ] Move semantics preserved (existing tests pass)
- [ ] `skipExisting` optimization preserved
- [ ] README documents pipeline and troubleshooting
- [ ] No hardcoded extension checks (classification hook only)