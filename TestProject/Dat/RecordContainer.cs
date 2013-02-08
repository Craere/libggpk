using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TestProject.Dat
{
	class DatContainer<T> where T : BaseDat
	{
		public Dictionary<int, BaseData> DataEntries = new Dictionary<int, BaseData>();
		public List<T> Entries;
		public int DataTableBegin;

		public DatContainer(string fileName)
		{
			byte[] fileBytes = File.ReadAllBytes(fileName);

			using (MemoryStream ms = new MemoryStream(fileBytes))
			{
				using (BinaryReader br = new BinaryReader(ms))
				{
					Read(br);
				}
			}
		}

		private void AddDataToTable(T entry, BinaryReader inStream)
		{
			var properties = typeof(T).GetFields();
			foreach (var prop in properties)
			{
				object[] customAttributes = prop.GetCustomAttributes(false);

				if (customAttributes.Length == 0)
					continue;

				int offset = (int)prop.GetValue(entry);
				if (DataEntries.ContainsKey(offset))
				{
					continue;
				}

				if (customAttributes[0] is StringIndex)
				{
					DataEntries[offset] = new UnicodeString(inStream, offset + DataTableBegin);
				}
				else if (customAttributes[0] is DataIndex)
				{
					DataEntries[offset] = new UnkownData(inStream, offset + DataTableBegin);
				}

					Console.WriteLine("{0} -> {1}", offset, DataEntries[offset]);
			}
		}

		private void UpdateDataOffsets(T entry, Dictionary<int, int> updatedOffsets)
		{
			var properties = typeof(T).GetFields();
			foreach (var prop in properties)
			{
				object[] customAttributes = prop.GetCustomAttributes(false);

				if (customAttributes.Length == 0)
					continue;

				int offset = (int)prop.GetValue(entry);
				prop.SetValue(entry, updatedOffsets[offset]);
			}
		}

		private void Read(BinaryReader inStream)
		{
			int numberOfEntries = inStream.ReadInt32();
			Entries = new List<T>(numberOfEntries);

			for (int i = 0; i < numberOfEntries; i++)
			{
				// TODO: Skip reflection if it's running slow (compiled lambda?)
				T newEntry = (T)Activator.CreateInstance(typeof(T), new object[] { inStream });
				Entries.Add(newEntry);
			}

			if (inStream.ReadUInt64() != 0xBBbbBBbbBBbbBBbb)
			{
				throw new ApplicationException("Missing magic number after records");
			}

			DataTableBegin = (int)(inStream.BaseStream.Position - 8);

			// Read all referenced string and data entries from the data following the entries (starting at magic number)
			foreach (var item in Entries)
			{
				AddDataToTable(item, inStream);
			}
		}

		public void Save(string fileName)
		{
			using (BinaryWriter outStream = new BinaryWriter(File.Open(fileName, FileMode.Create)))
			{
				Save(outStream);
			}
		}

		public void Save(BinaryWriter outStream)
		{
			// Mapping of the new string and data offsets
			Dictionary<int, int> changedOffsets = new Dictionary<int, int>();

			outStream.Write(Entries.Count);

			if (Entries.Count > 0)
			{
				// Pretty ugly way to zero out the for sizeof(Entry) * EntryCount bytes
				outStream.Write(new byte[(Entries[0] as BaseDat).GetSize() * Entries.Count]);
			}


			int newStartOfDataSection = (int)outStream.BaseStream.Position;
			outStream.Write(0xBBbbBBbbBBbbBBbb);

			var sortedDataEntries = DataEntries.ToList();
			sortedDataEntries.Sort((left, right) => left.Key.CompareTo(right.Key));

			// Write each referenced piece of data to data section
			foreach (var item in sortedDataEntries)
			{
				changedOffsets[item.Key] = (int)(outStream.BaseStream.Position - newStartOfDataSection);
				item.Value.Save(outStream);
			}

			outStream.BaseStream.Seek(4, SeekOrigin.Begin);

			// Now we must go through each StringIndex and DataIndex and update the index then save it
			foreach (var item in Entries)
			{
				UpdateDataOffsets(item, changedOffsets);
				item.Save(outStream);
			}
		}
	}
}