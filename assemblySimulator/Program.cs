using assemblySimulator;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
public class MainClass
{
    [STAThread]
    public static void Main()
    {
        List<Type> loadedInstruction = [];
        while(loadedInstruction.Count == 0){
            InstructionLoader.PickNewInstructionSet(typeof(InstructionSet), ref loadedInstruction);
            for (int i = 0; i < loadedInstruction.Count; i++)
            {
                Debug.WriteLine($"Loaded instruction: {{{i}}}", loadedInstruction[i].Name);
            }
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
        
        Mem mem = new((UInt64)0xFFFF, memBytesPerAddress, 1000);
        OsHandler os = new(mem, null, null);
        InstructionSet instruct;
        {
            InstructionSet? _instruct = (InstructionSet?)Activator.CreateInstance(loadedInstruction[0], mem, os);
            if (_instruct == null)
            {
                Console.Error.WriteLine("Error: Could not create InstructionSet instance.");
                return;
            }
            instruct = (InstructionSet)_instruct;
        }
        mem.FillMem(ReadBinaryFile(null));
        while (true)
        {
            UInt64 instruction0 = mem.Read(instruct.GetPC());
            instruct.IncrimentPC();
            instruct.ExecuteInstruction(instruction0);
        }
    }
    public static byte[] ReadBinaryFile(string? filePath)
    {
        if (filePath == null)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Binary files(*.bin) | *.bin",
                Title = "Select a binary file to load into memory"
            };
            if (dlg.ShowDialog() != DialogResult.OK)
            {
                return [];
            }
            filePath = dlg.FileName;
        }
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file '{filePath}' does not exist.");
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