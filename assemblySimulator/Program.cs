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
        
        bool autoTets = false;
        UInt64 memLength = 0xFFFF;
        byte[] memFile;
        while(true)
        {
            string option = Console.ReadLine();
            if (option == "test")
            {
                autoTets = true;
            }
            else if (option == "memtest")
            {
                TestMem(memBytesPerAddress, 0xFFFF);
            }
            else if (option == "setmem")
            {
                Console.WriteLine("Enter memory length (in address count):");
                memLength = UInt64.Parse(Console.ReadLine());
            }
            else if (option == "sizeFromFile")
            {
                memLength = 0;
            }
            else if (option == "q")
            {
                break;
            }
            else
            {
                Console.WriteLine("Unknown option.");
                Console.WriteLine("Options: test, memtest, setmem, sizeFromFile, q");
                continue;
            }
        }
        if (autoTets)
        {
            RunTest();
            return;
        }
        memFile = ReadBinaryFile(null);
        if (memFile.Length == 0)
        {
            memLength = (UInt64)memFile.Length / (UInt64)memBytesPerAddress + (UInt64)((memFile.Length % memBytesPerAddress == 0) ? 0 : 1);
        }
        Mem mem = new(memLength, memBytesPerAddress, 1000);
        OsHandler os = new(mem, null, null);
        InstructionSet instruct;
        {
            InstructionSet? _instruct = (InstructionSet?)Activator.CreateInstance(loadedInstruction[0], mem, os);
            if (_instruct == null)
            {
                Console.Error.WriteLine("Error: Could not create InstructionSet instance.");
                return;
            }
            instruct = _instruct;
        }
        mem.FillMem(memFile);
        while (true)
        {
            instruct.Execute();
        }
    }
    public static void RunTest()
    {
        filePath = FilePicker.ChooseFile("Select a binary file to load into memory");
        // Implementation for running tests
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