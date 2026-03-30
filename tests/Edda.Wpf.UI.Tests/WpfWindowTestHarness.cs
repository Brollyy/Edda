using System;
using System.IO;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    internal static class WpfWindowTestHarness {
        private const string StartupOpenMapButtonId = "ButtonOpenMap";
        private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

        internal static void RunOpenedFixtureMapTest(Action<WpfUIDriver, string> testBody, Action<WpfUIDriver>? configureDriver = null) {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                configureDriver?.Invoke(driver);
                mapFolder = LaunchAndOpenFixtureMap(driver);
                testBody(driver, mapFolder);
            } finally {
                driver.Shutdown();
                SafeDeleteDirectory(mapFolder);
            }
        }

        internal static string LaunchAndOpenFixtureMap(WpfUIDriver driver) {
            var fixtureCopy = CreateFixtureMapCopy();
            LaunchAndOpenMap(driver, fixtureCopy);
            return fixtureCopy;
        }

        internal static void LaunchAndOpenMap(WpfUIDriver driver, string mapFolder) {
            driver.Launch();
            driver.WaitForIdle();
            driver.SetTestFileSelection(mapFolder);
            driver.ClickButton(StartupOpenMapButtonId);
            driver.WaitForMainWindow();
        }

        internal static string CreateFixtureMapCopy() {
            var fixtureSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative);
            Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

            var fixtureCopyPath = CreateTempOutputFolder("fixture-copy");
            CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
            return fixtureCopyPath;
        }

        internal static string CreateTempOutputFolder(string tag) {
            var outputPath = Path.Combine(Path.GetTempPath(), "Edda-WpfWindowTests", tag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        internal static string CreateAlphabeticMapFolder(string tag) {
            var mapFolder = Path.Combine(CreateTempOutputFolder(tag), "PickerFlowMap");
            Directory.CreateDirectory(mapFolder);
            return mapFolder;
        }

        internal static (string fixtureFolder, string simfilePath) CreateStepManiaImportFixture(string title) {
            var fixtureFolder = CreateTempOutputFolder("import-source");
            var songSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative, "song.ogg");
            var songFileName = "importsong.ogg";
            var songTargetPath = Path.Combine(fixtureFolder, songFileName);
            File.Copy(songSourcePath, songTargetPath, overwrite: true);

            var simfilePath = Path.Combine(fixtureFolder, "picker-import.sm");
            File.WriteAllText(simfilePath, BuildStepManiaSimfile(title, songFileName));
            return (fixtureFolder, simfilePath);
        }

        internal static string GetRepositoryRoot() {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null) {
                if (File.Exists(Path.Combine(current.FullName, "RagnarockEditor.sln"))) {
                    return current.FullName;
                }
                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root (RagnarockEditor.sln).");
        }

        internal static void SafeDeleteDirectory(string? directoryPath) {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return;
            }

            try {
                Directory.Delete(directoryPath, true);
            } catch {
                // Best effort cleanup for temporary test paths.
            }
        }

        private static void CopyDirectoryRecursively(string sourcePath, string destinationPath) {
            foreach (var sourceDirectory in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
                var relativePath = Path.GetRelativePath(sourcePath, sourceDirectory);
                Directory.CreateDirectory(Path.Combine(destinationPath, relativePath));
            }

            foreach (var sourceFilePath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories)) {
                var relativePath = Path.GetRelativePath(sourcePath, sourceFilePath);
                var destinationFilePath = Path.Combine(destinationPath, relativePath);
                var destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory)) {
                    Directory.CreateDirectory(destinationDirectory);
                }
                File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
            }
        }

        private static string BuildStepManiaSimfile(string title, string songFileName) {
            return
                $"#TITLE:{title};\n" +
                "#SUBTITLE:;\n" +
                "#ARTIST:Picker Artist;\n" +
                "#CREDIT:Picker Mapper;\n" +
                $"#MUSIC:{songFileName};\n" +
                "#OFFSET:0.000;\n" +
                "#BPMS:0.000=120.000;\n" +
                "#NOTES:\n" +
                "     dance-single:\n" +
                "     :\n" +
                "     Easy:\n" +
                "     3:\n" +
                "     0.000,0.000,0.000,0.000,0.000:\n" +
                "0000\n" +
                "1000\n" +
                "0000\n" +
                "0000\n" +
                ",\n" +
                "0000\n" +
                "0000\n" +
                "0100\n" +
                "0000\n" +
                ";\n";
        }
    }
}