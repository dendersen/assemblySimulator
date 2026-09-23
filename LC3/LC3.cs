using assemblySimulator;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
namespace LC3
{
    internal class LC3Executer
    {
        internal RegisterBlock regs;
        internal OsHandler os;
        internal Mem mem;
        internal DebugManager? debug;
        internal InstructionSet instruct;
        bool n = false, z = false, p = false;
        public LC3Executer(RegisterBlock regs, OsHandler os, Mem mem, DebugManager? debug, InstructionSet instruct)
        {
            this.regs = regs;
            this.os = os;
            this.mem = mem;
            this.debug = debug;
            this.instruct = instruct;
        }
        public void handle(ulong instruction)
        {
            throw new NotImplementedException();
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
        internal Int64 SEXT(UInt64 value, int bits)
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
        public void add(byte DR, byte SR1, byte SR2)//
        {
            regs.Write(DR, calc((ulong)(regs.Read(SR1) + regs.Read(SR2))));
            
        }
        public void add(byte DR, byte SR1, short imm5)//
        {
            regs.Write(DR, calc((ulong)(regs.Read(SR1) + (UInt64)imm5)));
        }
        public void and(byte DR, byte SR1, byte SR2)//
        {
            regs.Write(DR, calc((ulong)(regs.Read(SR1) & regs.Read(SR2))));
        }
        public void and(byte DR, byte SR1, short imm5)//
        {
            regs.Write(DR, calc((regs.Read(SR1) & (UInt64)imm5)));
        }
        public void br(short PCoffset9, bool n, bool z, bool p)
        {
            if ((this.n && n) || (this.z && z) || (this.p && p))
            {
                instruct.JumpRelative(SEXT((UInt64)PCoffset9,9));
            }
        }
        public void jmp(byte BaseR)
        {
            instruct.JumpOverride(regs.Read(BaseR));
        }
        public void jsr(short PCoffset11)
        {
            instruct.JumpRelative((Int64)PCoffset11);
        }
        public void jsrr(byte BaseR)
        {
            instruct.JumpRelative((Int64)regs.Read(BaseR));
        }
        public void ld(byte DR, short PCoffset9)//
        {
            regs.Write(DR, calc(mem.Read((UInt64)(instruct.GetPC() + (UInt64)PCoffset9))));
        }
        public void ldi(byte DR, short PCoffset9)//
        {
            regs.Write(DR, calc(mem.Read(mem.Read((UInt64)(instruct.GetPC() + (UInt64)PCoffset9)))));
        }
        public void ldr(byte DR, byte BaseR, short offset6)//
        {
            throw new NotImplementedException();
        }
        public void lea(byte DR, short PCoffset9)//
        {
            throw new NotImplementedException();
        }
        public void not(byte DR, byte SR)//
        {
            throw new NotImplementedException();
        }
        public void ret() { jmp(0b111); }
        public void rti()
        {
            throw new NotImplementedException();
        }
        public void st(byte SR, short PCoffset9)
        {
            throw new NotImplementedException();
        }
        public void sti(byte SR, short PCoffset9)
        {
            throw new NotImplementedException();
        }
        public void str(byte SR, byte BaseR, short offset6)
        {
            throw new NotImplementedException();
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
        public void trap(byte trapvect8)
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
        internal LC3Executer? executer;
        public LC3InstructionSet(ref Mem memTarget, ref RegisterBlock regTarget, OsHandler os) : base(ref memTarget, ref regTarget, os) { }

        public override void ExecuteInstruction(ulong instruction)
        {
            if (executer == null)
            {
                executer = new LC3Executer(regTarget, os, memTarget, debug, this);
            }
            executer.handle(instruction);
        }
        public override ulong GetOpcode(ulong instruction)
        {
            return (instruction >> 12) & 0b1111;
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
