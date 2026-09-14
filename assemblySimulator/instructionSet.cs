using assemblySimulator;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace assemblySimulator
{
    internal class OsHandler
    {
        private TextReader stdIn;
        private TextWriter stdOut;
        private bool isStdOutTerminal;
        private TextWriter[] altOut;
        private Mem mem;
        private byte memMode = 0;
        private InstructionSet? instructionSet;
        public OsHandler(Mem mem,TextReader? stdIn, TextWriter? stdOut)
        {
            this.mem = mem;
            this.stdIn = stdIn ?? Console.In;
            this.stdOut = stdOut ?? Console.Out;
            this.isStdOutTerminal = stdOut == null;
            this.altOut = [];
        }
        public OsHandler(Mem mem, TextReader? stdIn, TextWriter[] stdOut)
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
        public void SetInstructionSet(InstructionSet instructionSet)
        {
            this.instructionSet = instructionSet;
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
    abstract class InstructionSet
    {
        Mem memTarget;
        // Reg regTarget;
        OsHandler os;
        public InstructionSet(ref Mem memTarget/*, ref Reg regTarget*/, OsHandler os)
        {
            this.memTarget = memTarget;
            //this.regTarget = regTarget;
            this.os = os;
        }
        /**
         * tool for allowing other components to recieve the currently used opcode from instruction
         */
        public abstract UInt64 GetOpcode(UInt64 instruction);
        /**
         * tool for allowing other components to know which registers will be written to by this instruction
         */
        public abstract UInt64[] GetRegsWrite(UInt64 instruction);
        /**
         * tool for allowing other components to knwo which registers will be read from by this instruction
         */
        public abstract UInt64[] GetRegsRead(UInt64 instruction);
        /**
         * incriment the PC to the next instruction
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
        public abstract void ReadArrayFromMem(UInt64 startAddress, UInt64[] value, int bytesPerIndex);
        /**
         * allow reading an array from memory
         * formats the raw memory structure to fit into the number system of a UInt64
         */
        public abstract void WriteArrayToMem(UInt64 startAddress, UInt64[] value, int bytesPerIndex);
    }
}
