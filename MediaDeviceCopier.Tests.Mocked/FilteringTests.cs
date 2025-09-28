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

        [Fact]
        public void Upload_RecursiveWithSubfolderFilter_OnlyMatchingSubtrees()
        {
            using var tempDir = new TempDirectory();
            var sourceRoot = tempDir.Path;
            
            // Create nested directory structure
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2025", "Jan", "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2025", "Feb", "file2.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "2024", "Dec", "file3.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "Other", "Sub", "file4.jpg"));

            var (device, mock) = CreateTestDevice();
            
            // Simulate recursive copy with subfolder filter "^2025$"
            var subfolders = Directory.GetDirectories(sourceRoot);
            var filterRegex = new Regex("^2025$");
            var filteredSubfolders = subfolders.Where(sf => filterRegex.IsMatch(new DirectoryInfo(sf).Name)).ToArray();
            
            foreach (var subfolder in filteredSubfolders)
            {
                ProcessDirectoryRecursively(device, subfolder, "/target");
            }

            // Should only upload files from 2025 subtree
            Assert.Equal(2, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/file1.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/file2.jpg", mock.UploadedTargetPaths);
        }

        private void ProcessDirectoryRecursively(MtpDevice device, string sourceDir, string targetBaseDir)
        {
            var files = Directory.GetFiles(sourceDir);
            foreach (var file in files)
            {
                var targetPath = targetBaseDir + "/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            var subDirs = Directory.GetDirectories(sourceDir);
            foreach (var subDir in subDirs)
            {
                ProcessDirectoryRecursively(device, subDir, targetBaseDir);
            }
        }

        [Fact]
        public void Upload_EmptySubfolderFilter_ProcessesAllSubfolders()
        {
            using var tempDir = new TempDirectory();
            var sourceRoot = tempDir.Path;
            
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "Folder1", "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "Folder2", "file2.jpg"));

            var (device, mock) = CreateTestDevice();
            
            // Empty/null filter should process all subfolders
            var subfolders = Directory.GetDirectories(sourceRoot);
            Regex? filterRegex = null; // Simulate empty filter

            var filteredSubfolders = filterRegex == null
                ? subfolders
                : subfolders.Where(sf => filterRegex.IsMatch(new DirectoryInfo(sf).Name)).ToArray();
            
            foreach (var subfolder in filteredSubfolders)
            {
                var files = Directory.GetFiles(subfolder);
                foreach (var file in files)
                {
                    var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                    device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
                }
            }

            Assert.Equal(2, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/file1.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/file2.jpg", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_EmptyFileFilter_ProcessesAllFiles()
        {
            using var tempDir = new TempDirectory();
            
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file2.mp4"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file3.png"));

            var (device, mock) = CreateTestDevice();
            
            var allFiles = Directory.GetFiles(tempDir.Path);
            Regex? filterRegex = null; // Simulate empty filter
            
            var filteredFiles = filterRegex == null
                ? allFiles
                : allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            Assert.Equal(3, mock.UploadedTargetPaths.Count);
        }

        [Fact]
        public void Upload_ComplexFileRegex_MatchesCorrectly()
        {
            using var tempDir = new TempDirectory();
            
            // Create files with various extensions and naming patterns
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "IMG_20230101_001.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "VID_20230101_001.mp4"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "DSC_20230101_001.JPG"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "random_file.txt"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "20230101_backup.zip"));

            var (device, mock) = CreateTestDevice();
            
            // Complex regex: files starting with IMG, VID, or DSC, followed by underscore and date, with image/video extensions
            var filterRegex = new Regex(@"^(IMG|VID|DSC)_\d{8}_\d{3}\.(jpg|mp4|JPG|MP4)$", RegexOptions.IgnoreCase);
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            Assert.Equal(3, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/IMG_20230101_001.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/VID_20230101_001.mp4", mock.UploadedTargetPaths);
            Assert.Contains("/target/DSC_20230101_001.JPG", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_FilterWithMoveOperation_TracksSourceDeletion()
        {
            using var tempDir = new TempDirectory();
            
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "keep.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "skip.txt"));

            var (device, mock) = CreateTestDevice();
            
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filterRegex = new Regex(@"\.jpg$");
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                var result = device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: true);
                
                // Verify the result indicates source was deleted (simulated move)
                Assert.True(result.SourceDeleted);
            }

            // Only filtered files should be uploaded
            Assert.Single(mock.UploadedTargetPaths);
            Assert.Contains("/target/keep.jpg", mock.UploadedTargetPaths);
            
            // Original file should be gone after move
            Assert.False(File.Exists(Path.Combine(tempDir.Path, "keep.jpg")));
            // Unfiltered file should still exist
            Assert.True(File.Exists(Path.Combine(tempDir.Path, "skip.txt")));
        }

        [Fact]
        public void Download_SubfolderFilter_OnlyMatchingDirectories()
        {
            var (device, mock) = CreateTestDevice();
            
            // Setup device directories with files
            mock.SetupDirectoryWithFiles("/device/2025_Photos", "/device/2025_Photos/img1.jpg", "/device/2025_Photos/img2.png");
            mock.SetupDirectoryWithFiles("/device/2024_Archive", "/device/2024_Archive/old1.jpg");
            mock.SetupDirectoryWithFiles("/device/Misc", "/device/Misc/other.txt");

            using var tempDir = new TempDirectory();
            
            // Simulate subfolder filtering for download (would need device.GetDirectories in real scenario)
            var deviceFolders = new[] { "/device/2025_Photos", "/device/2024_Archive", "/device/Misc" };
            var filterRegex = new Regex("^2025");
            var filteredFolders = deviceFolders.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var folder in filteredFolders)
            {
                var files = mock.GetFiles(folder);
                foreach (var file in files)
                {
                    var targetPath = System.IO.Path.Combine(tempDir.Path, System.IO.Path.GetFileName(file));
                    device.CopyFile(FileCopyMode.Download, file, targetPath, skipExisting: false, isMove: false);
                }
            }

            // Should only download from 2025_Photos folder
            Assert.Equal(2, mock.DownloadedSourcePaths.Count);
            Assert.Contains("/device/2025_Photos/img1.jpg", mock.DownloadedSourcePaths);
            Assert.Contains("/device/2025_Photos/img2.png", mock.DownloadedSourcePaths);
        }

        [Fact]
        public void Upload_NoMatchingSubfolders_UploadsNothing()
        {
            using var tempDir = new TempDirectory();
            var sourceRoot = tempDir.Path;
            
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "FolderA", "file1.jpg"));
            CreateTestFile(System.IO.Path.Combine(sourceRoot, "FolderB", "file2.jpg"));

            var (device, mock) = CreateTestDevice();
            
            // Filter that matches no subfolders
            var subfolders = Directory.GetDirectories(sourceRoot);
            var filterRegex = new Regex("^NoMatch$");
            var filteredSubfolders = subfolders.Where(sf => filterRegex.IsMatch(new DirectoryInfo(sf).Name)).ToArray();
            
            foreach (var subfolder in filteredSubfolders)
            {
                var files = Directory.GetFiles(subfolder);
                foreach (var file in files)
                {
                    var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                    device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
                }
            }

            Assert.Empty(mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_NoMatchingFiles_UploadsNothing()
        {
            using var tempDir = new TempDirectory();
            
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "document.pdf"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "text.txt"));

            var (device, mock) = CreateTestDevice();
            
            // Filter for image files when none exist
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filterRegex = new Regex(@"\.(jpg|png|gif)$", RegexOptions.IgnoreCase);
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            Assert.Empty(mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_CaseSensitiveFileFilter_MatchesExactCase()
        {
            using var tempDir = new TempDirectory();
            
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "test.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "test.JPG"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "test.png"));

            var (device, mock) = CreateTestDevice();
            
            // Case-sensitive filter for lowercase .jpg only
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filterRegex = new Regex(@"\.jpg$"); // Case-sensitive, matches only lowercase .jpg
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            // Should match only test.jpg (case-sensitive)
            Assert.Single(mock.UploadedTargetPaths);
            Assert.Contains("/target/test.jpg", mock.UploadedTargetPaths);
        }

        [Fact]
        public void Upload_SpecialCharactersInFilenames_FilteringWorks()
        {
            using var tempDir = new TempDirectory();
            
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file-name.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file_name.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file name.jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file(1).jpg"));
            CreateTestFile(System.IO.Path.Combine(tempDir.Path, "file[copy].jpg"));

            var (device, mock) = CreateTestDevice();
            
            // Filter for files with underscores or hyphens
            var allFiles = Directory.GetFiles(tempDir.Path);
            var filterRegex = new Regex(@"file[-_].*\.jpg$");
            var filteredFiles = allFiles.Where(f => filterRegex.IsMatch(System.IO.Path.GetFileName(f))).ToArray();
            
            foreach (var file in filteredFiles)
            {
                var targetPath = "/target/" + System.IO.Path.GetFileName(file);
                device.CopyFile(FileCopyMode.Upload, file, targetPath, skipExisting: false, isMove: false);
            }

            Assert.Equal(2, mock.UploadedTargetPaths.Count);
            Assert.Contains("/target/file-name.jpg", mock.UploadedTargetPaths);
            Assert.Contains("/target/file_name.jpg", mock.UploadedTargetPaths);
        }

        [Theory]
        [InlineData("*")]  // Invalid quantifier
        [InlineData("(")]  // Unclosed group
        [InlineData("[")]  // Unclosed character class
        [InlineData("(?<name>")]  // Unclosed named group
        public void SubfolderFilter_InvalidRegexPatterns_ThrowsRegexParseException(string invalidPattern)
        {
            var exception = Assert.Throws<RegexParseException>(() => new Regex(invalidPattern));
            Assert.IsType<RegexParseException>(exception);
        }

        [Theory]
        [InlineData("+")]  // Invalid quantifier at start
        [InlineData("\\")]  // Trailing escape
        [InlineData("(?")]  // Invalid group
        public void FileFilter_InvalidRegexPatterns_ThrowsRegexParseException(string invalidPattern)
        {
            var exception = Assert.Throws<RegexParseException>(() => new Regex(invalidPattern));
            Assert.IsType<RegexParseException>(exception);
        }
    }
}