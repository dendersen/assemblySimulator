using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace assemblySimulator
{
    class TestRunner
    {
        struct TestPath
        {
            public string name;
            public TestPath[]? children;
            public string[]? files;
        }
        private static void TestDumpHandler(bool printDump, bool autoDump, InstructionSet instruct, string testFile, byte[] regDump, byte[] regTarget, short memBytesPerAddress, UInt64 memLength)
        {
            if (printDump || autoDump)
                {
                    if (printDump)
                    {
                        instruct.Dump();
                        Console.WriteLine("regComparison:");
                    }
                    File.WriteAllBytes(testFile.Replace(".bin",".reg.dump"), regDump);
                    UInt64[] memDumpRaw = instruct.ReadArrayFromMem(0, 1, memLength * (UInt64)memBytesPerAddress);
                    byte[] memDumpBytes = new byte[memDumpRaw.Length * memBytesPerAddress];
                    for (int i = 0; i < memDumpRaw.Length; i++)
                    {
                        memDumpBytes[i] = (byte)(memDumpRaw[i] & 0xFF);
                    }
                    File.WriteAllBytes(testFile.Replace(".bin",".mem.dump"), memDumpBytes);
                    if (printDump)
                    {
                        for (int i = 0; i < regDump.Length; i += memBytesPerAddress)
                        {
                            UInt32 wordGot = BitConverter.ToUInt32(regDump, i);
                            UInt32 wordExpected = BitConverter.ToUInt32(regTarget, i);
                            Console.Write($"reg[{i/memBytesPerAddress:D2}]: 0x{wordGot:X8}");
                            if (wordGot != wordExpected)
                            {
                                Console.Write($" expected: 0x{wordExpected:X8}");
                            }
                            Console.WriteLine(",");
                        }
                        Console.WriteLine("press enter to continue...");
                        Console.ReadLine();
                    }
                }
        }
        private static bool RunSingleTest(Type InstructionSet, string testFile, string testCallingName, short memBytesPerAddress, UInt64 memLength, bool pauseOnFailure, bool pauseOnSuccess, bool autoDump)
        {
            bool success = false;
            try
            {
                Console.WriteLine("Running test: " + testCallingName);
                InstructionSet? instruct = MainClass.RunArchitecture(InstructionSet, testFile, memBytesPerAddress, memLength, false);
                if (instruct == null)
                {
                    Console.WriteLine("Test \e[41mfailed\e[0m: " + testCallingName);
                    Console.WriteLine("Instruction set failed to generate");
                    return success;
                }
                byte[] regDump = instruct.RegDump();
                
                byte[] regTarget = MainClass.ReadBinaryFile(testFile.Replace(".bin",".res"));
                if (regDump.Length != regTarget.Length || !regDump.SequenceEqual(regTarget))
                {
                    Console.WriteLine("Test \e[41mfailed\e[0m: " + testCallingName);
                    success = false;
                }
                else
                {
                    Console.WriteLine("Test \e[42mpassed\e[0m: " + testCallingName);
                    success = true;
                }
                string? response = "n";
                if (success && pauseOnSuccess)
                {
                    Console.WriteLine("type 'y' to print dump");
                    response = Console.ReadLine();
                }
                else if (!success && pauseOnFailure)
                {
                    Console.WriteLine("type 'y' to print dump");
                    response = Console.ReadLine();
                }
                bool printDump = response?.ToLower() == "y";
                TestDumpHandler(printDump, autoDump, instruct, testFile, regDump, regTarget, memBytesPerAddress, memLength);
            }
            catch (Exception ex)
            {
                success = false;
                Console.WriteLine("Test \e[41mfailed\e[0m: " + testCallingName);
                Console.WriteLine("Test encountered an unhandled exception.");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                return success;
            }
            return success;
        }
        /**
            Runs the tests for the specified instruction set.
            Parameters:
            - InstructionSet: The type of the instruction set to test.
            - memBytesPerAddress: The number of bytes per memory address.
            - memLength: The length of the memory.
            - pauseOnFailure: Whether to pause on test failure.
            - pauseOnSuccess: Whether to pause on test success.
            - autoDump: Whether to automatically dump the register and memory state at finish.
            - testPath: The path to the test files. If empty, defaults to DefaultTestSource, if not specified asks user, if specified uses the given path.
        */
        public static int RunTests(Type InstructionSet, short memBytesPerAddress, UInt64 memLength = 0, bool pauseOnFailure = true, bool pauseOnSuccess = true, bool autoDump = false, string? testPath = "")
        {
            int successCount = 0;
            int testCount = 0;
            string testSource = MainClass.DefaultTestSource;
            if (!string.IsNullOrWhiteSpace(testPath))
            {
                testSource = testPath;
            }
            else if (testPath == null)
            {
                string? userSource = FilePicker.ChooseFile("Select test directory", getDir: true);
                if (!string.IsNullOrWhiteSpace(userSource))
                {
                    testSource = userSource;
                }
                else
                {
                    Console.Error.WriteLine("No test directory selected. Using default: " + MainClass.DefaultTestSource);
                    testSource = MainClass.DefaultTestSource;
                }
            }
            {
                DirectoryInfo testDir = new(testSource);
                testSource = testDir.FullName;
            }
            Console.WriteLine("looking for tests in : " + testSource);
            
            string[] tests = ExposeTests(GetTests(testSource));
            string? prevTestGroup = null;

            for (int i = 0; i < tests.Length; i++)
            {
                string test = tests[i];
                string testRelativePath = Path.GetRelativePath(testSource, test);
                string testGroup = Path.GetDirectoryName(testRelativePath) ?? "";
                if (testGroup.Contains("/.")){
                    Debug.WriteLine("skipping " + testGroup);
                    continue;
                }
                if (prevTestGroup == null || prevTestGroup != testGroup)
                {
                    Console.WriteLine("Entering test group: " + testGroup);
                    Console.WriteLine();
                }
                prevTestGroup = testGroup;
                testCount++;
                if (RunSingleTest(InstructionSet, test, testRelativePath, memBytesPerAddress, memLength, pauseOnFailure, pauseOnSuccess, autoDump))
                {
                    successCount++;
                }
                Console.WriteLine();
            }
            Console.WriteLine("Finished running all tests.");
            Console.WriteLine($"Successful tests: {successCount} / {testCount}");
            return successCount;
        }
        private static string[] ExposeTests(TestPath tests)
        {
            List<string> result = [];
            if (tests.files != null)
            {
                result.AddRange(tests.files);
            }
            if (tests.children != null)
            {
                foreach (TestPath child in tests.children)
                {
                    foreach (string testName in ExposeTests(child))
                    {
                        result.Add(testName);
                    }
                }
            }
            return [.. result];
        }
        private static TestPath GetTests(string testSource)
        {
            StringComparer comparer = StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.NumericOrdering);
            DirectoryInfo testDir = new(testSource);
            string[] testFiles = [.. testDir.GetFiles("*.bin").Select(f => f.FullName)];
            string[] children = [.. testDir.GetDirectories().Select(d => d.FullName)];

            Array.Sort(testFiles, comparer);
            Array.Sort(children, comparer);

            TestPath ret = new()
            {
                name = testDir.FullName,
                files = testFiles.Length > 0 ? testFiles : null,
                children = children.Length > 0 ? [.. children.Select(c => GetTests(c))] : null
            };
            return ret;
        }
    }
}
