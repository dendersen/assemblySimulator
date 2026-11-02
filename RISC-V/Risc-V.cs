using assemblySimulator;
using System.Collections.Frozen;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RISC_V
{
    public class Instruction(UInt32 bits)
    {
        public UInt32 bits = bits;
        internal UInt32 mask(int bitCount)
        {
            if (bitCount >= 32)
                return ~(UInt32)0;
            return (1u << bitCount) - 1;
        }
        public UInt32 ReadBits(UInt32 bits, int start, int end)
        {
            return (bits & mask(end + 1)) >> start;
        }
        public ushort opcode => (ushort)ReadBits(bits,  0, 6);
        public void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction: bits=0x{bits:X8}, opcode=0x{opcode:X2}");
        }
        public UInt32 ZEXT(UInt32 value, int bitCount)
        {
            return value & mask(bitCount);
        }
        public Int32 SEXT(UInt32 value, int bitCount)
        {
            if ((value & (1u << (bitCount - 1))) == 0)
                return (Int32)(value & mask(bitCount));
            Int32 sign = (Int32)mask(32 - bitCount) << bitCount;
            return (Int32)(value & mask(bitCount)) | sign;
        }
    }
    public class Instruction_R(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public ushort funct7 => (ushort)ReadBits(bits, 25, 31);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_R: rd=0x{rd:X1}, funct3=0x{funct3:X1}, rs1=0x{rs1:X1}, rs2=0x{rs2:X1}, funct7=0x{funct7:X2}");
        }
        public void DEBUG_PRINT(string instructionName)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rd}, X{rs1}, X{rs2}");
        }
    }
    public class Instruction_I(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        protected UInt32 imm => (UInt32)ReadBits(bits, 20, 31);
        public Int32 simm => SEXT(imm, 12);
        public UInt32 zimm => ZEXT(imm, 12);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_I: rd=0x{rd:X1}, funct3=0x{funct3:X1}, rs1=0x{rs1:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rd}, X{rs1}, {(signedImm ? simm : zimm)}");
        }
    }
    public class Instruction_S(Instruction inst) : Instruction(inst.bits)
    {
        protected UInt32 imm => (UInt32)((ReadBits(bits,  7, 11) << 0) | 
                                         (ReadBits(bits, 25, 31) << 5));
        public Int32 simm => SEXT(imm, 12);
        public UInt32 zimm => ZEXT(imm, 12);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_S: rs1=0x{rs1:X1}, funct3=0x{funct3:X1}, rs2=0x{rs2:X1}, funct3=0x{funct3:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rs2}, {(signedImm ? simm : zimm)}(X{rs1})");
        }
    }
    public class Instruction_B(Instruction inst) : Instruction(inst.bits)
    {
        protected UInt32 imm => (UInt32)((ReadBits(bits,  7,  7) << 11) |
                                      (ReadBits(bits,  8, 11) <<  1) |
                                      (ReadBits(bits, 25, 30) <<  5) |
                                      (ReadBits(bits, 31, 31) << 12));
        public Int32 simm => SEXT(imm, 13);
        public UInt32 zimm => ZEXT(imm, 13);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_B: rs1=0x{rs1:X1}, funct3=0x{funct3:X1}, rs2=0x{rs2:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rs1}, X{rs2}, {(signedImm ? simm : zimm)}");
        }
    }
    public class Instruction_U(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        protected UInt32 imm => (UInt32)(ReadBits(bits, 12, 31) << 12);
        public Int32 simm => (Int32)imm;
        public UInt32 zimm => imm;
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_U: rd=0x{rd:X1}, imm=0x{imm:X8}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rd}, {(signedImm ? simm : zimm)}");
        }
    }
    public class Instruction_J(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        protected UInt32 imm => (UInt32)(ReadBits(bits, 12, 19) << 12 |
                                      ReadBits(bits, 20, 20) << 11 |
                                      ReadBits(bits, 21, 30) <<  1 |
                                      ReadBits(bits, 31, 31) << 20);
        public Int32 simm => SEXT(imm, 21);
        public UInt32 zimm => ZEXT(imm, 21);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_J: rd=0x{rd:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            if (signedImm)
            {
                if (simm < 0)
                {
                    Debug.WriteLine($"called instruction: {instructionName}, X{rd}, -{Math.Abs(simm)}");
                    return;
                }else
                {
                    Debug.WriteLine($"called instruction: {instructionName}, X{rd}, {Math.Abs(simm)}");
                    return;
                }
            }
            Debug.WriteLine($"called instruction: {instructionName}, X{rd}, {zimm}");
        }
    }
    public abstract class RiscV_32_Extension(Mem memTarget, OsHandler os, RegisterBlock regs,RiscV_32 instructionSet)
    {
        internal RiscV_32 instructionSet = instructionSet;
        internal RegisterBlock regs_shared = regs;
        internal OsHandler os = os;
        internal Mem mem = memTarget;
        public abstract bool Validate(Instruction inst);
        public abstract void Handle(Instruction inst);
    }
    public class RiscV_32 : InstructionSet
    {
        internal RegisterBlock regs_shared;
        private List<RiscV_32_Extension> extensions;
        public static new readonly short memBytesPerAddress = 1;
        private UInt32 PC = 0;
        private bool blockPC = false;
        public RiscV_32(ref Mem memTarget_, OsHandler os) : base(ref memTarget_, os)
        {
            regs_shared = new(32, 32,[false]);
            extensions = [new RV32I(memTarget_, os, regs_shared, this)];
        }
        public override bool ExecuteInstruction(ulong instruction)
        {
            if (instruction == 0)
            {
                Debug.WriteLine($"Instruction is 0, halting execution on illegal opcode.");
                return true;
            }
            bool handled = false;
            Instruction inst = new((UInt32)instruction);
            foreach (RiscV_32_Extension extension in extensions)
            {
                if (extension.Validate(inst))
                {
                    try
                    {
                        extension.Handle(inst);
                        Debug.WriteLine($"Instruction handled by extension: {extension.GetType().Name}, opcode: {inst.opcode:B8}");
                    }catch(IOException e)
                    {
                        Debug.WriteLine(e.Message);
                        return true;
                    }
                    handled = true;
                    break;
                }
            }
            if (!handled)
            {
                Debug.WriteLine($"Instruction not handled: {instruction:X8}");
                Debug.WriteLine($"Instruction opcode: {inst.opcode:X2}");
                throw new NotImplementedException($"Instruction not in any loaded extension. opcode: 0b{inst.opcode:B8}, PC: 0x{PC:X8}");
            }
            return false;
        }
        public override bool Execute()
        {
            UInt32 instruction = (UInt32)ReadValueFromMem(PC, 4);
            bool ret =  ExecuteInstruction((ulong)instruction);
            IncrimentPC();
            return ret;
        }
        public override ulong GetOpcode(ulong instruction)
        {
            return new Instruction((UInt32)instruction).opcode;
        }

        public override ulong GetPC()
        {
            return PC;
        }

        public override void IncrimentPC()
        {
            if(!blockPC)
                JumpRelative(4);
            blockPC = false;
        }
        public void JumpOverride(ulong address, bool isBranch)
        {
            blockPC = isBranch;
            JumpOverride(address);
        }
        public override void JumpOverride(ulong address)
        {
            if (address > memTarget.maxAddress)
            {
                Console.WriteLine($"Jump target address {address:X8} is beyond the memory size.");
                throw new Exception($"Jump target address {address:X8} is beyond the memory size.");
            }
            PC = (UInt32) address;
        }

        public void JumpRelative(long offset, bool isBranch)
        {
            blockPC = isBranch;
            JumpOverride(PC + (UInt32)offset);
        }
        public override void JumpRelative(long offset)
        {
            JumpRelative(offset, false);
        }

        public override ulong[] ReadArrayFromMem(ulong startAddress, int bytesPerIndex, ulong length)
        {
            ulong[] array = new ulong[length];
            for (ulong i = 0; i < length; i++)
            {
                array[i] = ReadValueFromMem(startAddress + (i * (ulong)bytesPerIndex), bytesPerIndex);
            }
            return array;
        }

        public override ulong ReadValueFromMem(ulong address, int byteCount)
        {
            ulong value = 0;
            for (int i = 0; i < byteCount; i++)
            {
                value |= (ulong)memTarget.Read(address + (ulong)i) << (8 * i);
            }
            return value;
        }

        public override void WriteArrayToMem(ulong startAddress, ulong[] value, int bytesPerIndex)
        {
            for (int i = 0; i < value.Length; i++)
            {
                WriteValueToMem(startAddress + (ulong)(i * bytesPerIndex), value[i], bytesPerIndex);
            }
        }

        public override void WriteValueToMem(ulong address, ulong value, int byteCount)
        {
            for (int i = 0; i < byteCount; i++)
            {
                memTarget.Write(address + (ulong)i, (value >> (8 * i)) & 0xFF);
            }
        }
        public override void Dump()
        {
            Debug.WriteLine($"PC: 0x{GetPC():X8}");
            for (int reg = 0; reg < regs_shared.Length; reg++)
            {
                Debug.WriteLine($"Reg x{reg:D2}: 0x{regs_shared.Read((ushort)reg):X8}");
            }
        }
        public override byte[] RegDump()
        {
            byte[] regValues = new byte[regs_shared.Length * 4];
            for (int reg = 0; reg < regs_shared.Length; reg++)
            {
                regValues[reg * 4 + 0] = (byte)(regs_shared.Read((ushort)reg) & 0xFF);
                regValues[reg * 4 + 1] = (byte)((regs_shared.Read((ushort)reg) >> 8) & 0xFF);
                regValues[reg * 4 + 2] = (byte)((regs_shared.Read((ushort)reg) >> 16) & 0xFF);
                regValues[reg * 4 + 3] = (byte)((regs_shared.Read((ushort)reg) >> 24) & 0xFF);
            }
            return regValues;
        }
    }
}
