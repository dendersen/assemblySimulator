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
        protected UInt32 mask(int bitCount)
        {
            return (1u << bitCount) - 1;
        }
        public UInt32 ReadBits(UInt32 bits, int start, int end)
        {
            return ((bits & mask(end + 1)) >> (start));
        }
        public ushort opcode => (ushort)ReadBits(bits,  0, 6);
        public void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction: bits={bits:X8}, opcode={opcode:X2}");
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
            Debug.WriteLine($"Instruction_R: rd={rd:X1}, funct3={funct3:X1}, rs1={rs1:X1}, rs2={rs2:X1}, funct7={funct7:X2}");
        }
    }
    public class Instruction_I(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort imm => (ushort)ReadBits(bits, 20, 31);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_I: rd={rd:X1}, funct3={funct3:X1}, rs1={rs1:X1}, imm={imm:X4}");
        }
    }
    public class Instruction_S(Instruction inst) : Instruction(inst.bits)
    {
        public ushort imm => (ushort)((ReadBits(bits,  7, 11) << 0) | 
                                      (ReadBits(bits, 25, 31) << 5));
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_S: rs1={rs1:X1}, funct3={funct3:X1}, rs2={rs2:X1}, funct3={funct3:X1}, imm={imm:X4}");
        }
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
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_B: rs1={rs1:X1}, funct3={funct3:X1}, rs2={rs2:X1}, imm={imm:X4}");
        }
    }
    public class Instruction_U(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort imm => (ushort)((ReadBits(bits, 12, 19) << 12) |
                                      (ReadBits(bits, 20, 20) << 11) |
                                      (ReadBits(bits, 21, 30) << 1) |
                                      (ReadBits(bits, 31, 31) << 20));
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_U: rd={rd:X1}, imm={imm:X4}");
        }
    }
    public class Instruction_J(Instruction inst) : Instruction(inst.bits)
    {
        public ushort rd => (ushort)ReadBits(bits, 7, 11);
        public ushort imm => (ushort)((ReadBits(bits, 12, 31) << 12));
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_J: rd={rd:X1}, imm={imm:X4}");
        }
    }
    public class RV32I(Mem memTarget, OsHandler os, RegisterBlock regs) : RiscV_32_Extension(memTarget, os, regs)
    {
        public override bool Validate(Instruction inst)
        {
            Instruction_R inst_R = new(inst);
            Instruction_I inst_I = new(inst);
            Instruction_S inst_S = new(inst);
            Instruction_B inst_B = new(inst);
            //Instruction_U inst_U = new(inst); not in RV32I
            //Instruction_J inst_J = new(inst); not in RV32I
            switch (inst.opcode)
            {
                case 0b0110111: // LUI
                case 0b0010111: // AUIPC
                    return true;
                case 0b0010011: //addi - slti - sltiu - xori - ori - andi - slli - srli - srai
                    if (new HashSet<ushort> { 0b000, 0b010, 0b011, 0b100, 0b110, 0b111 }.Contains(inst_I.funct3))
                    {//addi - slti - sltiu - xori - ori - andi
                        return true;
                    }
                    if (inst_I.funct3 == 0b001)
                    { //slli
                        return inst.ReadBits(inst.bits, 25, 31) == 0; 
                    }
                    if (inst_I.funct3 == 0b101)
                    { //srli - srai
                        return (inst.ReadBits(inst.bits, 25, 31)  & ~(0b1<<5)) == 0;
                    }
                    return false;
                case 0b0110011: //add - sub - sll - slt - sltu - xor - srl - sra - or - and
                    if (new HashSet<ushort> { 0b000, 0b001, 0b010, 0b011, 0b100, 0b101, 0b110, 0b111 }.Contains(inst_R.funct3) &&
                        inst.ReadBits(inst.bits,31,25) == 0)
                    {
                        return true;
                    }
                    if (new HashSet<ushort> { 0b000, 0b101 }.Contains(inst_R.funct3) &&
                        (inst.ReadBits(inst.bits, 31, 25) & ~(0b1 << 5)) == 0)
                    {
                        return true;
                    }
                    return false;
                case 0b1110011: //ecall - uret - sret - mret - wfi - sfence.vma
                    if (inst.ReadBits(inst.bits, 25, 31) == (1 << 0 | 1 << 3))
                    {
                        return true; //sfence.vma
                    }
                    //ecall - uret - sret - mret - wfi
                    return new HashSet<UInt32> { 0, 1 << 15, 1 << 15 | 1 << 22, 1 << 15 | 1 << 22 | 1 << 23, 1 << 22 | 1 << 16 | 1 << 14}.Contains(inst.ReadBits(inst.bits, 7, 31));
                case 0b0000011: //lb - lh - lw - lbu - lhu
                    return new HashSet<ushort> { 0b000, 0b001, 0b010, 0b100, 0b101 }.Contains(inst_I.funct3);
                case 0b0100011: //sb - sh - sw
                    return new HashSet<ushort> { 0b000, 0b001, 0b010 }.Contains(inst_S.funct3);
                case 0b1101111: //jal
                    return true;
                case 0b1100111: //jalr
                    return inst_I.funct3 == 0b000;
                case 0b1100011: //beq - bne - blt - bge - bltu - bgeu
                    return new HashSet<ushort> { 0b000, 0b001, 0b100, 0b101, 0b110, 0b111 }.Contains(inst_B.funct3);
            }
            return false;
        }
        public override void Handle(Instruction inst)
        {
            Instruction_R inst_R = new(inst);
            Instruction_I inst_I = new(inst);
            Instruction_S inst_S = new(inst);
            Instruction_B inst_B = new(inst);
            Instruction_U inst_U = new(inst);
            Instruction_J inst_J = new(inst);
            switch (inst.opcode)
            {
                case 0b0110111: // LUI
                    LUI(inst_J); return;
                case 0b0010111: // AUIPC
                    AUIPC(inst_J); return;
                case 0b0010011: //addi - slti - sltiu - xori - ori - andi - slli - srli - srai
                    switch (inst_I.funct3)
                    {
                        case 0b000: //addi
                            ADDI(inst_I); return;
                        case 0b010: //slti
                            SLTI(inst_I); return;
                        case 0b011: //sltiu
                            SLTIU(inst_I); return;
                        case 0b100: //xori
                            XORI(inst_I); return;
                        case 0b110: //ori
                            ORI(inst_I); return;
                        case 0b111: //andi
                            ANDI(inst_I); return;
                        case 0b001: //slli
                            SLLI(inst_I); return;
                        case 0b101: //srli - srai
                            if (inst.ReadBits(inst.bits, 30, 30) == 0)
                            {
                                SRLI(inst_I); return;
                            }
                            else
                            {
                                SRAI(inst_I); return;
                            }
                        default:
                            throw new NotImplementedException("Instruction not implemented.");
                    }
                case 0b0110011: //add - sub - sll - slt - sltu - xor - srl - sra - or - and
                    switch(inst_R.funct3)
                    {
                        case 0b000: //add - sub
                            if (inst.ReadBits(inst.bits, 30, 30) == 0)
                                { ADD(inst_R); return; }
                                else
                                { SUB(inst_R); return; }
                        case 0b001: //sll
                            SLL(inst_R); return;
                        case 0b010: //slt
                            SLT(inst_R); return;
                        case 0b011: //sltu
                            SLTU(inst_R); return;
                        case 0b100: //xor
                            XOR(inst_R); return;
                        case 0b101: //srl - sra
                            if (inst.ReadBits(inst.bits, 30, 30) == 0)
                                { SRL(inst_R); return; }
                                else
                                { SRA(inst_R); return; }
                        case 0b110: //or
                            OR(inst_R); return;
                        case 0b111: //and
                            AND(inst_R); return;
                        default:
                            throw new NotImplementedException("Instruction not implemented.");
                    }
                    case 0b1110011: //ecall - uret - sret - mret - wfi - sfence.vma
                    if (inst.ReadBits(inst.bits, 7,31) == 0)
                    {
                        ECALL(); return;
                    }
                    if (inst.ReadBits(inst.bits, 27, 31) == 0b0001001)
                    {
                        SFENCE_VMA(); return;
                    }
                    switch (inst.ReadBits(inst.bits, 20, 31))
                    {
                        case 1 << 1:
                            URET(); return;
                        case 1 << 1 | 1 << 8:
                            SRET(); return;
                        case 1 << 1 | 1 << 8 | 1 << 9:
                            MSRET(); return;
                        case 1 << 0 | 1 << 2 | 1 << 8:
                            WFI(); return;
                        default:
                            throw new NotImplementedException("Instruction not implemented.");
                    }
                case 0b0000011: //lb - lh - lw - lbu - lhu
                    LOAD(inst_R); return;
                case 0b0100011: //sb - sh - sw
                    STORE(inst_R); return;
                case 0b1101111: //jal
                    JAL(inst_U); return;
                case 0b1100111: //jalr
                    JALR(inst_S); return;
                case 0b1100011: //beq - bne - blt - bge - bltu - bgeu
                    switch (inst_B.funct3)
                    {
                        case 0b000: //beq
                            BEQ(inst_B); return;
                        case 0b001: //bne
                            BNE(inst_B); return;
                        case 0b100: //blt
                            BLT(inst_B); return;
                        case 0b101: //bge
                            BGE(inst_B); return;
                        case 0b110: //bltu
                            BLTU(inst_B); return;
                        case 0b111: //bgeu
                            BGEU(inst_B); return;
                        default:
                            throw new NotImplementedException("Instruction not implemented.");
                    }
            }
        }
        private void LUI(Instruction_J inst)
        {
            Debug.WriteLine($"instruction called: LUI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void AUIPC(Instruction_J inst)
        {
            Debug.WriteLine($"instruction called: AUIPC");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void ADDI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: ADDI");

            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLTI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: SLTI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLTIU(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: SLTIU");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void XORI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: XORI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void ORI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: ORI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void ANDI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: ANDI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLLI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: SLLI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SRLI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: SRLI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SRAI(Instruction_I inst)
        {
            Debug.WriteLine($"instruction called: SRAI");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void ADD(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: ADD");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SUB(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SUB");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLL(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SLL");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLT(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SLT");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SLTU(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SLTU");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void XOR(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: XOR");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SRL(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SRL");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void SRA(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: SRA");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void OR(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: OR");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void AND(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: AND");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void ECALL()
        {
            Debug.WriteLine($"instruction called: ECALL");
            //TODO
        }
        private void URET()
        {
            Debug.WriteLine($"instruction called: URET");
            //TODO
        }
        private void SRET()
        {
            Debug.WriteLine($"instruction called: SRET");
            //TODO
        }
        private void MSRET()
        {
            Debug.WriteLine($"instruction called: MSRET");
            //TODO
        }
        private void WFI()
        {
            Debug.WriteLine($"instruction called: WFI");
            //TODO
        }
        private void SFENCE_VMA()
        {
            //TODO
            Debug.WriteLine($"instruction called: SFENCE.VMA");
        }
        private void LOAD(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: LOAD");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void STORE(Instruction_R inst)
        {
            Debug.WriteLine($"instruction called: STORE");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void JAL(Instruction_U inst)
        {
            Debug.WriteLine($"instruction called: JAL");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void JALR(Instruction_S inst)
        {
            Debug.WriteLine($"instruction called: JALR");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BEQ(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BEQ");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BNE(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BNE");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BLT(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BLT");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BGE(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BGE");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BLTU(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BLTU");
            inst.DEBUG_PRINT();
            //TODO
        }
        private void BGEU(Instruction_B inst)
        {
            Debug.WriteLine($"instruction called: BGEU");
            inst.DEBUG_PRINT();
            //TODO
        }
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
        internal RegisterBlock regs_shared = new(32, 32,[false]);
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
        public override void Execute()
        {
            UInt16 instruction = (UInt16)ReadValueFromMem(PC, 4);
            IncrimentPC();
            ExecuteInstruction((ulong)instruction);
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
                memTarget.Write(address + (ulong)i, (value >> (8 * i)));
            }
        }
    }
}
