
using assemblySimulator;
using System.Collections.Frozen;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace RISC_V
{

    public abstract class InstructionExecutor()
    {
        public abstract bool IsValid(Instruction inst);
        public abstract void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os);
    }


    internal class LUI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            return inst.opcode == 0b0110111; // LUI opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_U inst_u = new(inst);
            inst_u.DEBUG_PRINT("LUI",false);
            regs.Write(inst_u.rd, (UInt32)inst_u.simm);
        }
    }
    internal class AUIPC : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            return inst.opcode == 0b0010111; // AUIPC opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_U inst_u = new(inst);
            inst_u.DEBUG_PRINT("AUIPC", false);
            regs.Write(inst_u.rd, instructionSet.GetPC() + (UInt32)inst_u.simm);
        }
    }
    internal class ADDI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // ADDI opcode
                   inst_i.funct3 == 0x0;       // ADDI funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("ADDI",true);
            regs.Write(inst_i.rd, (UInt32)((Int32)regs.Read(inst_i.rs1) + (Int32)inst_i.simm));
        }
    }
    internal class SLTI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // SLTI opcode 
                   inst_i.funct3 == 0x2;       // SLTI funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("SLTI",true);
            regs.Write(inst_i.rd, ((Int32)regs.Read(inst_i.rs1) < (Int32)inst_i.simm) ? 1ul : 0ul);
        }
    }
    internal class SLTIU : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // SLTIU opcode
                   inst_i.funct3 == 0x3;         // SLTIU funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("SLTIU",true);
            regs.Write(inst_i.rd, ((UInt32)regs.Read(inst_i.rs1) < (UInt32)inst_i.zimm) ? 1ul : 0ul);
        }
    }
    internal class XORI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // XORI opcode
                   inst_i.funct3 == 0x4;       // XORI funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("XORI",true);
            regs.Write(inst_i.rd, regs.Read(inst_i.rs1) ^ (UInt32)inst_i.simm);
        }
    }
    internal class ORI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // ORI opcode
                   inst_i.funct3 == 0x6;       // ORI funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("ORI",true);
            regs.Write(inst_i.rd, (UInt32)regs.Read(inst_i.rs1) | (UInt32)inst_i.simm);
        }
    }
    internal class ANDI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0010011 && // ANDI opcode
                   inst_i.funct3 == 0x7;         // ANDI funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("ANDI",true);
            regs.Write(inst_i.rd, (UInt32)regs.Read(inst_i.rs1) & (UInt32)inst_i.simm);
        }
    }
    internal class SLLI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            ushort funct7 = (ushort)inst.ReadBits(inst.bits, 25, 31);
            return inst_i.opcode == 0b0010011 && // SLLI opcode
                   inst_i.funct3 == 0x1       && // SLLI funct3
                     funct7 == 0x00;       // SLLI funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            ushort shamt = (ushort)inst.ReadBits(inst.bits, 20, 24);
            inst_i.DEBUG_PRINT("SLLI",true);
            regs.Write(inst_i.rd, (UInt32)regs.Read(inst_i.rs1) << shamt);
        }
    }
    internal class SRLI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            ushort funct7 = (ushort)inst.ReadBits(inst.bits, 25, 31);
            return inst_i.opcode == 0b0010011 && // SRLI opcode
                   inst_i.funct3 == 0x5       && // SRLI funct3
                     funct7 == 0x00;       // SRLI funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            ushort shamt = (ushort)inst.ReadBits(inst.bits, 20, 24);
            inst_i.DEBUG_PRINT("SRLI",true);
            regs.Write(inst_i.rd, (UInt32)regs.Read(inst_i.rs1) >> shamt);
        }
    }
    internal class SRAI : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            ushort funct7 = (ushort)inst.ReadBits(inst.bits, 25, 31);
            return inst_i.opcode == 0b0010011 && // SRAI opcode
                   inst_i.funct3 == 0x5 &&        // SRAI funct3
                     funct7 == 0x20;         // SRAI funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            ushort shamt = (ushort)inst.ReadBits(inst.bits, 20, 24);
            inst_i.DEBUG_PRINT("SRAI",true);
            regs.Write(inst_i.rd, (UInt32)inst_i.SEXT(((UInt32)regs.Read(inst_i.rs1)) >> shamt, 32 - shamt));
        }
    }
    internal class ADD : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // ADD opcode
                   inst_r.funct3 == 0x0 &&     // ADD funct3
                   inst_r.funct7 == 0b0000000;   // ADD funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("ADD");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) + regs.Read(inst_r.rs2));
        }
    }
    internal class SUB : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SUB opcode
                   inst_r.funct3 == 0b000 &&     // SUB funct3
                   inst_r.funct7 == 0x20;   // SUB funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SUB");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) - regs.Read(inst_r.rs2));
        }
    }
    internal class SLL : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SLL opcode
                   inst_r.funct3 == 0x1 &&     // SLL funct3
                   inst_r.funct7 == 0x0;   // SLL funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SLL");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) << (int)(regs.Read(inst_r.rs2) & 0b11111));
        }
    }
    internal class SLT : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SLT opcode
                   inst_r.funct3 == 0x2 &&     // SLT funct3
                   inst_r.funct7 == 0x0;   // SLT funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SLT");
            regs.Write(inst_r.rd, ((long)regs.Read(inst_r.rs1) < (long)regs.Read(inst_r.rs2)) ? 1ul : 0ul);
        }
    }
    internal class SLTU : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SLTU opcode
                   inst_r.funct3 == 0x3 &&     // SLTU funct3
                   inst_r.funct7 == 0x0;   // SLTU funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SLTU");
            regs.Write(inst_r.rd, (regs.Read(inst_r.rs1) < regs.Read(inst_r.rs2)) ? 1ul : 0ul);
        }
    }
    internal class XOR : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // XOR opcode
                   inst_r.funct3 == 0x4 &&     // XOR funct3
                   inst_r.funct7 == 0x0;   // XOR funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("XOR");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) ^ regs.Read(inst_r.rs2));
        }
    }
    internal class SRL : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SRL opcode
                   inst_r.funct3 == 0x5 &&     // SRL funct3
                   inst_r.funct7 == 0x0;   // SRL funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SRL");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) >> (int)(regs.Read(inst_r.rs2) & 0b11111));
        }
    }
    internal class SRA : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // SRA opcode
                   inst_r.funct3 == 0x5 &&     // SRA funct3
                   inst_r.funct7 == 0x20;   // SRA funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("SRA");
            regs.Write(inst_r.rd, (UInt32)inst_r.SEXT(((UInt32)regs.Read(inst_r.rs1)) >> (int)(regs.Read(inst_r.rs2) & 0b11111), 32 - (int)(regs.Read(inst_r.rs2) & (UInt32)0b11111)));
        }
    }
    internal class OR : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // OR opcode
                   inst_r.funct3 == 0x6 &&     // OR funct3
                   inst_r.funct7 == 0x0;   // OR funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("OR");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) | regs.Read(inst_r.rs2));
        }
    }
    internal class AND : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_R inst_r = new(inst);
            return inst_r.opcode == 0b0110011 && // AND opcode
                   inst_r.funct3 == 0x7 &&     // AND funct3
                   inst_r.funct7 == 0x0;   // AND funct7
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_R inst_r = new(inst);
            inst_r.DEBUG_PRINT("AND");
            regs.Write(inst_r.rd, regs.Read(inst_r.rs1) & regs.Read(inst_r.rs2));
        }
    }
    internal class ECAL : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b1110011 && // ECALL opcode
                   inst_i.funct3 == 0x0 &&       // ECALL funct3
                   inst_i.zimm == 0x0;            // ECALL imm
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            UInt32 action = (UInt32)regs.Read(17);
            UInt32 status = (UInt32)(regs.Read(10) & 0xFFFFFFFF);
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
                    throw new IOException($"ECALL instruction encountered with unknown action: {action}.");
            }
        }
    }
    internal class LOAD : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b0000011; // LOAD opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            char[] sizeChar = ['B', 'H', 'W'];
            string sign = (inst_i.funct3 & 0b100) == 0 ? "" : "U";
            inst_i.DEBUG_PRINT($"L{sizeChar[inst_i.funct3 & 0b11]}{ sign }",true);
            ushort byteCnt = (ushort)(1 << (inst_i.funct3 & 0b11));
            UInt64 addr = (UInt32)(regs.Read(inst_i.rs1) + (UInt64)(UInt32)inst_i.simm);
            UInt64 val = instructionSet.ReadValueFromMem(addr, byteCnt) & inst_i.mask(byteCnt * 8);
            if ((inst_i.funct3 & 0b100) == 0)
                val = (UInt64)(UInt32)inst_i.SEXT((UInt32)val, byteCnt * 8);
            else
                val = inst_i.ZEXT((UInt32)val, byteCnt * 8);
            regs.Write(inst_i.rd, val);
        }
    }
    internal class STORE : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_S inst_s = new(inst);
            return inst_s.opcode == 0b0100011; // STORE opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_S inst_s = new(inst);
            char[] sizeChar = ['B', 'H', 'W'];
            string sign = (inst_s.funct3 & 0b100) == 0 ? "" : "U";
            inst_s.DEBUG_PRINT($"S{sizeChar[inst_s.funct3 & 0b11]}{ sign }",true);
            ushort byteCnt = (ushort)(1 << (inst_s.funct3 & 0b11));
            UInt32 addr = (UInt32)(regs.Read(inst_s.rs1) + (UInt64)(UInt32)inst_s.simm);
            UInt64 val = regs.Read(inst_s.rs2) & inst_s.mask(byteCnt * 8);
            instructionSet.WriteValueToMem(addr, val, byteCnt);
        }
    }
    internal class JAL : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_J inst_j = new(inst);
            return inst_j.opcode == 0b1101111; // JAL opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_J inst_j = new(inst);
            inst_j.DEBUG_PRINT("JAL",true);
            regs.Write(inst_j.rd, (UInt32)instructionSet.GetPC() + 4);
            instructionSet.JumpRelative(inst_j.simm,true);
        }
    }
    internal class JALR : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_I inst_i = new(inst);
            return inst_i.opcode == 0b1100111; // JALR opcode
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_I inst_i = new(inst);
            inst_i.DEBUG_PRINT("JALR",true);
            regs.Write(inst_i.rd, (UInt32)instructionSet.GetPC());
            instructionSet.JumpOverride((regs.Read(inst_i.rs1) + (UInt64)(UInt32)inst_i.simm) & ~1ul, true);
        }
    }
    internal class BEQ : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && //BEQ opcode
                   inst_b.funct3 == 0x0;        // BEQ funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BEQ",true);
            if((Int32)regs.Read(inst_b.rs1) == (Int32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }
    internal class BNE : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && // BNE opcode
                   inst_b.funct3 == 0x1; // BNE funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BNE",true);
            if((Int32)regs.Read(inst_b.rs1) != (Int32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }
    internal class BLT : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && //BLT opcode
                   inst_b.funct3 == 0x4; // BLT funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BLT",true);
            if((Int32)regs.Read(inst_b.rs1) < (Int32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }
    internal class BGE : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && // BGE opcode
                   inst_b.funct3 == 0x5; // BGE funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BGE",true);
            if((Int32)regs.Read(inst_b.rs1) >= (Int32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }
    internal class BLTU : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && // BLTU opcode
                   inst_b.funct3 == 0x6; // BLTU funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BLTU",true);
            if((UInt32)regs.Read(inst_b.rs1) < (UInt32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }
    internal class BGEU : InstructionExecutor{
        public override bool IsValid(Instruction inst){
            Instruction_B inst_b = new(inst);
            return inst_b.opcode == 0b1100011 && // BGEU opcode
                   inst_b.funct3 == 0x7; // BGEU funct3
        }
        public override void Execute(Instruction inst, RegisterBlock regs, RiscV_32 instructionSet, Mem mem, OsHandler os){
            Instruction_B inst_b = new(inst);
            inst_b.DEBUG_PRINT("BGEU",true);
            if((UInt32)regs.Read(inst_b.rs1) >= (UInt32)regs.Read(inst_b.rs2))
                instructionSet.JumpRelative(inst_b.simm,true);
        }
    }

public class RV32I(Mem memTarget, OsHandler os, RegisterBlock regs, RiscV_32 instructionSet) : RiscV_32_Extension(memTarget, os, regs, instructionSet)
    {
        InstructionExecutor[] executers = {
            new LUI(),
            new AUIPC(),
            new ADDI(),
            new SLTI(),
            new SLTIU(),
            new XORI(),
            new ORI(),
            new ANDI(),
            new SLLI(),
            new SRLI(),
            new SRAI(),
            new ADD(),
            new SUB(),
            new SLL(),
            new SLT(),
            new SLTU(),
            new XOR(),
            new SRL(),
            new SRA(),
            new OR(),
            new AND(),
            new ECAL(),
            new LOAD(),
            new STORE(),
            new JAL(),
            new JALR(),
            new BEQ(),
            new BNE(),
            new BLT(),
            new BGE(),
            new BLTU(),
            new BGEU(),
        };
        public override bool Validate(Instruction inst)
        {
            foreach (var executer in executers)
            {
                if (executer.IsValid(inst))
                    return true;
            }
            return false;
        }
        public override void Handle(Instruction inst)
        {
            foreach (var executer in executers)
            {
                if (executer.IsValid(inst))
                {
                    executer.Execute(inst, regs, instructionSet, mem, os);
                    return;
                }
            }
            throw new InvalidOperationException("Invalid instruction");
        }
    }
}
