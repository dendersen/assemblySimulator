using assemblySimulator;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Diagnostics;

namespace LC3
{

    [StructLayout(LayoutKind.Explicit, Size = 2)]
    internal struct PSR
    {
        [FieldOffset(0)]
        public UInt16 bits;// Backing 16-bit storage layout shared with C
        public bool P// Bit 0 (1 bit flag)
        {
            readonly get => (bits & 0x1) != 0;
            set => bits = value ? (UInt16)(bits | 0x1) : (UInt16)(bits & ~0x1);
        }
        public bool Z// Bit 1 (1 bit flag)
        {
            readonly get => (bits & (1 << 1)) != 0;
            set => bits = value ? (UInt16)(bits | (1 << 1)) : (UInt16)(bits & ~(1 << 1));
        }
        public bool N// Bit 2 (1 bit flag)
        {
            readonly get => (bits & (1 << 2)) != 0;
            set => bits = value ? (UInt16)(bits | (1 << 2)) : (UInt16)(bits & ~(1 << 2));
        }
        public byte PriorityLevel// Bits 8..10 (3 bits, max value = 7)
        {
            readonly get => (byte)((bits >> 8) & 0x7);
            set => bits = (UInt16)((bits & ~(0x7 << 8)) | ((value & 0x7) << 8));
        }
        public bool PrivilegeMode// Bit 15 (1 bit flag)
        {
            readonly get => (bits & (1 << 15)) != 0;
            set => bits = value ? (UInt16)(bits | (1 << 15)) : (UInt16)(bits & ~(1 << 15));
        }
    }
    internal class LC3Executer(OsHandler os, Mem mem, DebugManager? debug, LC3InstructionSet instruct)
    {
        internal RegisterBlock regs = new(8, 16);
        internal OsHandler os = os;
        internal Mem mem = mem;
        internal DebugManager? debug = debug;
        internal LC3InstructionSet instruct = instruct;
        private PSR psr;

        private static byte GetDR(ulong instruction)
        {
            return (byte)((instruction >> 9) & 0b111);
        }
        private static byte GetSR1(ulong instruction)
        {
            return (byte)((instruction >> 6) & 0b111);
        }
        private static byte GetSR2(ulong instruction)
        {
            return (byte)(instruction & 0b111);
        }
        private static short GetImmX(ulong instruction, short bits)
        {
            return (short)(instruction & (((UInt64)1 << bits) - 1));
        }
        public static byte GetOpcode(ulong instruction) {
            return (byte)((instruction >> 12) & 0b1111);
        }
        public static bool Validate(ulong instruction)
        {
            byte opcode = GetOpcode(instruction);
            byte DR = GetDR(instruction);
            switch (opcode)
            {
                case 0b0001://ADD
                case 0b0101://AND
                    return ((instruction & (1 << 5)) != 0) || (GetImmX(instruction,5) >> 3) == 0;
                case 0b1000://RTI
                    return ((instruction & ~(UInt64)opcode) & 0xFFFF) == 0;
                case 0b1001://NOT
                    return GetImmX(~instruction, 6) == 0;
                case 0b0100://JSR
                    return (instruction & (UInt64)(1 << 11)) != 0 || (DR == 0 && GetImmX(instruction, 6) == 0);
                case 0b1100://JMP
                    return DR == 0 && GetImmX(instruction, 6) == 0;
                case 0b0000://BR
                case 0b0010://LD
                case 0b0011://ST
                case 0b0110://LDR
                case 0b0111://STR
                case 0b1010://LDI
                case 0b1011://STI
                case 0b1110://LEA
                case 0b1111://TRAP
                    return true;
                case 0b1101://RESERVED
                default:
                    return false;
            }
        }
        public static byte[] GetUsedRegs(ulong instruction)
        {
            byte opcode = GetOpcode(instruction);
            byte DR = GetDR(instruction);
            byte SR1 = GetSR1(instruction);
            byte SR2 = GetSR2(instruction);
            switch (opcode)
            {
                case 0b0001://ADD
                case 0b0101://AND
                    if ((instruction & (1 << 5)) != 0)
                    {
                        return [DR, SR1];
                    }
                    else
                    {
                        return [DR, SR1, SR2];
                    }
                case 0b1000://RTI
                    return [6];
                case 0b1001://NOT
                    return [DR, SR1];
                case 0b0100://JSR
                    if ((instruction & (UInt64)(1 << 11)) != 0)
                    {
                        return [7];
                    }
                    else
                    {
                        return [7, SR1];
                    }
                case 0b1100://JMP
                    return [SR1];
                case 0b0110://LDR
                case 0b0111://STR
                    return [DR, SR1];
                case 0b0010://LD
                case 0b0011://ST
                case 0b1010://LDI
                case 0b1011://STI
                case 0b1110://LEA
                    return [DR];
                case 0b1111://TRAP
                    return [7];
                case 0b0000://BR
                case 0b1101://RESERVED
                default:
                    return Array.Empty<byte>();
            }
        }
        public void Handle(ulong instruction)
        {
            if (!Validate(instruction))
            {
                instruct.UNKOWNCALL(instruction);
                return;
            }
            byte opcode = GetOpcode(instruction);
            byte DR = GetDR(instruction);
            byte SR1 = GetSR1(instruction);
            byte SR2 = GetSR2(instruction);
            switch (opcode)
            {
                case 0b0000://BR
                    {
                        short n = (short)((instruction >> 11) & 1);
                        short z = (short)((instruction >> 10) & 1);
                        short p = (short)((instruction >> 9) & 1);
                        short PCoffset9 = GetImmX(instruction, 9);
                        Br(PCoffset9, n != 0, z != 0, p != 0);
                    }
                    break;
                case 0b0001://ADD
                    if ((instruction & (1 << 5)) != 0)
                    {
                        short imm5 = (short)(instruction & 0b11111);
                        Add((byte)DR, (byte)SR1, imm5);
                    }
                    else
                    {
                        Add((byte)DR, (byte)SR1, (byte)SR2);
                    }
                    break;
                case 0b0010://LD
                    {
                        short PCoffset9 = GetImmX(instruction, 9);
                        Ld((byte)DR, PCoffset9);
                    }
                    break;
                case 0b0011://ST
                    {
                        short PCoffset9 = GetImmX(instruction, 9);
                        St((byte)SR1, PCoffset9);
                    }
                    break;
                case 0b0100://JSR / JSRR
                    if ((instruction & (1 << 11)) != 0) {
                        short PCoffset11 = GetImmX(instruction, 11);
                        Jsr(PCoffset11);
                    }
                    else
                    {
                        Jsrr(SR1);
                    }
                    break;
                case 0b0101://AND
                    if ((instruction & (1 << 5)) != 0)
                    {
                        short imm5 = (short)(instruction & 0b11111);
                        And((byte)DR, (byte)SR1, imm5);
                    }
                    else
                    {
                        And((byte)DR, (byte)SR1, (byte)SR2);
                    }
                    break;
                case 0b0110://LDR
                    {
                        short offset6 = GetImmX(instruction, 6);
                        Ldr((byte)DR, (byte)SR1, offset6);
                    }
                    break;
                case 0b0111://STR
                    {
                        short offset6 = GetImmX(instruction, 6);
                        Str((byte)SR1, (byte)SR2, offset6);
                    }
                    break;
                case 0b1000://RTI
                    {
                        Rti();
                    }
                    break;
                case 0b1001://NOT
                    {
                        Not((byte)DR, (byte)SR1);
                    }
                    break;
                case 0b1010://LDI
                    {
                        short PCoffset9 = GetImmX(instruction, 9);
                        Ldi((byte)DR, PCoffset9);
                    }
                    break;
                case 0b1011://STI
                    {
                        short PCoffset9 = GetImmX(instruction, 9);
                        Sti((byte)SR1, PCoffset9);
                    }
                    break;
                case 0b1100://JMP
                    {
                        Jmp((byte)SR1);
                    }
                    break;
                case 0b1101://RESERVED
                    {
                        instruct.UNKOWNCALL(instruction);
                    }
                    break;
                case 0b1110://LEA
                    {
                        short PCoffset9 = GetImmX(instruction, 9);
                        Lea((byte)DR, PCoffset9);
                    }
                    break;
                case 0b1111://TRAP
                    {
                        byte trapvect8 = (byte)(instruction & 0xFF);
                        Trap(trapvect8);
                    }
                    break;
            }

        }
        internal UInt64 Calc(UInt64 result)
        {
            if (result < 0) {
                psr.N = true;
            } else {
                psr.N = false;
            }

            if (result == 0){
                psr.Z = true;
            } else {
                psr.Z = false;
            }
            
            if (result > 0){
                psr.P = true;
            } else {
                psr.P = false;
            }
            return result;
        }
        internal static Int64 SEXT(UInt64 value, int bits)
        {
            if ((value & (1UL << (bits - 1))) != 0)
            {
                value |= ~((1UL << bits) - 1);
            }
            return (Int64)value;
        }
        internal UInt64 Offset(UInt64 value, int bits)
        {
            return (UInt64)SEXT(value, bits) + instruct.GetPC();
        }
        internal void Add(byte DR, byte SR1, byte SR2)//
        {
            UInt64 result = regs.Read(SR1) + regs.Read(SR2);
            regs.Write(DR, Calc(result));
        }
        internal void Add(byte DR, byte SR1, short imm5)//
        {
            UInt64 result = (UInt64)((Int64)regs.Read(SR1) + SEXT((UInt64)imm5,5));
            regs.Write(DR, Calc(result));
        }
        internal void And(byte DR, byte SR1, byte SR2)//
        {
            UInt64 result = regs.Read(SR1) & regs.Read(SR2);
            regs.Write(DR, Calc(result));
        }
        internal void And(byte DR, byte SR1, short imm5)//
        {
            UInt64 result = (UInt64)((Int64)regs.Read(SR1) & (Int64)SEXT((UInt64)imm5,5));
            regs.Write(DR, Calc(result));
        }
        internal void Br(short PCoffset9, bool n, bool z, bool p)
        {
            if ((psr.N && n) || (psr.Z && z) || (psr.P && p))
            {
                instruct.JumpRelative(SEXT((UInt64)PCoffset9,9));
            }
        }
        internal void Jmp(byte BaseR)
        {
            instruct.JumpOverride(regs.Read(BaseR));
        }
        internal void Jsr(short PCoffset11)
        {
            regs.Write(7, instruct.GetPC());
            instruct.JumpRelative(SEXT((UInt64)PCoffset11, 11));
        }
        internal void Jsrr(byte BaseR)
        {
            regs.Write(7, instruct.GetPC());
            instruct.JumpRelative((Int64)regs.Read(BaseR));
        }
        internal void Ld(byte DR, short PCoffset9)//
        {
            UInt64 address = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9, 9));
            UInt64 result = mem.Read(address);
            regs.Write(DR, Calc(result));
        }
        internal void Ldi(byte DR, short PCoffset9)//
        {
            UInt64 address0 = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9,9));
            UInt64 address1 = mem.Read(address0);
            UInt64 result = mem.Read(address1);
            regs.Write(DR, Calc(result));
        }
        internal void Ldr(byte DR, byte BaseR, short offset6)//
        {
            UInt64 address = (UInt64)(((Int64)regs.Read(BaseR)) + SEXT((UInt64)offset6, 6));
            UInt64 result = mem.Read(address);
            regs.Write(DR, Calc(result));
        }
        internal void Lea(byte DR, short PCoffset9)//
        {
            UInt64 result = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9, 9));
            regs.Write(DR, Calc(result));
        }
        internal void Not(byte DR, byte SR)//
        {
            regs.Write(DR,Calc(~regs.Read(SR)));
        }
        internal void Ret() { Jmp(0b111); }
        internal void Rti()
        {
            if (psr.PrivilegeMode)
            {
                throw new InvalidOperationException("RTI instruction can only be executed in privileged mode.");
            }
            instruct.JumpOverride(mem.Read(regs.Read(6)));
            regs.Write(6, mem.Read(regs.Read(6) + 1));
            UInt16 TEMP = (UInt16)mem.Read(regs.Read(6));
            regs.Write(6, mem.Read(regs.Read(6) + 1));
            psr.bits = TEMP;
        }
        internal void St(byte SR, short PCoffset9)
        {
            UInt64 address = (UInt64)((Int64)instruct.GetPC() + (Int64)SEXT((UInt64)PCoffset9, 9));
            mem.Write(address, regs.Read(SR));
        }
        internal void Sti(byte SR, short PCoffset9)
        {
            UInt64 address0 = (UInt64)((Int64)instruct.GetPC() + (Int64)SEXT((UInt64)PCoffset9, 9));
            UInt64 address1 = mem.Read(address0);
            mem.Write(address1, regs.Read(SR));
        }
        internal void Str(byte SR, byte BaseR, short offset6)
        {
            UInt64 address = (UInt64)((Int64)regs.Read(BaseR) + (Int64)SEXT((UInt64)offset6, 6));
            mem.Write(address, regs.Read(SR));
        }
        /**
         * these are implimented in the OS handler, instead of actually performing the trap,
         * undefined traps are performed as standard traps
         * predefined trap table:
         * * 0x20 -> get char from console into R0
         * * 0x21 -> output char from R0 into console
         * * 0x22 -> output string to console starting at R0
         * * 0x23 -> read a character from the console and echo it to the console, then store it in R0
         * * 0x24 -> output string to console starting at R0, but read two chars from each address
         * * 0x25 -> HALT and print HALT to console
         * 
         * at this point trap 0x20 and 0x23 are the same
         */
        internal void Trap(byte trapvect8)
        {
            switch(trapvect8)
            {
                case 0x20:
                    Trap(0x23);
                    break;
                case 0x21:
                    mem.Write(0x21, regs.Read(0));
                    os.WriteConsoleSingle(0x21,0);
                    break;
                case 0x22:
                    os.WriteStringConsole(regs.Read(0),-1, false);
                    break;
                case 0x23:
                    {
                        os.ReadConsoleSingle(0x20, 0);
                        ulong temp = mem.Read(0x20);
                        regs.Write(0, temp);
                    }
                    break;
                case 0x24:
                    os.WriteStringConsole(regs.Read(0),1, false);
                    break;
                case 0x25:
                    if (debug != null)
                    {
                        debug.AwaitHalt();
                    }
                    else
                    {
                        Debug.WriteLine("halt trap called, but no debugger is attached");
                        System.Environment.Exit(0);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    public class LC3InstructionSet : InstructionSet
    {
        internal UInt64 PC = 0;
        public static readonly new short memBytesPerAddress = 2;
        internal LC3Executer executer;
        public LC3InstructionSet(ref Mem memTarget_, OsHandler os) : base(ref memTarget_, os) {
            executer = new LC3Executer(os, memTarget_, debug, this);
        }
        public override void Execute()
        {
            UInt64 instruction = memTarget.Read(GetPC());
            IncrimentPC();
            Debug.WriteLine($"Executing instruction: 0x{instruction:X4}");
            Debug.WriteLine($"Executing opcode: 0b{GetOpcode(instruction):B4}");
            executer.Handle(instruction);
        }
        public override ulong GetOpcode(ulong instruction)
        {
            return (UInt64) LC3Executer.GetOpcode(instruction);
        }
        public void UNKOWNCALL(UInt64 instruction)
        {
            throw new NotImplementedException($"Unknown instruction: {instruction:X4}");
        }
        public override ulong GetPC()
        {
            return PC;
        }
        public override void IncrimentPC()
        {
            PC++;
            PC &= 0xFFFF; // Ensure PC wraps around at 16 bits
        }
        public override void JumpOverride(UInt64 address)
        {
            PC = address;
            PC &= 0xFFFF; // Ensure PC wraps around at 16 bits
        }
        public override void JumpRelative(Int64 offset)
        {
            if (offset < 0)
            {
                PC -= (UInt64)(-offset);
            }
            else
            {
                PC += (UInt64)offset;
            }
            PC &= 0xFFFF; // Ensure PC wraps around at 16 bits
        }

        public override ulong[] ReadArrayFromMem(ulong startAddress, int bytesPerIndex, ulong length)
        {
            ulong[] value = new ulong[length];
            for (ulong i = 0; i < length; i++)
            {
                value[i] = ReadValueFromMem(startAddress + i, bytesPerIndex);
            }
            return value;
        }

        public override ulong ReadValueFromMem(ulong address, int byteCount)
        {
            if (byteCount == 1)
            {
                return memTarget.Read(address) & 0xFF;
            }
            else if (byteCount == 2)
            {
                return memTarget.Read(address) & 0xFFFF;
            }
            else
            {
                throw new NotImplementedException("LC3 only supports reading 1 or 2 bytes from memory at a time.");
            }
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
            if (byteCount == 1)
            {
                ulong existingValue = memTarget.Read(address) & ~0xFFul;
                memTarget.Write(address, (value & 0xFF) | existingValue);
            }
            else if (byteCount == 2)
            {
                memTarget.Write(address, (value & 0xFFFF));
            }
            else
            {
                ulong[] values = new ulong[byteCount / 2];
                for (int i = 0; i < byteCount; i++)
                {
                    values[i] = (value >> (i * 16)) & 0xFFFF;
                }
                WriteArrayToMem(address, values, 2);
                if (byteCount % 2 != 0)
                {
                    ulong existingValue = memTarget.Read(address + (ulong)(byteCount - 1)) & ~0xFFul;
                    memTarget.Write(address + (ulong)(byteCount - 1), (value & 0xFF) | existingValue);
                }
            }
        }
    }
}
