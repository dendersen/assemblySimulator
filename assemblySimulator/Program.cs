using assemblySimulator;
using System.Diagnostics;
using System.Reflection;
public class MainClass
{
    [STAThread]
    public static void Main()
    {
        List<Type> loadedInstruction = [];
        InstructionLoader.PickNewInstructionSet(typeof(InstructionSet), ref loadedInstruction);
        if (loadedInstruction.Count == 0)
        {
            Console.Error.WriteLine("No instruction set loaded. Exiting.");
            return;
        }
        for (int i = 0; i < loadedInstruction.Count; i++)
        {
            Debug.WriteLine($"Loaded instruction: {{{i}}}", loadedInstruction[i].Name);
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

        Debug.WriteLine($"memBytesPerAddress: {memBytesPerAddress}");
        
        bool autoTest = false;
        UInt64 memLength = 0xFFFF;
        string? filePath = null;
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
                if (autoTest)
                {
                    autoTest = false;
                    Console.WriteLine("Auto test disabled.");
                }
                else
                {
                    autoTest = true;
                    Console.WriteLine("Auto test enabled.");
                }
            }
            else if (option == "file")
            {
                Console.WriteLine("Enter file path:");
                filePath = FilePicker.ChooseFile("Select a binary file to load into memory");
            }
            else if (option == "memtest")
            {
                TestMem(memBytesPerAddress, memLength != 0 ? memLength : 0xFFFF);
            }
            else if (option == "setmem")
            {
                Console.WriteLine("Enter memory length (in address count):");
                string? memLengthStr = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(memLengthStr))
                {
                    Console.WriteLine("No memory length entered.");
                    continue;
                }
                if (memLengthStr.StartsWith("0x"))
                {
                    memLengthStr = memLengthStr[2..];
                    memLength = Convert.ToUInt64(memLengthStr, 16);
                    continue;
                }
                else if (memLengthStr.StartsWith("0b"))
                {
                    memLengthStr = memLengthStr[2..];
                    memLength = Convert.ToUInt64(memLengthStr, 2);
                    continue;
                }
                else if (memLengthStr.StartsWith("0o"))
                {
                    memLengthStr = memLengthStr[2..];
                    memLength = Convert.ToUInt64(memLengthStr, 8);
                    continue;
                }
                else 
                {
                    memLength = Convert.ToUInt64(memLengthStr, 10);
                }
                Console.WriteLine($"Memory length set to: {memLength:X} addresses");
                Console.WriteLine($"Memory length in bytes: {memLength * (UInt64)memBytesPerAddress:X}");
            }
            else if (option == "sizeFromFile")
            {
                memLength = 0;
                Console.WriteLine("Memory length will be determined after loading the file(s).");
            }
            else if (option == "help")
            {
                Console.WriteLine("test: execute the tests in subdirectories of the 'tests' directory, type again to disable auto test");
                Console.WriteLine("file: select a binary file to load into memory");
                Console.WriteLine("memtest: run the memory module test, making sure all values are correctly stored and retrieved");
                Console.WriteLine("setmem: set the memory length to a prespecified value");
                Console.WriteLine("sizeFromFile: set the memory length to the size of the file");
                Console.WriteLine("q: quit the options menu, execute the instruciton set");
                Console.WriteLine("help: display this help message");
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
            RunTest(loadedInstruction[0], memBytesPerAddress, memLength);
            return;
        }
        RunArchitecture(loadedInstruction[0], filePath, memBytesPerAddress, memLength);
    }
    private static InstructionSet? RunArchitecture(Type loadedInstruction, string? testFile, short memBytesPerAddress, UInt64 memLength, bool printDump = true)
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
    public static void RunTest(Type InstructionSet, short memBytesPerAddress, UInt64 memLength = 0)
    {
        String[] testGroups = {"task1", "task2", "task3", "task4"};
        foreach (String testGroup in testGroups)
        {
            Console.WriteLine("Running tests for: " + testGroup);
            foreach (String test in Directory.GetFiles("tests/" + testGroup, "*.bin"))
            {
                try
                {
                    Console.WriteLine("Running test: " + test);
                    InstructionSet? instruct = RunArchitecture(InstructionSet, test, memBytesPerAddress, memLength, false);
                    if (instruct == null)
                    {
                        Console.WriteLine("Test \e[41mfailed\e[0m: " + test);
                        Console.WriteLine("Instruction set failed to generate");
                        continue;
                    }
                    byte[] regDump = instruct.RegDump();
                    byte[] regTarget = ReadBinaryFile(test.Replace(".bin",".res"));
                    if (regDump.Length != regTarget.Length || !regDump.SequenceEqual(regTarget))
                    {
                        Console.WriteLine("Test \e[41mfailed\e[0m: " + test);
                    }
                    else
                    {
                        Console.WriteLine("Test \e[42mpassed\e[0m: " + test);
                    }
                    Console.WriteLine("type 'y' to print dump");
                    string? response = Console.ReadLine();
                    if (response?.ToLower() == "y")
                    {
                        instruct.Dump();
                        Console.WriteLine("regComparison:");
                        for (int i = 0; i < regDump.Length; i += 4)
                        {
                            UInt32 wordGot = BitConverter.ToUInt32(regDump, i);
                            UInt32 wordExpected = BitConverter.ToUInt32(regTarget, i);
                            Console.Write($"reg[{i/4:D2}]: 0x{wordGot:X8}");
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
                catch (Exception ex)
                {
                    Console.WriteLine("Test \e[41mfailed\e[0m: " + test);
                    Console.WriteLine("Test encountered an unhandled exception.");
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    return;
                }
            }
        }
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