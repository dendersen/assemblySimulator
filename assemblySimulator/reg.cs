using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace assemblySimulator
{
    internal class Reg
    {
        private UInt64 value = 0;
        private readonly ushort length;
        public readonly bool canWrite = true;
        public Reg(ushort lenght)
        {
            this.length = lenght;
            if (this.length > 64)
            {
                throw new NotImplementedException("reg module does not support length over 64 bits");
            }
        }
        public Reg(ushort lenght, UInt64 initialValue)
        {
            this.length = lenght;
            this.value = initialValue;
            if (this.length > 64)
            {
                throw new NotImplementedException("reg module does not support length over 64 bits");
            }
        }
        public Reg(ushort lenght, bool canWrite, UInt64 readValue)
        {
            this.length = lenght;
            this.value = readValue;
            this.canWrite = canWrite;
            if (this.length > 64)
            {
                throw new NotImplementedException("reg module does not support length over 64 bits");
            }
        }
        public UInt64 GetValue()
        {
            return this.value;
        }
        public void SetValue(UInt64 value)
        {
            if (this.canWrite)
            {
                this.value = value & (UInt64)((Int64)(-1) >> (64 - this.length));
            }
        }
    }
    internal class RegisterBlock
    {
        private readonly Reg[] registers;
        private DebugManager debuger;
        public RegisterBlock(int registerCount, ushort registerLength)
        {
            this.registers = new Reg[registerCount];
            for (int i = 0; i < registerCount; i++)
            {
                this.registers[i] = new(registerLength);
            }
        }

        public RegisterBlock(ushort[] registerLength)
        {
            this.registers = new Reg[registerLength.Length];
            for (int i = 0; i < registerLength.Length; i++)
            {
                this.registers[i] = new(registerLength[i]);
            }
        }
        public RegisterBlock(ushort[] registerLength, bool[] writeable, UInt64[] initialValue)
        {
            this.registers = new Reg[registerLength.Length];
            for (int i = 0; i <= registerLength.Length; i++)
            {
                this.registers[i] = new(registerLength[i], writeable.Length >= i || writeable[i], initialValue.Length < i ? initialValue[i] : 0);
            }
        }
        public void SetDebugger(DebugManager debuger)
        {
            this.debuger = debuger;
        }
        public UInt64 Read(ushort reg)
        {
            this.debuger?.CallRegRead(reg, registers[reg].GetValue());
            return registers[reg].GetValue();
        }
        public void Write(ushort reg, UInt64 value)
        {
            this.debuger?.CallRegWrite(reg, value);
            registers[reg].SetValue(value);
        }
    }
}
