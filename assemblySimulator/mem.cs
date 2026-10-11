using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Runtime.ExceptionServices;

namespace assemblySimulator
{
    internal class ByteArray
    {
        private byte[]? data;
        private Dictionary<UInt64, byte>? hiddenData;
        private ArrayList? awaitingAccess;
        public readonly UInt64 Length;
        public ByteArray(UInt64 size)
        {
            this.data = new byte[size];
            this.Length = size;
        }
        public ByteArray(UInt64 size, MemOptimization optimization)
        {
            if (optimization == MemOptimization.hideZero)
            {
                this.data = null;
                this.hiddenData = [];
            }
            else if (optimization == MemOptimization.awaitAccessBlock)
            {
                this.data = null;
                this.awaitingAccess = [];
            }
            else if (optimization == MemOptimization.None || optimization == MemOptimization.Swap)
            {
                this.data = new byte[size];
            }
            else
            {
                throw new Exception("Invalid memory optimization mode");
            }
            this.Length = size;
        }
        public byte this[UInt64 index]
        {
            get
            {
                if (this.data != null)
                {
                    return this.data[index];
                }
                else if (this.hiddenData != null)
                {
                    if (hiddenData.TryGetValue(index, out byte value))
                    {
                        return value;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else if (this.awaitingAccess != null)
                {
                    if (this.awaitingAccess.Count < (int)index)
                    {
                        byte? value = (byte?)awaitingAccess[(int)index];
                        if (value == null)
                        {
                            throw new Exception("Memory access violation: address not yet accessed");
                        }
                        return value ?? 0;
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    throw new Exception("ByteArray is not initialized");
                }
            }
            set
            {
                if (this.data != null)
                {
                    this.data[index] = value;
                }
                else if (this.hiddenData != null)
                {
                    if (value == 0)
                    {
                        this.hiddenData.Remove(index);
                    }
                    else
                    {
                        this.hiddenData[index] = value;
                    }
                }
                else if (this.awaitingAccess != null)
                {
                    if (this.awaitingAccess.Count < (int)index)
                    {
                        this.awaitingAccess[(int)index] = value;
                    }
                    else
                    {
                        if (value == 0)
                        {
                            return;
                        }
                        else
                        {
                            while(this.awaitingAccess.Count <= (int)index)
                            {
                                this.awaitingAccess.Add(0);
                            }
                            this.awaitingAccess[(int)index] = value;
                        }
                    }
                }
                else
                {
                    throw new Exception("ByteArray is not initialized");
                }
            }
        }
        public byte[] ToArray()
        {
            if (this.data != null)
            {
                return [.. this.data];
            }
            else if (this.hiddenData != null)
            {
                byte[] result = new byte[Length];
                foreach (var kvp in this.hiddenData)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
            else if (this.awaitingAccess != null)
            {
                object?[] value = this.awaitingAccess.ToArray();
                byte[] result = new byte[Length];
                for (UInt64 i = 0; i < (UInt64)value.Length; i++)
                {
                    if (value.Length < (int)i)
                    {
                        if (value[i] != null)
                        {
                            result[i] = (byte?)value[i]??0;
                            continue;
                        }
                    }
                }
                return result;
            }
            else
            {
                throw new Exception("ByteArray is not initialized");
            }
        }
        public void Empty()
        {
            if (this.data != null)
            {
                data = new byte[0];
            }
            else if (this.hiddenData != null)
            {
                this.hiddenData.Clear();
            }
            else if (this.awaitingAccess != null)
            {
                this.awaitingAccess.Clear();
            }
            else
            {
                throw new Exception("ByteArray is not initialized");
            }
        }
        public void Fill(byte value)
        {
            byte[] tmp = new byte[this.Length];
            Array.Fill(tmp, value);
            Fill(tmp);
        }
        public void Fill(byte[] values)
        {
            if ((UInt64)values.Length > this.Length)
            {
                throw new Exception("ByteArray is not large enough to fill with given values");
            }

            if (this.data != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    this.data[i] = values[i];
                }
            }
            else if (this.hiddenData != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != 0)
                    {
                        this.hiddenData[(UInt64)i] = values[i];
                    }
                }
            }
            else if (this.awaitingAccess != null)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    this.awaitingAccess.Add(values[i]);
                }
            }
            else
            {
                throw new Exception("ByteArray is not initialized");
            }
        }
    }
    internal class MemBlock
    {
        public UInt64 initialAddress;
        public UInt64 addressCount;
        public short bytesPerAddress;
        public MemBlock? nextBlock, prevBlock;
        public ByteArray data;
        protected bool memAccess = false;
        public Thread? swapper;
        protected readonly Mutex swapCheck = new(false);
        public MemBlock(UInt64 initialAddress, UInt64 addressCount, short bytesPerAddress, MemBlock? prevBlock, MemOptimization memMode)
        {
            this.initialAddress = initialAddress;
            this.addressCount = addressCount;
            this.bytesPerAddress = bytesPerAddress;
            this.nextBlock = null;
            this.prevBlock = prevBlock;
            prevBlock?.nextBlock = this;
            this.data = new ByteArray(addressCount * (UInt64)bytesPerAddress, memMode);
            if (memMode == MemOptimization.Swap)
            {
                this.swapper = new Thread(this.MemSwapLoop!);
                this.swapper.Start(10);
            }
        }
        protected void MemSwapLoop(object maxAge)
        {
            DateTime accessTime = DateTime.Now;
            UInt64 maxAgeMs = (UInt64)maxAge;
            bool isOut = false;
            while (true)
            {
                Thread.Sleep(isOut ? 1 : 5);
                if (this.memAccess && isOut)
                {
                    SwapIn();
                    isOut = false;
                    swapCheck.ReleaseMutex();
                }
                else if (this.memAccess && !isOut)
                {
                    accessTime = DateTime.Now;
                    this.memAccess = false;
                }
                else if (DateTime.Now.Subtract(accessTime).TotalMilliseconds > maxAgeMs && !isOut)
                {
                    swapCheck.WaitOne();
                    if (!this.memAccess)
                    {
                        SwapOut();
                        isOut = true;
                    }
                    else
                    {
                        accessTime = DateTime.Now;
                        this.memAccess = false;
                        swapCheck.ReleaseMutex();
                    }
                }
            }
        }
        protected void SwapOut()
        {
            FileStream f = File.Create($"memblock_{this.initialAddress:X4}_{this.addressCount:X4}.bin");
            f.Write(this.data.ToArray(), 0, (int)this.data.Length);
            this.data.Empty();
            f.Close();
        }
        protected void SwapIn()
        {
            FileStream f = File.OpenRead($"memblock_{this.initialAddress:X4}_{this.addressCount:X4}.bin");
            this.data.Fill(0);
            int length = f.Read(this.data.ToArray(), 0, (int)this.data.Length);
            if ((UInt64)length != this.data.Length)
            {
                throw new Exception("Memory block swap file corrupted");
            }
            f.Close();
        }
        public UInt64 Read(UInt64 address)
        {
            if (address < this.initialAddress || address >= this.initialAddress + this.addressCount)
            {
                throw new Exception("Address out of range");
            }
            this.memAccess = true;
            UInt64 offset = address - this.initialAddress;
            UInt64 value = 0;
            swapCheck.WaitOne();
            for (short i = 0; i < this.bytesPerAddress; i++)
            {
                value |= (UInt64)this.data[offset * (UInt64)this.bytesPerAddress + (UInt64)i] << (int)(i * 8);
            }
            swapCheck.ReleaseMutex();
            return value;
        }
        public void Write(UInt64 address, UInt64 data)
        {
            if (address < this.initialAddress || address >= this.initialAddress + this.addressCount)
            {
                throw new Exception("Address out of range");
            }
            this.memAccess = true;
            UInt64 offset = address - this.initialAddress;
            swapCheck.WaitOne();
            for (short i = 0; i < this.bytesPerAddress; i++)
            {
                this.data[offset * (UInt64)this.bytesPerAddress + (UInt64)i] = (byte)((data & (UInt64)((UInt64)(0xFF) << (int)(i * 8))) >> (int)(i * 8));
            }
            swapCheck.ReleaseMutex();
        }
    }
    internal enum MemOptimization
    {
        None,
        Swap,
        awaitAccessBlock,
        hideZero,
    }
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
        public readonly UInt64 maxAddress;
        private readonly LinkedList<AddressLock> AddressLocks = new();
        public Mem(UInt64 maxAddress, short bytesPerAddress, UInt64 blockSize)
        {
            this.maxAddress = maxAddress;
            this.bytesPerAddress = bytesPerAddress;
            this.memInUse = new Mutex(false);
            if (blockSize == 0)
            {
                MemBlocks = new(0, maxAddress, bytesPerAddress, null, MemOptimization.None);
                return;
            }
            while (true)
            {
                MemBlock newBlock = new(MemBlocks == null ? 0 : MemBlocks.initialAddress + MemBlocks.addressCount, blockSize, bytesPerAddress, MemBlocks, MemOptimization.None);
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
