/* Copyright (C) Atomix OS Development, Inc - All Rights Reserved
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* Written by SANDEEP ILIGER <sandeep.iliger@gmail.com>, 08-2014
*/

using Kernel_alpha.Lib;
using Kernel_alpha.FileSystem.FAT;

namespace Kernel_alpha.FileSystem.Find
{
	/// <summary>
	///
	/// </summary>
	public class ByCluster : ACompare
	{
		protected uint cluster;

		public ByCluster(uint cluster)
		{
			this.cluster = cluster;
		}

		public override bool Compare(byte[] data, uint offset, FatType type)
		{
			BinaryFormat entry = new BinaryFormat(data);

            byte first = entry.GetByte(offset + Entry.DOSName);

            if (first == FileNameAttribute.LastEntry)
				return false;

            if ((first == FileNameAttribute.Deleted) | (first == FileNameAttribute.Dot))
				return false;

            if (first == FileNameAttribute.Escape)
				return false;

			uint startcluster = FatFileSystem.GetClusterEntry(data, offset, type);

			if (startcluster == cluster)
				return true;

			return false;
		}
	}
}