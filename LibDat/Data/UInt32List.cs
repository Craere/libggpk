﻿using System;
using System.IO;

namespace LibDat.Data
{
    /// <summary>
    /// Represents a list of UInt32 found in the resource section of a .dat file
    /// </summary>
    public class UInt32List : AbstractDataList<UInt32>
    {
        public UInt32List(int offset, int dataTableOffset, int listLength, BinaryReader inStream)
            : base(offset, dataTableOffset, listLength, inStream) { }

        protected override void ReadData(BinaryReader inStream)
        {
            while (inStream.BaseStream.Position < inStream.BaseStream.Length)
            {
                var u = inStream.ReadUInt32();
                Data.Add(u);
                break;
            }
        }
    }
}