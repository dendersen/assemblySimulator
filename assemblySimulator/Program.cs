using assemblySimulator;
using System.Diagnostics;
using System.Reflection;
public class MainClass
{
    public const string DefaultTestSource = "./tests/";
    [STAThread]
    public static void Main()
    {
        using var debugLog = new TextWriterTraceListener("debug.log");
        Trace.Listeners.Add(debugLog);
        Trace.AutoFlush = true;
        Console.SetOut(new TeeTextWriter(Console.Out, debugLog.Writer));
        DebugLevel.AddType("OS", 1);

        List<Type> loadedInstruction = [];
        InstructionLoader.PickNewInstructionSet(typeof(InstructionSet), ref loadedInstruction);
        if (loadedInstruction.Count == 0)
        {
            Console.Error.WriteLine("No instruction set loaded. Exiting.");
            return;
        }
        for (int i = 0; i < loadedInstruction.Count; i++)
        {
            Console.WriteLine($"Loaded instruction: {{{i}}}", loadedInstruction[i].Name);
        }

        short memBytesPerAddress;
        // Retrieve the static fields from the loaded instruction set
        {
            Type thing = loadedInstruction[0];
            FieldInfo? memBytesPerAddress_field = thing.GetField("memBytesPerAddress");
            if (memBytesPerAddress_field == null)
            {
                Console.Error.WriteLine("memBytesPerAddress: missing");
                return;
            }
            short? _memBytesPerAddress = (short?)memBytesPerAddress_field.GetValue(null);
            if (_memBytesPerAddress == null)
            {
                Console.Error.WriteLine("Error: Could not retrieve memBytesPerAddress.");
                return;
            }
            memBytesPerAddress = (short)_memBytesPerAddress;
        }

        if (DebugLevel.IsDebugEnabled("OS"))
        {
            Debug.WriteLine($"memBytesPerAddress: {memBytesPerAddress}");
        }
        
        bool autoTest = false;
        bool pauseOnSuccess = false;
        bool pauseOnFailure = true;
        bool dumpTest = true;
        UInt64 memLength = 0xFFFF;
        bool pickFile = false;
        while(true)
        {
            Console.WriteLine("Options: test, memtest, setmem, sizeFromFile, q, help");
            string option = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(option))
            {
                Console.WriteLine("No option entered.");
                continue;
            }
            if (option == "test")
            {
                autoTest = !autoTest;
                Console.WriteLine("Auto test is now " + (autoTest ? "enabled" : "disabled"));
            }
            else if (option == "testSource")
            {
                pickFile = !pickFile;
                Console.WriteLine("Test source is now " + (pickFile ? "picked at start" : $"default ({DefaultTestSource})"));
            }
            else if (option == "memtest")
            {
                TestMem(memBytesPerAddress, memLength != 0 ? memLength : 0xFFFF);
            }
            else if (option == "setmem")
            {
                Console.WriteLine("Enter memory length (in address count):");
                string? memLengthStr = Console.ReadLine();
                try
                {
                    if (string.IsNullOrWhiteSpace(memLengthStr))
                    {
                        Console.WriteLine("No memory length entered.");
                    }else if (memLengthStr.StartsWith("0x"))
                    {
                        memLengthStr = memLengthStr[2..];
                        memLength = Convert.ToUInt64(memLengthStr, 16);
                    }
                    else if (memLengthStr.StartsWith("0b"))
                    {
                        memLengthStr = memLengthStr[2..];
                        memLength = Convert.ToUInt64(memLengthStr, 2);
                    }
                    else if (memLengthStr.StartsWith("0o"))
                    {
                        memLengthStr = memLengthStr[2..];
                        memLength = Convert.ToUInt64(memLengthStr, 8);
                    }
                    else 
                    {
                        memLength = Convert.ToUInt64(memLengthStr, 10);
                    }
                    Console.WriteLine($"Memory length set to: 0x{memLength:X} addresses ({memLength})");
                    Console.WriteLine($"Memory length in bytes: 0x{memLength * (UInt64)memBytesPerAddress:X} ({memLength * (UInt64)memBytesPerAddress})");
                }catch (FormatException ex)
                {
                    Console.WriteLine($"Invalid memory length format: {ex.Message}");
                }
            }
            else if (option == "sizeFromFile")
            {
                memLength = 0;
                Console.WriteLine("Memory length will be determined after loading the file(s).");
            }
            else if (option == "pausepass")
            {
                pauseOnSuccess = !pauseOnSuccess;
                Console.WriteLine("Pause on success is now " + (pauseOnSuccess ? "enabled" : "disabled"));
            }
            else if (option == "pausefail")
            {
                pauseOnFailure = !pauseOnFailure;
                Console.WriteLine("Pause on failure is now " + (pauseOnFailure ? "enabled" : "disabled"));
            }
            else if (option == "DUMP")
            {
                dumpTest = !dumpTest;
                Console.WriteLine("Dump for all test results is now " + (dumpTest ? "enabled" : "disabled"));
            }
            else if (option == "help")
            {
                Console.WriteLine("test: toggle wether to execute all tests in the 'tests' directory (currently " + (autoTest ? "enabled" : "disabled") + ")");
                Console.WriteLine("testSource: toggle whether to pick the test source at start or use the default './tests/' directory");
                Console.WriteLine("memtest: run the memory module test, making sure all values are correctly stored and retrieved");
                Console.WriteLine("setmem: set the memory length to a prespecified value");
                Console.WriteLine("sizeFromFile: set the memory length to the size of the file");
                Console.WriteLine("q: quit the options menu, execute the instruciton set");
                Console.WriteLine("help: display this help message");
                Debug.WriteLine(  "DEBUG: set debug level");
                Debug.WriteLine(  "DEBUGVAL: set a specific debug value to on for debugging purposes");
                Debug.WriteLine(  "DUMP: toggle whether to create a dump for all test results (currently " + (dumpTest ? "enabled" : "disabled") + ")");
                if (autoTest)
                {
                    Console.WriteLine("pausepass: toggle whether to pause on successful test execution (currently " + (pauseOnSuccess ? "enabled" : "disabled") + ")");
                    Console.WriteLine("pausefail: toggle whether to pause on failed test execution (currently " + (pauseOnFailure ? "enabled" : "disabled") + ")");
                }
            }
            else if (option == "DEBUG")
            {
                Console.WriteLine("Set debug level");
                string? debugLevelStr = Console.ReadLine();
                if (UInt32.TryParse(debugLevelStr, out UInt32 debugLevel))
                {
                    DebugLevel.SetDebugLevel(debugLevel);
                }
                else
                {
                    Console.WriteLine("Invalid debug level.");
                }
            }
            else if (option == "DEBUGVAL")
            {
                string[] existing = DebugLevel.GetAllDebugKeys();
                Console.WriteLine("Existing debug keys:");
                foreach (string key in existing)
                {
                    Console.WriteLine("  " + key);
                }
                Console.WriteLine("Enter a debug key (may be new) to enable, this will set level to 0 until changed by next add");
                string? debugKey = Console.ReadLine();
                if (!string.IsNullOrEmpty(debugKey))
                {
                    DebugLevel.EnableType(debugKey);
                }
            }
            else if (option == "q")
            {
                Console.WriteLine("Exiting options menu and starting program.");
                break;
            }
            else
            {
                Console.WriteLine($"Unknown option: {option}");
                continue;
            }
        }
        if (autoTest)
        {
            Console.WriteLine("Running in auto test mode with dumpTest " + (dumpTest ? "enabled" : "disabled"));
            TestRunner.RunTests(loadedInstruction[0], memBytesPerAddress, memLength, pauseOnFailure, pauseOnSuccess, dumpTest, pickFile ? null : ""); 
            return;
        }
        Console.WriteLine("Running with dumpTest " + (dumpTest ? "enabled" : "disabled"));
        RunArchitecture(loadedInstruction[0], null, memBytesPerAddress, memLength);
    }
    public static InstructionSet? RunArchitecture(Type loadedInstruction, string? testFile, short memBytesPerAddress, UInt64 memLength, bool printDump = true)
    {
        byte[] memFile;
        memFile = ReadBinaryFile(testFile);
        if (memLength == 0)
        {
            memLength = (UInt64)memFile.Length / (UInt64)memBytesPerAddress + (UInt64)((memFile.Length % memBytesPerAddress == 0) ? 0 : 1);
        }
        Mem mem = new(memLength, memBytesPerAddress, 1000);
        OsHandler os = new(mem, null, null);
        InstructionSet instruct;
        {
            InstructionSet? _instruct = (InstructionSet?)Activator.CreateInstance(loadedInstruction, mem, os);
            if (_instruct == null)
            {
                Console.Error.WriteLine("Error: Could not create InstructionSet instance.");
                return default!;
            }
            instruct = _instruct;
        }
        mem.FillMem(memFile);
        while (true)
        {
            if (instruct.Execute())
            {
                Console.WriteLine("Execution halted.");
                if (printDump)
                {
                    instruct.Dump();
                }
                break;
            }
        }
        return instruct;
    }
    
    public static byte[] ReadBinaryFile(string? filePath)
    {
        if (!String.IsNullOrEmpty(filePath))
        {
            return File.ReadAllBytes(filePath);
        }
        filePath = FilePicker.ChooseFile("Select a binary file to load into memory");
        if (String.IsNullOrEmpty(filePath))
        {
            Console.Error.WriteLine("No file selected. Exiting.");
            return [];
        }
        return File.ReadAllBytes(filePath);
    }
    public static UInt64 Mask(int bitCnt)
    {
        return UInt64.MaxValue >> (64-bitCnt);
    }
    public static bool TestMem(short maxByteCnt, UInt64 maxAddress)
    {
        for (int byteCnt = 0; byteCnt < maxByteCnt; byteCnt++)
        {
            Mem mem = new((UInt64)maxAddress, (short)byteCnt, 10);
            for (int i = 1; i < byteCnt; i++)
            {
                int offset = i * 8;
                for (int j = 0; (UInt64)j < maxAddress; j++)
                {
                    Console.Write("progress: {0}/{1}, write {2}/{3}    \r", i, byteCnt - 1, j + 1, maxAddress);
                    mem.Write((UInt64)j, (UInt64)j << offset);
                }
                for (int j = 0; (UInt64)j < maxAddress; j++)
                {
                    Console.Write("progress: {0}/{1}, read  {2}/{3}    \r", i, byteCnt - 1, j + 1, maxAddress);
                    Debug.Assert(mem.Read((UInt64)j) == (((UInt64)j << offset) & Mask(byteCnt * 8)));
                    if(!(mem.Read((UInt64)j) == (((UInt64)j << offset) & Mask(byteCnt * 8))))
                    {
                        return false;
                    }
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("done!");
        return true;
    }
}