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
            int shift = 32 - bitCount;
            return (UInt32)(((Int32)(value << shift)) >> shift);
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
        protected ushort imm => (ushort)ReadBits(bits, 20, 31);
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
        protected ushort imm => (ushort)((ReadBits(bits,  7, 11) << 0) | 
                                      (ReadBits(bits, 25, 31) << 5));
        public Int32 simm => SEXT(imm, 12);
        public UInt32 zimm => ZEXT(imm, 12);
        public ushort rd => (ushort)ReadBits(bits, 7, 11); //PART OF imm
        public ushort funct3 => (ushort)ReadBits(bits, 12, 14);
        public ushort rs1 => (ushort)ReadBits(bits, 15, 19);
        public ushort rs2 => (ushort)ReadBits(bits, 20, 24);
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_S: rs1=0x{rs1:X1}, funct3=0x{funct3:X1}, rs2=0x{rs2:X1}, funct3=0x{funct3:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rs2}, {(signedImm ? simm : zimm)}({rs1})");
        }
    }
    public class Instruction_B(Instruction inst) : Instruction(inst.bits)
    {
        protected ushort imm => (ushort)((ReadBits(bits,  7,  7) << 11) |
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
        protected ushort imm => (ushort)(ReadBits(bits, 12, 19) << 12 |
                                      ReadBits(bits, 20, 20) << 11 |
                                      ReadBits(bits, 21, 30) <<  1 |
                                      ReadBits(bits, 31, 31) << 20);
        public Int32 simm => (Int32)imm;
        public UInt32 zimm => imm;
        public new void DEBUG_PRINT()
        {
            Debug.WriteLine($"Instruction_J: rd=0x{rd:X1}, imm=0x{imm:X4}");
        }
        public void DEBUG_PRINT(string instructionName, bool signedImm = true)
        {
            Debug.WriteLine($"called instruction: {instructionName}, X{rd}, {(signedImm ? simm : zimm)}");
        }
    }
    public class RV32I(Mem memTarget, OsHandler os, RegisterBlock regs, RiscV_32 instructionSet) : RiscV_32_Extension(memTarget, os, regs, instructionSet)
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
                    LUI(inst_U); return;
                case 0b0010111: // AUIPC
                    AUIPC(inst_U); return;
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
                    LOAD(inst_I); return;
                case 0b0100011: //sb - sh - sw
                    STORE(inst_S); return;
                case 0b1101111: //jal
                    JAL(inst_J); return;
                case 0b1100111: //jalr
                    JALR(inst_I); return;
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
        private void LUI(Instruction_U inst)
        {
            inst.DEBUG_PRINT("LUI");
            regs_shared.Write(inst.rd, inst.zimm);
        }
        private void AUIPC(Instruction_U inst)
        {
            inst.DEBUG_PRINT("AUIPC");
            regs_shared.Write(inst.rd, instructionSet.GetPC() + inst.zimm);
        }
        private void ADDI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("ADDI",true);
            regs_shared.Write(inst.rd, (UInt32)((Int32)regs_shared.Read(inst.rs1) + (Int32)inst.simm));
        }
        private void SLTI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("SLTI",true);
            regs_shared.Write(inst.rd, ((Int32)regs_shared.Read(inst.rs1) < (Int32)inst.simm) ? 1ul : 0ul);
        }
        private void SLTIU(Instruction_I inst)
        {
            inst.DEBUG_PRINT("SLTIU",true);
            regs_shared.Write(inst.rd, ((UInt32)regs_shared.Read(inst.rs1) < (UInt32)inst.zimm) ? 1ul : 0ul);
        }
        private void XORI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("XORI",true);
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) ^ (UInt32)inst.simm);
        }
        private void ORI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("ORI",true);
            regs_shared.Write(inst.rd, (UInt32)regs_shared.Read(inst.rs1) | (UInt32)inst.simm);
        }
        private void ANDI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("ANDI",true);
            regs_shared.Write(inst.rd, (UInt32)regs_shared.Read(inst.rs1) & (UInt32)inst.simm);
        }
        private void SLLI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("SLLI");
            regs_shared.Write(inst.rd, (UInt32)regs_shared.Read(inst.rs1) << (int)inst.zimm);
        }
        private void SRLI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("SRLI");
            regs_shared.Write(inst.rd,(UInt32)regs_shared.Read(inst.rs1) >> (int)inst.zimm);
        }
        private void SRAI(Instruction_I inst)
        {
            inst.DEBUG_PRINT("SRAI");
            regs_shared.Write(inst.rd, (UInt32)inst.SEXT(((UInt32)regs_shared.Read(inst.rs1)) >> (int)inst.zimm, 32 - (int)inst.zimm));
        }
        private void ADD(Instruction_R inst)
        {
            inst.DEBUG_PRINT("ADD");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) + regs_shared.Read(inst.rs2));
        }
        private void SUB(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SUB");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) - regs_shared.Read(inst.rs2));
        }
        private void SLL(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SLL");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) << (int)(regs_shared.Read(inst.rs2) & 0b11111));
        }
        private void SLT(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SLT");
            regs_shared.Write(inst.rd, ((long)regs_shared.Read(inst.rs1) < (long)regs_shared.Read(inst.rs2)) ? 1ul : 0ul);
        }
        private void SLTU(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SLTU");
            regs_shared.Write(inst.rd, (regs_shared.Read(inst.rs1) < regs_shared.Read(inst.rs2)) ? 1ul : 0ul);
        }
        private void XOR(Instruction_R inst)
        {
            inst.DEBUG_PRINT("XOR");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) ^ regs_shared.Read(inst.rs2));
        }
        private void SRL(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SRL");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) >> (int)(regs_shared.Read(inst.rs2) & 0b11111));
        }
        private void SRA(Instruction_R inst)
        {
            inst.DEBUG_PRINT("SRA");
            regs_shared.Write(inst.rd, (UInt32)inst.SEXT(((UInt32)regs_shared.Read(inst.rs1)) >> (int)(regs_shared.Read(inst.rs2) & 0b11111), 32 - (int)(regs_shared.Read(inst.rs2) & (UInt32)0b11111)));
        }
        private void OR(Instruction_R inst)
        {
            inst.DEBUG_PRINT("OR");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) | regs_shared.Read(inst.rs2));
        }
        private void AND(Instruction_R inst)
        {
            inst.DEBUG_PRINT("AND");
            regs_shared.Write(inst.rd, regs_shared.Read(inst.rs1) & regs_shared.Read(inst.rs2));
        }
        private void ECALL()
        {
            UInt32 action = (UInt32)regs_shared.Read(17);
            UInt32 status = (UInt32)(regs_shared.Read(10) & 0xFFFFFFFF);
            Debug.WriteLine($"instruction called: ECALL; a7={{{action}}}; a0={{{status}}}");
            switch (action)
            {
                case 1:
                    os.WriteStringConsole($"{status}"); return;
                case 2:
                    os.WriteStringConsole($"{(float)System.BitConverter.ToSingle(BitConverter.GetBytes(status), 0)}"); return;
                case 4:
                    os.WriteStringConsole((UInt64) status); return;
                case 10:
                    throw new IOException("ECALL instruction encountered, no args.");
                case 11:
                    os.WriteStringConsole(string.Concat((char)(status & 0xff))); return;
                case 34:
                    os.WriteStringConsole($"{status:X}"); return;
                case 35:
                    os.WriteStringConsole($"{status:B}"); return;
                case 36:
                    os.WriteStringConsole($"{status:B}"); return;
                case 93:
                    throw new IOException($"ECALL instruction encountered, {status}.");
                default:
                    throw new Exception($"ECALL instruction encountered with unknown action: {action}.");
            }
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
            Debug.WriteLine($"instruction called: SFENCE.VMA");
            //TODO
        }
        private void LOAD(Instruction_I inst)
        {
            char[] sizeChar = { 'B', 'H', 'W' };
            inst.DEBUG_PRINT($"L{sizeChar[inst.funct3]}",true);
            ushort byteCnt = (ushort)((1 << inst.funct3) & 0b11);
            UInt64 val = mem.Read((UInt32)(regs_shared.Read(inst.rs1) + (UInt64)(UInt32)inst.simm)) & inst.mask(byteCnt);
            if ((inst.funct3 & 0b100) == 0)
                val = (UInt64)(UInt32)inst.SEXT((UInt32)val, byteCnt * 8);
            else
                val = inst.ZEXT((UInt32)val, byteCnt * 8);
            regs_shared.Write(inst.rd, val);
        }
        private void STORE(Instruction_S inst)
        {
            char[] sizeChar = { 'B', 'H', 'W' };
            inst.DEBUG_PRINT($"S{sizeChar[inst.funct3]}",true);
            ushort byteCnt = (ushort)(1 << inst.funct3);
            UInt32 addr = (UInt32)(regs_shared.Read(inst.rs1) + (UInt64)(UInt32)inst.simm);
            mem.Write(addr, regs_shared.Read(inst.rs2) & inst.mask(byteCnt * 8));
        }
        private void JAL(Instruction_J inst)
        {
            inst.DEBUG_PRINT("JAL",true);
            regs_shared.Write(inst.rd, (UInt32)instructionSet.GetPC() + 4);
            instructionSet.JumpRelative(inst.simm);
        }
        private void JALR(Instruction_I inst)
        {
            inst.DEBUG_PRINT("JALR",true);
            regs_shared.Write(inst.rd, (UInt32)instructionSet.GetPC() + 4);
            instructionSet.JumpOverride((regs_shared.Read(inst.rs1) + (UInt64)(UInt32)inst.simm) & ~1ul);
        }
        private void BEQ(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BEQ",true);
            if((Int32)regs_shared.Read(inst.rs1) == (Int32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
        }
        private void BNE(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BNE",true);
            if((Int32)regs_shared.Read(inst.rs1) != (Int32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
        }
        private void BLT(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BLT",true);
            if((Int32)regs_shared.Read(inst.rs1) < (Int32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
        }
        private void BGE(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BGE",true);
            if((Int32)regs_shared.Read(inst.rs1) >= (Int32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
        }
        private void BLTU(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BLTU",true);
            if((UInt32)regs_shared.Read(inst.rs1) < (UInt32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
        }
        private void BGEU(Instruction_B inst)
        {
            inst.DEBUG_PRINT("BGEU",true);
            if((UInt32)regs_shared.Read(inst.rs1) >= (UInt32)regs_shared.Read(inst.rs2))
                instructionSet.JumpRelative(inst.simm);
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
        public RiscV_32(ref Mem memTarget_, OsHandler os) : base(ref memTarget_, os)
        {
            regs_shared = new(32, 32,[false]);
            extensions = [new RV32I(memTarget_, os, regs_shared, this)];
        }
        public override bool ExecuteInstruction(ulong instruction)
        {
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
            IncrimentPC();
            return ExecuteInstruction((ulong)instruction);
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
            JumpRelative(4);
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

        public override void JumpRelative(long offset)
        {
            JumpOverride(PC + (UInt32)offset);
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
