using assemblySimulator;
using System.Runtime.InteropServices;

namespace RISC_V
{
    public class Instruction(UInt32 bits)
    {
        public UInt32 bits = bits;
        protected UInt32 mask(int bitCount)
        {
            return (1u << bitCount) - 1;
        }
        protected UInt32 ReadBits(UInt32 bits, int start, int end)
        {
            return ((bits & mask(end + 1)) >> (start));
        }
        public ushort opcode => (ushort)ReadBits(bits,  0, 6);
    }
    public class Instruction_R(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public ushort funct7 => (ushort)ReadBits(bits, 25, 31);
    }
    public class Instruction_I(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort imm => (ushort)ReadBits(bits, 20, 31);
    }
    public class Instruction_S(Instruction inst) : Instruction(inst.bits)
    {
        public ushort imm => (ushort)((ReadBits(bits,  7, 11) << 0) | 
                                      (ReadBits(bits, 25, 31) << 5));
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
    }
    public class Instruction_B(Instruction inst) : Instruction(inst.bits)
    {
        public ushort imm => (ushort)((ReadBits(bits,  7,  7) << 11) |
                                      (ReadBits(bits,  8, 11) <<  1) |
                                      (ReadBits(bits, 12, 14) <<  5) |
                                      (ReadBits(bits, 31, 31) <<  12));
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
    }
    public class Instruction_U(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort imm => (ushort)((ReadBits(bits, 12, 19) << 12) |
                                      (ReadBits(bits, 20, 20) << 11) |
                                      (ReadBits(bits, 21, 30) << 1) |
                                      (ReadBits(bits, 31, 31) << 20));
    }
    public class Instruction_J(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort imm => (ushort)((ReadBits(bits, 12, 31) << 12));
    }
    public abstract class RiscV_32_Extension(Mem memTarget, OsHandler os, RegisterBlock regs)
    {
        internal RegisterBlock regs_shared = regs;
        internal OsHandler os = os;
        internal Mem mem = memTarget;
        public abstract bool Validate(Instruction inst);
        public abstract void Handle(Instruction inst);
    }
    public class RiscV_32(ref Mem memTarget_, OsHandler os) : InstructionSet(ref memTarget_, os)
    {
        private List<RiscV_32_Extension> extensions = [];
        public static new readonly short memBytesPerAddress = 1;
        private UInt32 PC = 0;
        public override void ExecuteInstruction(ulong instruction)
        {
            bool handled = false;
            Instruction inst = new((UInt32)instruction);
            foreach (RiscV_32_Extension extension in extensions)
            {
                if (extension.Validate(inst))
                {
                    extension.Handle(inst);
                    handled = true;
                    break;
                }
            }
            if (!handled)
            {
                throw new NotImplementedException("Instruction not in any loaded extension.");
            } 
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
            PC += 4;
            if (PC >= memTarget.maxAddress)
            {
                throw new Exception("Program Counter exceeded memory size.");
            }
        }

        public override void JumpOverride(ulong address)
        {
            PC = (UInt32) address;
        }

        public override void JumpRelative(long offset)
        {
            PC += (UInt32)offset;
            if (PC >= memTarget.maxAddress)
            {
                throw new Exception("Program Counter exceeded memory size.");
            }
        }

        public override ulong[] ReadArrayFromMem(ulong startAddress, int bytesPerIndex, ulong length)
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
