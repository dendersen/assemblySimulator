using assemblySimulator;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace assemblySimulator
{
    public class OsHandler
    {
        private TextReader stdIn;
        private TextWriter stdOut;
        private readonly bool isStdOutTerminal;
        private TextWriter[] altOut;
        internal Mem mem;
        private byte memMode = 0;
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        private const int STD_OUTPUT_HANDLE = -11;
        public static StreamWriter OpenNewConsole()
        {
            if (AllocConsole())
            {
                IntPtr stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                SafeFileHandle safeHandle = new SafeFileHandle(stdHandle, ownsHandle: false);
                FileStream fileStream = new FileStream(safeHandle, FileAccess.Write);
                return new StreamWriter(fileStream) { AutoFlush = true };
            }
            throw new InvalidOperationException("Failed to allocate console.");
        }
        public OsHandler(Mem mem, TextReader? stdIn, TextWriter[]? stdOut)
        {
            this.mem = mem;
            this.stdIn = stdIn ?? Console.In;
            this.isStdOutTerminal = false;
            if (stdOut == null || stdOut.Length == 0)
            {
                this.stdOut = Console.Out;
                altOut = [];
                this.isStdOutTerminal = true;
            }
            else
            {
                this.stdOut = stdOut[0];
                this.altOut = new TextWriter[stdOut.Length - 1];
                for (int i =  1; i < stdOut.Length; i++)
                {
                    this.altOut[i - 1] = stdOut[i];
                }
            }
        }
        public OsHandler(Mem mem, TextReader? stdIn, TextWriter? stdOut, TextWriter[]? altOut)
        {
            this.mem = mem;
            this.stdIn = stdIn ?? Console.In;
            this.stdOut = stdOut ?? Console.Out;
            this.isStdOutTerminal = stdOut == null;
            this.altOut = altOut ?? [];
        }
        /**
         * indicate how to read memory that is addressed in any size over 1 byte
         * 0b00 = only least significant byte <- default
         * 0b01 = only most significant byte
         * 0b01 = read all bytes from least significant to most significant
         * 0b11 = read all bytes from most significant to least significant
         */
        public void SetMemMode(byte memMode)
        {
            this.memMode = memMode;
        }
        /**
         * write a number of bytes from memory into the console
         */
        public void WriteConsole(UInt64 sourceAddress, int byteCount)
        {
            this.WriteConsole(sourceAddress, byteCount, 1);
        }
        /**
         * write a number of bytes with extra spacing from memory into the console
         * spacing is the number of valid write positions between each byte read from memory
         * if spacing is 0 it will read the same byte from memory multiple times
         */
        public void WriteConsole(UInt64 sourceAddress, int byteCount, int spacing)
        {
            for (int i = 0; i < byteCount; i++)
            {
                UInt64 offsetDistance = (UInt64)spacing * (UInt64)i;
                UInt64 addressOffset = 0;
                UInt64 ByteOffset = 0;
                switch (memMode)
                {
                    case 0b00:
                        addressOffset = offsetDistance;
                        break;
                    case 0b01:
                        addressOffset = offsetDistance;
                        ByteOffset = (UInt64)this.mem.bytesPerAddress - 1;
                        break;
                    case 0b10:
                        addressOffset = offsetDistance / (UInt64)mem.bytesPerAddress;
                        ByteOffset = (offsetDistance % (UInt64)mem.bytesPerAddress);
                        break;
                    case 0b11:
                        addressOffset = offsetDistance / (UInt64)mem.bytesPerAddress;
                        ByteOffset = (((UInt64)mem.bytesPerAddress - offsetDistance - 1) % (UInt64)mem.bytesPerAddress);
                        break;
                }
                WriteConsoleSingle(sourceAddress + addressOffset, (int)ByteOffset);
            }
        }
        /**
         * writes a string to the console starting at a specific address in memory
         * continues until a null byte is reached, or the end of memory is reached
         * 
         * to read exactly one byte from each address, set spacing negative
         * when abs of the negative number will equal the byte (lowest to highest) read from memory
         * otherwise spacing defines how many bytes to skip between each read from memory
         * 
         * quitOnFull swaps from quiting when a null byte is reached to quitting when a null address is reached
         * meaning it quits when the all memory in one address is 0, instead of when the read byte in an address is 0
         */
        public void WriteStringConsole(UInt64 startingAddress, int spacing, bool quitOnFull)
        {
            UInt64 address = startingAddress;
            int byteOffset = 0;
            if (spacing < 0)
            {
                byteOffset = -spacing - 1;
            }
            while (((this.mem.Read(address) & (0xFFu << (byteOffset * 8))) != 0 && !quitOnFull) || 
                   ((this.mem.Read(address) != 0) && quitOnFull))
            {
                WriteConsoleSingle(address, byteOffset);
                if (spacing < 0)
                {
                    address++;
                }
                else
                {
                    byteOffset += spacing;
                }
                while(byteOffset >= this.mem.bytesPerAddress)
                {
                    byteOffset -= this.mem.bytesPerAddress;
                    address++;
                }
            }
        }
        /**
         * write a single byte from memory at a specific byte offset into the 
         * this has no interation with the memMode, it will always readto the specified byte offset
         */
        public void WriteConsoleSingle(UInt64 sourceAddress, int byteOffset)
        {
            UInt64 memValue = mem.Read(sourceAddress);
            char outputChar = (char)((memValue >> (byteOffset * 8)) & 0xff);
            stdOut.Write(outputChar);
            foreach (TextWriter writer in this.altOut)
            {
                writer.Write(outputChar);
            }
        }
        private readonly string clearText = "\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n\n><\\/\\/\\/\\/\\/\\/\\/\\/\\/\\/\\/><\nconsole has been cleared\n><\\/\\/\\/\\/\\/\\/\\/\\/\\/\\/\\/><\n\n";
        /**
         * clear all text in console and reset cursor to top left position
         */
        public void ClearConsole()
        {
            if (this.isStdOutTerminal)
            {
                stdOut.Write("\x1b[2J\x1b[H");
            }
            else
            {
                stdOut.Write(clearText);
            }
            foreach (TextWriter writer in this.altOut)
            {
                writer.Write(clearText);
            }
        }
        /**
         * read a number of bytes from the console input into memory
         */
        public void ReadConsole(UInt64 TargetAddress, int byteCount)
        {
            this.ReadConsole(TargetAddress, byteCount, 1);
        }
        /**
         * read a number of bytes from the console input into memory with extra spacing
         * the spacing is the number of valid write positions between each byte
         * if spacing is 0 it will read the same byte from memory multiple times
         */
        public void ReadConsole(UInt64 TargetAddress, int byteCount, int spacing)
        {
            for (int i = 0; i < byteCount; i++)
            {

                UInt64 offsetDistance = (UInt64)spacing * (UInt64)i;
                UInt64 addressOffset = 0;
                UInt64 ByteOffset = 0;
                switch (memMode)
                {
                    case 0b00:
                        addressOffset = offsetDistance;
                        break;
                    case 0b01:
                        addressOffset = offsetDistance;
                        ByteOffset = (UInt64)this.mem.bytesPerAddress - 1;
                        break;
                    case 0b10:
                        addressOffset = offsetDistance / (UInt64)mem.bytesPerAddress;
                        ByteOffset = (offsetDistance % (UInt64)mem.bytesPerAddress);
                        break;
                    case 0b11:
                        addressOffset = offsetDistance / (UInt64)mem.bytesPerAddress;
                        ByteOffset = (((UInt64)mem.bytesPerAddress - offsetDistance - 1) % (UInt64)mem.bytesPerAddress);
                        break;
                }
                ReadConsoleSingle(TargetAddress + addressOffset, (int)ByteOffset);
            }
        }
        /**
         * read a single byte from the console input into a memory address at a specific byte offset
         * this has no interation with the memMode, it will always write to the specified byte offset
         */
        public void ReadConsoleSingle(UInt64 TargetAdress, int byteOffset)
        {
            char[] buffer = new char[1];
            if (stdIn.Read(buffer, 0, 1) == 0)
            {
                throw new Exception("No input available");
            }
            UInt64 memValue = (mem.Read(TargetAdress) & ~(UInt64)(0xff << (byteOffset * 8))) | ((UInt64)buffer[0]) << (byteOffset * 8);
            mem.Write(TargetAdress, memValue);
        }
    }
    abstract public class InstructionSet(ref Mem memTarget, OsHandler os)
    {
        protected Mem memTarget = memTarget;
        protected OsHandler os = os;
        protected DebugManager? debug;
        public static readonly short memBytesPerAddress = 0;
        public abstract void JumpOverride(UInt64 address);
        public abstract void JumpRelative(Int64 offset);

        public abstract void Execute();
        /**
         * tool for allowing other components to recieve the currently used opcode from instruction
         */
        public abstract UInt64 GetOpcode(UInt64 instruction);
        /**
         * tool for allowing other components to know which registers will be written to by this instruction
         */
        public abstract void IncrimentPC();
        /**
         * provide the address the PC currently points to
         */
        public abstract UInt64 GetPC();
        /**
         * allow reading from memory
         * formats the raw memory structure to fit into the number system of a UInt64
         */
        public abstract UInt64 ReadValueFromMem(UInt64 address, int byteCount);
        /**
         * allow writting to memory
         * formats the raw memory structure to fit into the number system of a UInt64
         */
        public abstract void WriteValueToMem(UInt64 address, UInt64 value, int byteCount);
        /**
         * allow writting an array into memory
         * formats the raw memory structure to fit into the number system of a UInt64
         */
        public abstract UInt64[] ReadArrayFromMem(UInt64 startAddress, int bytesPerIndex, UInt64 length);
        /**
         * allow reading an array from memory
         * formats the raw memory structure to fit into the number system of a UInt64
         */
        public abstract void WriteArrayToMem(UInt64 startAddress, UInt64[] value, int bytesPerIndex);
    }
    public class InstructionLoader: AssemblyLoadContext
    {
        public InstructionLoader() : base(isCollectible: true) { }
        protected override Assembly Load(AssemblyName assemblyName)
        {
            return null!; // fallback to default load context
        }
        public static bool PickNewInstructionSet(Type targetType, ref List<Type> loadedInstruction)
        {
            loadedInstruction ??= [];
            List<Type> pluginTypes = FindInstructions(targetType, loadedInstruction);

            Console.WriteLine($"Found {pluginTypes.Count} instruction sets:");
            Console.WriteLine($"{0}: exit picker");
            for (int index = 0; index < pluginTypes.Count; index++)
            {
                var type = pluginTypes[index];
                Console.WriteLine($"{index + 1}: {type.Namespace} -> {type.Name}");
            }
            
            int pickedIndex = FindUserPick(pluginTypes);
            if (pickedIndex == -1)
            {
                Console.WriteLine("No instruction set loaded.");
                return false;
            }

            loadedInstruction.Add(pluginTypes[pickedIndex - 1]);
            Console.WriteLine($"Loaded instruction set: {pluginTypes[pickedIndex - 1].Namespace} -> {pluginTypes[pickedIndex - 1].Name}");
            return true;
        }
        public static int FindUserPick(List<Type> pluginTypes, int maxAttempts = 10)
        {
            int pickedIndex = -1;
            for (int i = 0; i < maxAttempts; i++)
            {
                Debug.WriteLine($"performing attempt: {i}/{maxAttempts}");
                Debug.WriteLine("Enter the number of an instruction to load:");
                Console.Write("> ");
                String? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("No input provided!");
                    continue;
                }
                if (int.TryParse(input, out pickedIndex))
                {
                    if (pickedIndex == 0)
                    {
                        Console.WriteLine("Exiting picker.");
                        return -1;
                    }
                    if (pickedIndex < 1 || pickedIndex > pluginTypes.Count)
                    {
                        Console.WriteLine("Invalid number, please try again.");
                        pickedIndex = -1;
                        continue;
                    }
                    Debug.WriteLine($"Valid number found! {pickedIndex}");
                    break;
                }
                else
                {
                    Debug.WriteLine($"input: {input} could not be parsed as integer");
                    pickedIndex = -1;
                }

                for (int index = 0; index < pluginTypes.Count; index++)
                {
                    if (string.Equals(pluginTypes[index].Name, input, StringComparison.OrdinalIgnoreCase))
                    {
                        return index + 1;
                    }
                }
                Debug.WriteLine($"did not find any instruction set matching: {input}");
            }
            return pickedIndex;
        }
        public static List<Type> FindInstructions(Type targetType, List<Type> exclusion)
        {
            string exeDir = AppContext.BaseDirectory;
            Debug.WriteLine($"Searching for instruction sets in: {exeDir}");
            string[] dllFiles = Directory.GetFiles(exeDir, "*.dll");
            List<Type> pluginTypes = [];
            var context = new InstructionLoader();
            for (int i = 0; i < exclusion.Count; i++)
            {
                Debug.WriteLine($"Excluded instruction: {{{i}}}", exclusion[i].Name);
            }
            foreach (var dll in dllFiles)
            {
                try
                {
                    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                    Type[] types;
                    try
                    {
                        Debug.WriteLine($"Loading types from assembly: {dll}");
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        Debug.WriteLine($"Warning: something went wrong while loading types from {dll}, {ex.Message}");
                        types = ex.Types!;
                    }
                    foreach (Type t in types)
                    {
                        if (t == null) {
                            Debug.WriteLine($"ignoring null type");
                            continue;
                        }
                        if (!targetType.IsAssignableFrom(t))
                        {
                            Debug.WriteLine($"ignoring class: {t.Name} as it does not extend {targetType.Name}");
                            continue;
                        }
                        if (t.IsAbstract)
                        {
                            Debug.WriteLine($"ignoring class: {t.Name} as it is abstract");
                            continue;
                        }
                        if (exclusion.Contains(t))
                        {
                            Debug.WriteLine($"exluding class: {t.Name}");
                            continue;
                        }

                        pluginTypes.Add(t);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading assembly {dll}: {ex.Message}");
                }
                finally
                {
                    Debug.WriteLine($"handled file: {dll}");
                }
            }
            return pluginTypes;
        }
    }
}
