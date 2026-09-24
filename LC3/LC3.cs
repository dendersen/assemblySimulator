using assemblySimulator;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
namespace LC3
{
    internal class LC3Executer
    {
        internal RegisterBlock regs;
        internal OsHandler os;
        internal Mem mem;
        internal DebugManager? debug;
        internal LC3InstructionSet instruct;
        bool n = false, z = false, p = false;
        private UInt16 PSR = 0; //TODO
        public LC3Executer(RegisterBlock regs, OsHandler os, Mem mem, DebugManager? debug, LC3InstructionSet instruct)
        {
            this.regs = regs;
            this.os = os;
            this.mem = mem;
            this.debug = debug;
            this.instruct = instruct;
        }
        private byte getDR(ulong instruction)
        {
            return (byte)((instruction >> 9) & 0b111);
        }
        private byte getSR1(ulong instruction)
        {
            return (byte)((instruction >> 6) & 0b111);
        }
        private byte getSR2(ulong instruction)
        {
            return (byte)(instruction & 0b111);
        }
        private short getImmX(ulong instruction, short bits)
        {
            return (short)(instruction & (((UInt64)1 << bits) - 1));
        }
        public byte GetOpcode(ulong instruction) {
            return (byte)((instruction >> 12) & 0b1111);
        }
        public bool validate(ulong instruction)
        {
            byte opcode = GetOpcode(instruction);
            byte DR = getDR(instruction);
            switch (opcode)
            {
                case 0b0001: // ADD
                case 0b0101: // AND
                    return ((instruction & (1 << 5)) != 0) || (getImmX(instruction,5) >> 3) == 0;
                case 0b1000: // RTI
                    return ((instruction & ~(UInt64)opcode) & 0xFFFF) == 0;
                case 0b1001: // NOT
                    return getImmX(~instruction, 6) == 0;
                case 0b0100: // JSR
                    return (instruction & (UInt64)(1 << 11)) != 0 || (DR == 0 && getImmX(instruction, 6) == 0);
                case 0b1100: // JMP
                    return DR == 0 && getImmX(instruction, 6) == 0;
                case 0b0000: // BR
                case 0b0010: // LD
                case 0b0011: // ST
                case 0b0110: // LDR
                case 0b0111: // STR
                case 0b1010: // LDI
                case 0b1011: // STI
                case 0b1110: // LEA
                case 0b1111: // TRAP
                    return true;
                case 0b1101: // RESERVED
                default:
                    return false;
            }
        }
        public void handle(ulong instruction)
        {
            if (!validate(instruction))
            {
                instruct.UNKOWNCALL(instruction);
                return;
            }
            byte opcode = GetOpcode(instruction);
            byte DR = getDR(instruction);
            byte SR1 = getSR1(instruction);
            byte SR2 = getSR2(instruction);
            switch (opcode)
            {
                case 0b0000:// BR
                    {
                        short n = (short)((instruction >> 11) & 1);
                        short z = (short)((instruction >> 10) & 1);
                        short p = (short)((instruction >> 9) & 1);
                        short PCoffset9 = getImmX(instruction, 9);
                        br(PCoffset9, n != 0, z != 0, p != 0);
                    }
                    break;
                case 0b0001:// ADD
                    if ((instruction & (1 << 5)) != 0)
                    {
                        short imm5 = (short)(instruction & 0b11111);
                        add((byte)DR, (byte)SR1, imm5);
                    }
                    else
                    {
                        add((byte)DR, (byte)SR1, (byte)SR2);
                    }
                    break;
                case 0b0010:// LD
                    {
                        short PCoffset9 = getImmX(instruction, 9);
                        ld((byte)DR, PCoffset9);
                    }
                    break;
                case 0b0011:// ST
                    {
                        short PCoffset9 = getImmX(instruction, 9);
                        st((byte)SR1, PCoffset9);
                    }
                    break;
                case 0b0100:// JSR / JSRR
                    if ((instruction & (1 << 11)) != 0) {
                        short PCoffset11 = getImmX(instruction, 11);
                        jsr(PCoffset11);
                    }
                    else
                    {
                        jsrr(SR1);
                    }
                    break;
                case 0b0101:// AND
                    if ((instruction & (1 << 5)) != 0)
                    {
                        short imm5 = (short)(instruction & 0b11111);
                        and((byte)DR, (byte)SR1, imm5);
                    }
                    else
                    {
                        and((byte)DR, (byte)SR1, (byte)SR2);
                    }
                    break;
                case 0b0110:// LDR
                    {
                        short offset6 = getImmX(instruction, 6);
                        ldr((byte)DR, (byte)SR1, offset6);
                    }
                    break;
                case 0b0111:// STR
                    {
                        short offset6 = getImmX(instruction, 6);
                        str((byte)SR1, (byte)SR2, offset6);
                    }
                    break;
                case 0b1000:// RTI
                    {
                        rti();
                    }
                    break;
                case 0b1001:// NOT
                    {
                        not((byte)DR, (byte)SR1);
                    }
                    break;
                case 0b1010:// LDI
                    {
                        short PCoffset9 = getImmX(instruction, 9);
                        ldi((byte)DR, PCoffset9);
                    }
                    break;
                case 0b1011:// STI
                    {
                        short PCoffset9 = getImmX(instruction, 9);
                        sti((byte)SR1, PCoffset9);
                    }
                    break;
                case 0b1100:// JMP
                    {
                        jmp((byte)SR1);
                    }
                    break;
                case 0b1101:// RESERVED
                    {
                        instruct.UNKOWNCALL(instruction);
                    }
                    break;
                case 0b1110:// LEA
                    {
                        short PCoffset9 = getImmX(instruction, 9);
                        lea((byte)DR, PCoffset9);
                    }
                    break;
                case 0b1111:// TRAP
                    {
                        byte trapvect8 = (byte)(instruction & 0xFF);
                        trap(trapvect8);
                    }
                    break;
            }

        }
        internal UInt64 calc(UInt64 result)
        {
            if (result < 0) {
                n = true;
            } else {
                n = false;
            }

            if (result == 0){
                z = true;
            } else {
                z = false;
            }
            
            if (result > 0){
                p = true;
            } else {
                p = false;
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
        internal void add(byte DR, byte SR1, byte SR2)//
        {
            UInt64 result = regs.Read(SR1) + regs.Read(SR2);
            regs.Write(DR, calc(result));
        }
        internal void add(byte DR, byte SR1, short imm5)//
        {
            UInt64 result = (UInt64)((Int64)regs.Read(SR1) + SEXT((UInt64)imm5,5));
            regs.Write(DR, calc(result));
        }
        internal void and(byte DR, byte SR1, byte SR2)//
        {
            UInt64 result = regs.Read(SR1) & regs.Read(SR2);
            regs.Write(DR, calc(result));
        }
        internal void and(byte DR, byte SR1, short imm5)//
        {
            UInt64 result = (UInt64)((Int64)regs.Read(SR1) & (Int64)SEXT((UInt64)imm5,5));
            regs.Write(DR, calc(result));
        }
        internal void br(short PCoffset9, bool n, bool z, bool p)
        {
            if ((this.n && n) || (this.z && z) || (this.p && p))
            {
                instruct.JumpRelative(SEXT((UInt64)PCoffset9,9));
            }
        }
        internal void jmp(byte BaseR)
        {
            instruct.JumpOverride(regs.Read(BaseR));
        }
        internal void jsr(short PCoffset11)
        {
            regs.Write(7, instruct.GetPC());
            instruct.JumpRelative(SEXT((UInt64)PCoffset11, 11));
        }
        internal void jsrr(byte BaseR)
        {
            regs.Write(7, instruct.GetPC());
            instruct.JumpRelative((Int64)regs.Read(BaseR));
        }
        internal void ld(byte DR, short PCoffset9)//
        {
            UInt64 address = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9, 9));
            UInt64 result = mem.Read(address);
            regs.Write(DR, calc(result));
        }
        internal void ldi(byte DR, short PCoffset9)//
        {
            UInt64 address0 = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9,9));
            UInt64 address1 = mem.Read(address0);
            UInt64 result = mem.Read(address1);
            regs.Write(DR, calc(result));
        }
        internal void ldr(byte DR, byte BaseR, short offset6)//
        {
            UInt64 address = (UInt64)(((Int64)regs.Read(BaseR)) + SEXT((UInt64)offset6, 6));
            UInt64 result = mem.Read(address);
            regs.Write(DR, calc(result));
        }
        internal void lea(byte DR, short PCoffset9)//
        {
            UInt64 result = (UInt64)((Int64)instruct.GetPC() + SEXT((UInt64)PCoffset9, 9));
            regs.Write(DR, calc(result));
        }
        internal void not(byte DR, byte SR)//
        {
            regs.Write(DR,calc(~regs.Read(SR)));
        }
        internal void ret() { jmp(0b111); }
        internal void rti()
        {
            if ((PSR & (1 << 15)) != 0)
            {
                throw new InvalidOperationException("RTI instruction can only be executed in privileged mode.");
            }
            instruct.JumpOverride(mem.Read(regs.Read(6)));
            regs.Write(6, mem.Read(regs.Read(6) + 1));
            UInt16 TEMP = (UInt16)mem.Read(regs.Read(6));
            regs.Write(6, mem.Read(regs.Read(6) + 1));
            PSR = TEMP;
        }
        internal void st(byte SR, short PCoffset9)
        {
            UInt64 address = (UInt64)((Int64)instruct.GetPC() + (Int64)SEXT((UInt64)PCoffset9, 9));
            mem.Write(address, regs.Read(SR));
        }
        internal void sti(byte SR, short PCoffset9)
        {
            UInt64 address0 = (UInt64)((Int64)instruct.GetPC() + (Int64)SEXT((UInt64)PCoffset9, 9));
            UInt64 address1 = mem.Read(address0);
            mem.Write(address1, regs.Read(SR));
        }
        internal void str(byte SR, byte BaseR, short offset6)
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
        internal void trap(byte trapvect8)
        {
            switch(trapvect8)
            {
                case 0x20:
                    trap(0x23);
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
                        throw new NotImplementedException("halt trap called, but no debugger is attached");
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
        public static readonly new short memBytesPerAddress = 16;
        public static readonly new UInt64 regCount = 8;
        public static readonly new UInt64[]? regValues = null;
        public static readonly new bool[]? regWritable = null;
        public readonly static new string[] opcodeNames = {
            "BR", "ADD", "LD", "ST",
            "JSR", "AND", "LDR", "STR",
            "RTI", "NOT", "LDI", "STI",
            "JMP", "RESERVED", "LEA", "TRAP"
        };
        internal LC3Executer? executer;
        public LC3InstructionSet(ref Mem memTarget, ref RegisterBlock regTarget, OsHandler os) : base(ref memTarget, ref regTarget, os) { }
        public override void ExecuteInstruction(ulong instruction)
        {
            executer ??= new LC3Executer(regTarget, os, memTarget, debug, this);
            executer.handle(instruction);
        }
        public override ulong GetOpcode(ulong instruction)
        {
            executer ??= new LC3Executer(regTarget, os, memTarget, debug, this);
            return (UInt64) executer.GetOpcode(instruction);
        }
        public override void UNKOWNCALL(UInt64 instruction)
        {

        }
        public override bool isKnownCall(UInt64 instruction)
        {
            return executer.validate(instruction);
        }

        public override ulong GetPC()
        {
            return PC;
        }

        public override ulong[] GetRegsRead(ulong instruction)
        {
            throw new NotImplementedException();
        }

        public override ulong[] GetRegsWrite(ulong instruction)
        {
            throw new NotImplementedException();
        }

        public override void IncrimentPC()
        {
            PC++;
        }
        public override void JumpOverride(UInt64 address)
        {
            PC = address;
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
        }

        public override void ReadArrayFromMem(ulong startAddress, ulong[] value, int bytesPerIndex)
        {
            throw new NotImplementedException();
        }

        public override ulong ReadValueFromMem(ulong address, int byteCount)
        {
            throw new NotImplementedException();
        }

        public override void WriteArrayToMem(ulong startAddress, ulong[] value, int bytesPerIndex)
        {
            throw new NotImplementedException();
        }

        public override void WriteValueToMem(ulong address, ulong value, int byteCount)
        {
            throw new NotImplementedException();
        }
    }
}
