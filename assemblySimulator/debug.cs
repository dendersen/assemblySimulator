using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace assemblySimulator
{
    public class DebugManager
    {
        private bool enabled = false;
        private LinkedList<ushort> registerReadStops = new();
        private LinkedList<ushort> registerWriteStops = new();
        public void AwaitHalt()
        {
            throw new NotImplementedException();
        }
        public void SetBreakOnRegRead(ushort regNum)
        {
            registerReadStops.AddLast(regNum);
        }
        public void SetBreakOnRegWrite(ushort regNum)
        {
            registerWriteStops.AddLast(regNum);
        }
        public void CallRegRead(ushort regNum, UInt64 value)
        {
            if (registerReadStops.Count == 0) return;
            if (registerReadStops.Contains(regNum))
            {
                this.CallSuccessReg(value, regNum, false);
            }
        }
        public void CallRegWrite(ushort regNum, UInt64 value)
        {
            if (registerWriteStops.Count == 0) return;
            if (registerWriteStops.Contains(regNum))
            {
                this.CallSuccessReg(value, regNum, true);
            }
        }
        private void CallSuccessReg(UInt64 value, ushort regNum, bool write)
        {
            Console.WriteLine("register breakpoint reached");
            Console.WriteLine("operation:", write ? "write" : "read");
            if (write)
            {
                Console.WriteLine("the new value being written: ", value);
            }
            else
            {
                Console.WriteLine("the current value being read: ", value);
            }
            this.Inspector();
        }
        public void Inspector()
        {
            this.enabled = false;
            this.Inspector_();
            this.enabled = true;
        }
        private void Inspector_()
        {
            while (true) {
                Console.WriteLine("\nwelcome to the debug terminal, please write a command, ? for help");
                string? userCommand = Console.ReadLine();
                if (userCommand == null) {
                    Console.WriteLine("unkown error, leaving terminal");
                    return;
                }
                string CMD = userCommand.Split(' ', 1)[0];
                string[] args = userCommand.Split(' ', 1)[1].Split(' ');
                switch (CMD) {
                    case "exit":
                        Console.WriteLine("exiting debug terminal");
                        return;
                    case "reg":
                        if (args.Length < 1)
                        {
                            Console.WriteLine("missing register number");
                            break;
                        }
                        
                        break;

                }
            }
        }
    }
}
