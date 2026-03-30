using System;
using System.IO;
using Xunit;

namespace Edda.Wpf.UI.Tests {
    public class MainWindowEditorGridTests {
        private const string StartupOpenMapButtonId = "ButtonOpenMap";
        private const string SongNameTextBoxId = "txtSongName";
        private const string SongPlayerButtonId = "btnSongPlayer";
        private const string ScrollEditorId = "scrollEditor";
        private const string NotesStatsAllId = "notesStatsAll";
        private const string NotesStatsSelectedId = "notesStatsSelected";
        private const string ColumnStatsValue1Id = "columnStatsValue1";

        private const string FixtureMapFolderRelative = "tests/TestData/Wpf/MainWindow/FixtureMap";

        [Fact]
        public void AddNoteFromMenuUpdatesTotalAndColumnStats() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.ClickButton("columnStats");
                var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);
                var initialColumn1 = ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id);

                driver.SelectMenuItem("Edit>Add New>Note (column 1)");
                driver.WaitForIdle();

                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
                Assert.Equal(initialColumn1 + 1, ParseIntegerText(driver.GetText(ColumnStatsValue1Id), ColumnStatsValue1Id));
            });
        }

        [Fact]
        public void UndoAndRedoRestoreEditorGridNoteCount() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

                driver.SelectMenuItem("Edit>Add New>Note (column 2)");
                driver.WaitForIdle();
                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

                driver.SendKeyboardShortcut("Ctrl+Z");
                driver.WaitForIdle();
                Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

                driver.SendKeyboardShortcut("Ctrl+Y");
                driver.WaitForIdle();
                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

                driver.SendKeyboardShortcut("Ctrl+Z");
                driver.WaitForIdle();
                Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

                driver.SendKeyboardShortcut("Ctrl+Shift+Z");
                driver.WaitForIdle();
                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            });
        }

        [Fact]
        public void SelectAllAndDeleteRemoveAllNotesFromGrid() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Edit>Add New>Note (column 1)");
                driver.SelectMenuItem("Edit>Add New>Note (column 2)");
                driver.WaitForIdle();

                var allNotes = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);
                Assert.True(allNotes > 0);

                driver.SendKeyboardShortcut("Ctrl+A");
                driver.WaitForIdle();
                Assert.Equal(allNotes, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));

                driver.SendKeyboardShortcut("Delete");
                driver.WaitForIdle();
                Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
                Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            });
        }

        [Fact]
        public void CtrlAIsIgnoredDuringPlayback() {
            RunOpenedFixtureMapTest((driver, _) => {
                driver.SelectMenuItem("Edit>Add New>Note (column 1)");
                driver.SelectMenuItem("Edit>Add New>Note (column 2)");
                driver.WaitForIdle();

                Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("Ctrl+A");
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();

                Assert.Equal(0, ParseIntegerText(driver.GetText(NotesStatsSelectedId), NotesStatsSelectedId));
            });
        }

        [Fact]
        public void NumberKeyAddsNoteWhenSongIsPlaying() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("1");
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();

                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            });
        }

        [Fact]
        public void NumberKeyIsIgnoredWhenTextboxHasFocusDuringPlayback() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

                driver.SendKeyboardShortcut("Space");
                driver.WaitForIdle();
                driver.SetText(SongNameTextBoxId, driver.GetText(SongNameTextBoxId));
                driver.WaitForIdle();
                driver.SendKeyboardShortcut("1");
                driver.WaitForIdle();
                driver.ClickButton(SongPlayerButtonId);
                driver.WaitForIdle();

                Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            });
        }

        [Fact]
        public void RightClickOnHoveredNoteRemovesIt() {
            RunOpenedFixtureMapTest((driver, _) => {
                var initialAll = ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId);

                driver.ClickWithinElement(ScrollEditorId, 0.2, 0.8);
                driver.WaitForIdle();
                Assert.Equal(initialAll + 1, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));

                driver.MoveMouseWithinElement(ScrollEditorId, 0.2, 0.8);
                driver.RightClickWithinElement(ScrollEditorId, 0.2, 0.8);
                driver.WaitForIdle();
                Assert.Equal(initialAll, ParseIntegerText(driver.GetText(NotesStatsAllId), NotesStatsAllId));
            });
        }

        private static void RunOpenedFixtureMapTest(Action<WpfUIDriver, string> testBody) {
            var driver = new WpfUIDriver();
            string? mapFolder = null;

            try {
                mapFolder = LaunchAndOpenFixtureMap(driver);
                testBody(driver, mapFolder);
            } finally {
                driver.Shutdown();
                SafeDeleteDirectory(mapFolder);
            }
        }

        private static string LaunchAndOpenFixtureMap(WpfUIDriver driver) {
            var fixtureCopy = CreateFixtureMapCopy();

            driver.Launch();
            driver.WaitForIdle();
            driver.SetTestFileSelection(fixtureCopy);
            driver.ClickButton(StartupOpenMapButtonId);
            driver.WaitForMainWindow();

            return fixtureCopy;
        }

        private static string CreateFixtureMapCopy() {
            var fixtureSourcePath = Path.Combine(GetRepositoryRoot(), FixtureMapFolderRelative);
            Assert.True(Directory.Exists(fixtureSourcePath), $"MainWindow fixture map folder was not found: {fixtureSourcePath}");

            var fixtureCopyPath = CreateTempOutputFolder("grid-fixture");
            CopyDirectoryRecursively(fixtureSourcePath, fixtureCopyPath);
            return fixtureCopyPath;
        }

        private static string CreateTempOutputFolder(string tag) {
            var outputPath = Path.Combine(Path.GetTempPath(), "Edda-WpfEditorGridTests", tag, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputPath);
            return outputPath;
        }

        private static string GetRepositoryRoot() {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null) {
                if (File.Exists(Path.Combine(current.FullName, "RagnarockEditor.sln"))) {
                    return current.FullName;
                }
                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate repository root (RagnarockEditor.sln).");
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

        private static void SafeDeleteDirectory(string? directoryPath) {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath)) {
                return;
            }

            try {
                Directory.Delete(directoryPath, true);
            } catch {
                // Best effort cleanup for temporary test paths.
            }
        }

        private static int ParseIntegerText(string value, string controlId) {
            if (int.TryParse(value, out var parsed)) {
                return parsed;
            }

            throw new InvalidOperationException($"Expected integer text in '{controlId}', but got '{value}'.");
        }
    }
}