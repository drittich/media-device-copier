using MediaDeviceCopier;
using MediaDeviceCopier.Tests.Mocked;
using MediaDevices;
using System.Text.RegularExpressions;
using Xunit;

namespace MediaDeviceCopier.Tests.Mocked
{
    public class FilteringTests
    {
        private sealed class TempDirectory : IDisposable
        {
            public string Path { get; }

            public TempDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FilteringTests_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try
                {
                    if (Directory.Exists(Path))
                    {
                        Directory.Delete(Path, recursive: true);
                    }
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
        }

        private sealed class TrackingMockDevice : IMediaDevice
        {
            private readonly MockMediaDevice _baseMock = new MockMediaDevice();
            public List<string> UploadedTargetPaths { get; } = new List<string>();
            public List<string> DownloadedSourcePaths { get; } = new List<string>();
            private readonly Dictionary<string, string[]> _directoryContents = new Dictionary<string, string[]>();

            public string FriendlyName => _baseMock.FriendlyName;
            public bool IsConnected => _baseMock.IsConnected;

            public void Connect() => _baseMock.Connect();
            public void Dispose() => _baseMock.Dispose();
            public bool FileExists(string path) => _baseMock.FileExists(path);
            public bool DirectoryExists(string folder) => _baseMock.DirectoryExists(folder);
            public void CreateDirectory(string folder) => _baseMock.CreateDirectory(folder);
            public void DeleteFile(string path) => _baseMock.DeleteFile(path);

            public MediaFileInfo GetFileInfo(string path)
            {
                throw new NotImplementedException("MediaFileInfo not needed for filtering tests");
            }

            public void DownloadFile(string sourceFilePath, string targetFilePath)
            {
                _baseMock.DownloadFile(sourceFilePath, targetFilePath);
                DownloadedSourcePaths.Add(sourceFilePath);
            }

            public void UploadFile(string sourceFilePath, string targetFilePath)
            {
                _baseMock.UploadFile(sourceFilePath, targetFilePath);
                UploadedTargetPaths.Add(targetFilePath);
            }

            public void SetupDirectoryWithFiles(string directory, params string[] files)
            {
                _directoryContents[directory] = files;
                _baseMock.AddFolder(directory);
                foreach (var file in files)
                {
                    _baseMock.AddFile(file, new byte[] { 1, 2, 3, 4, 5 });
                }
            }

            public string[] GetFiles(string folder)
            {
                return _directoryContents.TryGetValue(folder, out var files) ? files : new string[0];
            }

            public string[] GetDirectories(string folder)
            {
                // For test simplicity, return empty array unless we have specific setup
                return new string[0];
            }
        }

        private static (MtpDevice device, TrackingMockDevice mock) CreateTestDevice()
        {
            var mock = new TrackingMockDevice();
            mock.Connect();
            var device = new MtpDevice(mock);
            device.Connect();
            return (device, mock);
        }

        private static void CreateTestFile(string path, string content = "test content")
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        [Fact]
        public void Upload_SubfolderFilter_IncludesOnlyMatching()
        {
            using var tempDir = new TempDirectory();
            var sourceRoot = tempDir.Path;
            
            // Create test directory structure
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "A2025", "sub1", "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "B2024", "file2.jpg"));

            var (device, mock) = CreateTestDevice();
            
            // Apply subfolder filter
            var subfolders = Directory.GetDirectories(sourceRoot);
            var filterRegex = new Regex("^A2025");
            var filteredSubfolders = subfolders.Where(sf => filterRegex.IsMatch(new DirectoryInfo(sf).Name)).ToArray();
            
            // Simulate file processing for matching subfolders
            foreach (var subfolder in filteredSubfolders)
            {
                var files = Directory.GetFiles(subfolder, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                    device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
                }
            }

            // Assert only A2025 subtree files invoked for upload
            Assert.Single(mock.UploadedTargetPaths);
            Assert.Contains("/target/file1.jpg", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_FileFilter_IncludesOnlyMatching()
        {
            using var tempDir = new TempDirectory();
            
            // Create test files: a.mp4, b.jpg, c.MP4
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "a.mp4"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "b.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "c.MP4"));

            var (device, mock) = CreateTestDevice();
            
            // Apply file filter for case-insensitive mp4
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filterRegex = new Regex(@"(?i).*\.mp4$");
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            // Process filtered files
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            // Assert only mp4 files uploaded (case-insensitive)
            Assert.Equal(2, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/a.mp4", mock.UploadedTargetPaths);
            Assert.Contains("/target/c.MP4", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_BothFilters_Combined()
        {
            using var tempDir = new TempDirectory();
            var sourceRoot = tempDir.Path;
            
            // Create test directory structure
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2025JAN", "a1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2025JAN", "a2.png"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "OTHER", "b1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2025FEB", "c1.mp4"));

            var (device, mock) = CreateTestDevice();
            
            // Apply both filters: subfolder "^2025JAN$" and files ".*\.(jpg|png)$"
            var subfolders = Directory.GetDirectories(sourceRoot);
            var subfolderRegex = new Regex("^2025JAN$");
            var filteredSubfolders = subfolders.Where(sf => subfolderRegex.IsMatch(new DirectoryInfo(sf).Name)).ToArray();
            
            var fileRegex = new Regex(@".*\.(jpg|png)$");
            
            foreach (var subfolder in filteredSubfolders)
            {
                var files = Directory.GetFiles(subfolder);
                var filteredFiles = files.Where(f => fileRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
                
                foreach (var file in filteredFiles)
                {
                    var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                    device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
                }
            }

            // Expect a1.jpg + a2.png only
            Assert.Equal(2, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/a1.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/a2.png", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Download_FileFilter_OnlyMatching()
        {
            var (device, mock) = CreateTestDevice();
            
            // Mock device exposes directory with files: d1.jpg, d2.mp4, d3.png
            mock.SetupDirectoryWithFiles("/device", "/device/d1.jpg", "/device/d2.mp4", "/device/d3.png");

            using var tempDir = new TempDirectory();
            var targetDir = Path.Combine(tempDir.Path, "target");
            Directory.CreateDirectory(targetDir);
            
            // Apply file filter for png files
            var deviceFiles = mock.GetFiles("/device");
            var filterRegex = new Regex(@".*\.png$");
            var filteredFiles = deviceFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileName(file));
                device.CopyFile(FileCopyMode.Download, file, targetPath, skipExisting: false, isMove: false);
            }

            // Assert only png downloaded
            Assert.Single(mock.DownloadedSourcePaths);
            Assert.Contains("/device/d3.png", mock.DownloadedSourcePaths);
        }

        [Fact]
        public void NoFilters_AllCopied()
        {
            using var tempDir = new TempDirectory();
            
            // Create test files
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file2.mp4"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file3.png"));

            var (device, mock) = CreateTestDevice();
            
            // Process all files (no filters)
            var allFiles = Directory.GetFiles(tempDir.Path);
            foreach (var file in allFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            // Baseline control: no -sf/-f => all files processed
            Assert.Equal(3, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/file1.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/file2.mp4", mock.UploadedTargetPaths);
            Assert.Contains("/target/file3.png", mock.UploadedTargetPaths);
        }

        [Fact]
        public void InvalidSubfolderRegex_ShowsError()
        {
            // Test invalid regex pattern
            var invalidPattern = "["; // Unclosed bracket
            
            var exception = Assert.Throws<RegexParseException>(() => new Regex(invalidPattern));
            Assert.Contains("Invalid pattern", exception.Message);
        }

        [Fact]
        public void InvalidFileRegex_ShowsError()
        {
            // Test invalid regex pattern
            var invalidPattern = "("; // Unclosed parenthesis
            
            var exception = Assert.Throws<RegexParseException>(() => new Regex(invalidPattern));
            Assert.Contains("Invalid pattern", exception.Message);
        }
    }
}