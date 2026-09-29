using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace assemblySimulator
{
    internal struct AddressLock
    {
        public Thread memUser;
        public UInt64 address;
    }
    public class Mem
    {
        readonly public short bytesPerAddress;
        private readonly Mutex memInUse;
        private Thread? memUser;
        private int recursiveLockCount = 0;
        private MemBlock MemBlocks;
        private readonly LinkedList<AddressLock> AddressLocks = new();
        
        private class MemBlock
        {
            public UInt64 initialAddress;
            public UInt64 addressCount;
            public short bytesPerAddress;
            public MemBlock? nextBlock, prevBlock;
            public byte[] data;
            public MemBlock(UInt64 initialAddress, UInt64 addressCount, short bytesPerAddress, MemBlock? prevBlock)
            {
                this.initialAddress = initialAddress;
                this.addressCount = addressCount;
                this.bytesPerAddress = bytesPerAddress;
                this.nextBlock = null;
                this.prevBlock = prevBlock;
                prevBlock?.nextBlock = this;
                this.data = new byte[addressCount * (UInt64)bytesPerAddress];
            }
            public UInt64 Read(UInt64 address)
            {
                if (address < this.initialAddress || address >= this.initialAddress + this.addressCount)
                {
                    throw new Exception("Address out of range");
                }
                UInt64 offset = address - this.initialAddress;
                UInt64 value = 0;
                for (short i = 0; i < this.bytesPerAddress; i++)
                {
                    value |= (UInt64)this.data[offset * (UInt64)this.bytesPerAddress + (UInt64)i] << (int)(i * 8);
                }
                return value;
            }
            public void Write(UInt64 address, UInt64 data)
            {
                if (address < this.initialAddress || address >= this.initialAddress + this.addressCount)
                {
                    throw new Exception("Address out of range");
                }
                UInt64 offset = address - this.initialAddress;
                for (short i = 0; i < this.bytesPerAddress; i++)
                {
                    this.data[offset * (UInt64)this.bytesPerAddress + (UInt64)i] = (byte)((data & (UInt64)((UInt64)(0xFF) << (int)(i * 8))) >> (int)(i * 8));
                }
            }
        }
        public Mem(UInt64 maxAddress, short bytesPerAddress, UInt64 blockSize)
        {
            this.bytesPerAddress = bytesPerAddress;
            this.memInUse = new Mutex(false);
            if (blockSize == 0)
            {
                MemBlocks = new MemBlock(0, maxAddress, bytesPerAddress, null);
                return;
            }
            while (true)
            {
                MemBlock newBlock = new MemBlock(MemBlocks == null ? 0 : MemBlocks.initialAddress + MemBlocks.addressCount, blockSize, bytesPerAddress, MemBlocks);
                MemBlocks = newBlock;
                if (newBlock.initialAddress + newBlock.addressCount >= maxAddress)
                {
                    break;
                }
            }
        }
        public bool LockAddress(UInt64 address)
        {
            foreach(AddressLock locks in AddressLocks)
            {
                if (locks.address == address && locks.memUser == Thread.CurrentThread)
                {
                    return true;
                }else if (locks.address == address)
                {
                    return false;
                }
            }
            AddressLocks.AddLast(new AddressLock() { address = address, memUser = Thread.CurrentThread });
            return true;
        }
        public bool UnlockAddress(UInt64 address)
        {
            foreach (AddressLock locks in AddressLocks)
            {
                if (locks.address == address && locks.memUser == Thread.CurrentThread)
                {
                    AddressLocks.Remove(locks);
                    return true;
                } else if (locks.address == address)
                {
                    return false;
                }
            }
            return false;
        }
        private bool LockMem(UInt64 address)
        {
            foreach (AddressLock locks in AddressLocks)
            {
                if (locks.address == address && locks.memUser != Thread.CurrentThread)
                {
                    return false;
                }
            }
            if (this.memUser != null && this.memUser == Thread.CurrentThread)
            {
                this.recursiveLockCount++;
                return true;
            }else if (this.memInUse.WaitOne())
            {
                this.memUser = Thread.CurrentThread;
                this.recursiveLockCount = 1;
                return true;
            }
            else
            {
                return false;
            }
        }
        private void UnlockMem()
        {
            if (this.recursiveLockCount < 0)
            {
                throw new Exception("Memory module mutex broken on unlock, recursive lock count < 0");
            }
            if (this.memUser != Thread.CurrentThread)
            {
                throw new Exception("Memory module mutex broken on unlock, incorrect user");
            }
            this.recursiveLockCount--;
            if (this.recursiveLockCount == 0)
            {
                this.memUser = null;
                this.memInUse.ReleaseMutex();
            }
        }
        public UInt64 Read(UInt64 address)
        {
            if (this.LockMem(address))
            {
                FindTarget(address);
                UInt64 output = MemBlocks.Read(address);
                this.UnlockMem();
                if (output >= (1ul << (bytesPerAddress * 8)) && bytesPerAddress != 8)
                {
                    throw new Exception("Memory module read value exceeds maximum value for bytes per address");
                }
                return output;
            }
            return 0;
        }
        public void Write(UInt64 address, UInt64 value)
        {
            Debug.WriteLine($"Writing {value:X4} to address {address:X4}");
            if (value >= (1ul << (bytesPerAddress * 8)) && bytesPerAddress != 8)
            {
                throw new Exception("Memory module write value exceeds maximum value for bytes per address");
            }
            if (this.LockMem(address))
            {
                FindTarget(address);
                MemBlocks.Write(address, value);
                this.UnlockMem();
                return;
            }
        }
        private void FindTarget(UInt64 address)
        {
            while (address >= MemBlocks.initialAddress + MemBlocks.addressCount)
            {
                if (MemBlocks.nextBlock == null)
                {
                    throw new Exception("Address out of range");
                }
                MemBlocks = MemBlocks.nextBlock;
            }
            while (address < MemBlocks.initialAddress)
            {
                if (MemBlocks.prevBlock == null)
                {
                    throw new Exception("Address out of range");
                }
                MemBlocks = MemBlocks.prevBlock;
            }
        }
        public void FillMem(byte[] data)
        {
            if ((UInt64)data.Length > MemBlocks.addressCount * (UInt64)bytesPerAddress)
            {
                throw new Exception("Data too large to fit in memory");
            }
            for (UInt64 i = 0; i < (UInt64)data.Length; i += (UInt64)bytesPerAddress)
            {
                UInt64 value = 0;
                for (short j = 0; j < bytesPerAddress; j++)
                {
                    if (i + (UInt64)j >= (UInt64)data.Length)
                    {
                        break;
                    }
                    if (value >= (1ul << (bytesPerAddress * 8)) && bytesPerAddress != 8)
                    {
                        throw new Exception("Memory module write value exceeds maximum value for bytes per address");
                    }
                    value <<= j * 8;
                    value |= (UInt64)data[i + (UInt64)j];
                }
                Write(i / (UInt64)bytesPerAddress, value);
            }
        }
    }
}
