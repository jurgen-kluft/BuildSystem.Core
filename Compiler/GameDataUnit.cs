using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using GameCore;
using GameData;

namespace DataBuildSystem
{
	using Int8 = SByte;
	using UInt8 = Byte;

	public class GameDataUnits
	{
		private List<GameDataUnit> DataUnits = new List<GameDataUnit>();

		public GameDataUnits() { }

		private AssemblyLoadContext mGameDataAssemblyContext;
		private Assembly mGameDataAssembly;

		private Assembly LoadAssembly(string gamedata_dll_filename)
		{
			mGameDataAssemblyContext = new AssemblyLoadContext("GameData", true);
			byte[] dllBytes = File.ReadAllBytes(BuildSystemCompilerConfig.GddPath + "/" + gamedata_dll_filename);
			mGameDataAssembly = mGameDataAssemblyContext.LoadFromStream(new MemoryStream(dllBytes));

			return mGameDataAssembly;
		}
		private void UnloadAssembly()
		{
			mGameDataAssemblyContext.Unload();
			mGameDataAssembly = null;
		}

		public State Update(string srcpath, string dstpath)
		{
			// Foreach DataUnit that is out-of-date or missing
			foreach (GameDataUnit gdu in DataUnits)
			{
				gdu.Verify();
				State gduGameDataDll = gdu.StateOf(EGameData.GameDataDll);
				State gduCompilerLog = gdu.StateOf(EGameData.GameDataCompilerLog);
				State gduGameDataData = gdu.StateOf(EGameData.GameDataData, EGameData.GameDataRelocation);
				State gduBigfile = gdu.StateOf(EGameData.BigFileData, EGameData.BigFileToc, EGameData.BigFileFilenames, EGameData.BigFileHashes);

				if (gduGameDataDll.IsOk && gduCompilerLog.IsOk && gduGameDataData.IsOk && gduBigfile.IsOk)
				{
					// All is up-to-date, but source files might have changed!
					GameDataCompilerLog gdcl = new(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataCompilerLog));

					// Load the compiler log so that they can be used to verify the source files
					List<IDataCompiler> loaded_compilers = new();
					gdcl.Load(loaded_compilers);
					
					// Execute all compilers, every compiler will thus check it's dependencies (source and destination files)
					Result result = gdcl.Execute(loaded_compilers, out List<string> dst_relative_filepaths);
					if (result != Result.Ok)
					{
						// Some (or all) compilers reported a change, now we have to load the assembly and build the Bigfile and Game Data.
						Assembly gdasm = LoadAssembly(gdu.FilePath);
						GameDataData gdd = new(gdasm);

						// Compiler log is updated -> rebuild the Bigfile
						// As long as all the FileId's will be the same we do not need to build/save the game data files
						GameDataBigfile bff = new();
						bff.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.BigFileData), dst_relative_filepaths);
						gdd.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataData));

						// Lastly we need to save the game data compiler log
						gdcl.Save(loaded_compilers);

						UnloadAssembly();
					}
				}
				else if (gduGameDataDll.IsOk && gduCompilerLog.IsNotOk && gduGameDataData.IsOk && gduBigfile.IsOk)
				{
					Assembly gdasm = LoadAssembly(gdu.FilePath);
					GameDataData gdd = new(gdasm);

					List<IDataCompiler> current_compilers = gdd.CollectDataCompilers();
					List<IDataCompiler> loaded_compilers = new(current_compilers.Count);

					GameDataCompilerLog gdcl = new(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataCompilerLog));
					gdcl.Load(loaded_compilers);

					gdcl.Merge(loaded_compilers, current_compilers, out List<IDataCompiler> merged_compilers);

					Result result = gdcl.Execute(merged_compilers, out List<string> dst_relative_filepaths);
					if (result == Result.Ok)
					{
						if (gduGameDataData.IsNotOk || gduBigfile.IsNotOk)
						{
							result = Result.OutOfData;
						}
					}

					if (result == Result.OutOfData)
					{
						// Compiler log is updated -> rebuild the Bigfiles and GameData files
						GameDataBigfile bff = new();
						bff.Save(Path.ChangeExtension(gdu.FilePath, BigfileConfig.BigFileExtension), dst_relative_filepaths);
						gdd.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataData));

						// Everything is saved, now save the compiler log
						gdcl.Save(merged_compilers);
					}

					UnloadAssembly();
				}
				else if (gduGameDataDll.IsNotOk && gduCompilerLog.IsOk && gduGameDataData.IsOk && gduBigfile.IsOk)
				{
					Assembly gdasm = LoadAssembly(gdu.FilePath);
					GameDataData gdd = new(gdasm);

					List<IDataCompiler> current_compilers = gdd.CollectDataCompilers();

					GameDataCompilerLog gdcl = new(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataCompilerLog));
					if (gduGameDataData.IsOk && gduBigfile.IsOk)
					{
						// See if compiler log is up-to-date, including the source files
						List<IDataCompiler> loaded_compilers = new(current_compilers.Count);
						gdcl.Load(loaded_compilers);
						Result result = gdcl.Merge(loaded_compilers, current_compilers, out List<IDataCompiler> merged_compilers);

						result = gdcl.Execute(merged_compilers, out List<string> dst_relative_filepaths);
						if (result == Result.Ok)
						{
							UnloadAssembly();
							continue;
						}

						// Compiler log is updated -> rebuild the Bigfiles
						GameDataBigfile bff = new();
						bff.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.BigFileData), dst_relative_filepaths);

						gdcl.Save(merged_compilers);
					}
					UnloadAssembly();
				}
				else if (gduGameDataDll.IsOk && gduCompilerLog.IsOk && gduGameDataData.IsOk && gduBigfile.IsNotOk)
				{
					Assembly gdasm = LoadAssembly(gdu.FilePath);
					GameDataData gdd = new(gdasm);

					List<IDataCompiler> current_compilers = gdd.CollectDataCompilers();
					List<IDataCompiler> loaded_compilers = new(current_compilers.Count);

					GameDataCompilerLog gdcl = new(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataCompilerLog));
					gdcl.Load(loaded_compilers);
					gdcl.Execute(loaded_compilers, out List<string> dst_relative_filepaths);

					GameDataBigfile bff = new();
					bff.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.BigFileData), dst_relative_filepaths);
					gdd.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataData));
					gdcl.Save(loaded_compilers);

					UnloadAssembly();
				}
				else if (gduGameDataDll.IsOk && gduCompilerLog.IsOk && gduGameDataData.IsNotOk && gduBigfile.IsOk)
				{
					// Game Data (.gdd) file or it's relocation data has changed or is deleted

					List<IDataCompiler> loaded_compilers = new();

					// We need the list of destination files (for determining the FileId's)
					// So we need to load the Game Data Compiler Log and verify that source and destination files
					// have not changed.
					GameDataCompilerLog gdcl = new(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataCompilerLog));
					gdcl.Load(loaded_compilers);
					Result result = gdcl.Execute(loaded_compilers, out List<string> dst_relative_filepaths);
					if (result == Result.Ok)
					{
						Assembly gdasm = LoadAssembly(gdu.FilePath);
						GameDataData gdd = new(gdasm);

						// All source and destination files are up-to-date
						gdd.PrepareFilesProviders(loaded_compilers);
						gdd.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataData));

						UnloadAssembly();
					}
					else
					{
						// Hmmmm, the GameDataCompilerLog indicated that some (or all) compilers detected changes.
						Assembly gdasm = LoadAssembly(gdu.FilePath);
						GameDataData gdd = new(gdasm);

						// All source and destination files are up-to-date
						gdd.PrepareFilesProviders(loaded_compilers);
						gdd.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.GameDataData));

						GameDataBigfile bff = new();
						bff.Save(GameDataPath.GetFilePathFor(gdu.Name, EGameData.BigFileData), dst_relative_filepaths);

						gdcl.Save(loaded_compilers);

						UnloadAssembly();


					}



				}

			}

			return State.Ok;
		}

		public void Load(string dstpath, string gddpath)
		{
			// Scan the gddpath folder for all game data .dll's
			Dictionary<Hash160, string> hashToPath = new();
			foreach (var path in GameCore.DirUtils.EnumerateFiles(gddpath, "*.dll", SearchOption.TopDirectoryOnly))
			{
				string filepath = path.RelativePath(gddpath.Length + 1).Span.ToString();
				if (Path.GetFileNameWithoutExtension(filepath).StartsWith("GameData"))
				{
					Hash160 hash = HashUtility.Compute_UTF8(filepath);
					hashToPath.Add(hash, filepath);
				}
			}

			List<int> indices = new();
			for (int i = 0; i < (2 * hashToPath.Count); i++)
				indices.Add(i);

			BinaryFileReader binaryfile = new BinaryFileReader();
			if (binaryfile.Open(Path.Join(dstpath, "GameDataUnits.log")))
			{
				UInt32 magic = binaryfile.ReadUInt32();
				if (magic == StringTools.Encode_64_10('D', 'A', 'T', 'A', '.', 'U', 'N', 'I', 'T', 'S'))
				{
					Int32 numUnits = binaryfile.ReadInt32();
					DataUnits = new List<GameDataUnit>(numUnits);

					for (int i = 0; i < numUnits; i++)
					{
						GameDataUnit gdu = GameDataUnit.Load(binaryfile);

						// Is this one still in the list of .dll's?
						if (hashToPath.ContainsKey(gdu.Hash))
						{
							indices.Remove(gdu.Index);
							hashToPath.Remove(gdu.Hash);
							DataUnits.Add(gdu);
						}
					}
				}
				binaryfile.Close();
			}

			// Any new DataUnit's -> create them!
			int j = 0;
			foreach (var item in hashToPath)
			{
				GameDataUnit gdu = new(item.Value, indices[j++]);
				DataUnits.Add(gdu);
			}
		}

		public void Save(string dstpath)
		{
			BinaryFileWriter binaryfile = new BinaryFileWriter();
			binaryfile.Open(Path.Join(dstpath, "GameDataUnits.log"));

			binaryfile.Write(StringTools.Encode_64_10('D', 'A', 'T', 'A', '.', 'U', 'N', 'I', 'T', 'S'));
			binaryfile.Write((Int32)DataUnits.Count);
			foreach (GameDataUnit gdu in DataUnits)
			{
				gdu.Save(binaryfile);
			}

			binaryfile.Close();
		}
	}

	/// e.g.
	/// FilePath: GameData.Fonts.dll
	/// Index: 1
	/// Units: [Modified,Ok,Ok,Ok,Ok,Ok,Ok,Ok]


	public class GameDataUnit
	{
		public string FilePath { get; private set; }
		public string Name { get; private set; }
		public Hash160 Hash { get; set; }
		public Int32 Index { get; set; }
		private State[] States { get; set; } = new State[(int)EGameData.Count];
		private Dependency Dep { get; set; }

		public State StateOf(EGameData u)
		{
			return States[(int)u];
		}

		public State StateOf(params EGameData[] pu)
		{
			State s = new State();
			foreach (var u in pu)
				s.Merge(States[(int)u]);
			return s;
		}

		private GameDataUnit() : this(string.Empty, -1) { }

		public GameDataUnit(string filepath, Int32 index)
		{
			FilePath = filepath;
			Name = Path.GetFileNameWithoutExtension(filepath);
			Hash = HashUtility.Compute_UTF8(FilePath);
			Index = index;

			for (int i = 0; i < States.Length; ++i)
			{
				States[i] = State.Missing;
			}
		}

		public void Verify()
		{
			Dep = Dependency.Load(EGameDataPath.Dst, FilePath);
			if (Dep != null)
			{
				Dep.Update(delegate (int idx, State state)
				{
					States[idx] = state;
				});
			}
			else
			{
				for (int i = 0; i < States.Length; ++i)
				{
					States[i] = State.Missing;
				}

				Dep = new();
				foreach (var e in (EGameData[])Enum.GetValues(typeof(EGameData)))
				{
					Dep.Add((int)e, GameDataPath.GetPathFor(e), Path.ChangeExtension(FilePath, GameDataPath.GetExtFor(e)));
				}
			}
		}

		public State VerifySource()
		{
			// Check if source files have changed, a change in any source file will have to be handled
			// by executing the DataCompiler to build up-to-date destination files.

			// So we need to use ActorFlow to stream the CompilerLog and each DataCompiler needs to check if
			// its source file(s) are up-to-date.

			return State.Ok;
		}

		public void Save(IBinaryWriter writer)
		{
			writer.Write(FilePath);
			Hash.WriteTo(writer);
			writer.Write(Index);
			for (int i = 0; i < States.Length; ++i)
				writer.Write(States[i].AsInt);
			Dep.WriteTo(writer);
		}

		public static GameDataUnit Load(IBinaryReader reader)
		{
			GameDataUnit gdu = new();
			gdu.FilePath = reader.ReadString();
			gdu.Hash = Hash160.ReadFrom(reader);
			gdu.Index = reader.ReadInt32();
			for (int i = 0; i < gdu.States.Length; ++i)
				gdu.States[i] = new State(reader.ReadInt32());
			gdu.Dep = Dependency.ReadFrom(reader);
			return gdu;
		}
	}

	// TODO  Write up full design with all possible cases of data modification

	// A DataCompiler:
	//
	//     - Reads one or more source (input) files from srcpath
	//     - Processes those (can use external processes, e.g. 'dxc.exe')
	//     - Writes resulting (output) files to destination (dstpath)
	//     - Keeps track of the dependency information by itself

	//
	// Q: How to name and where to place the destination files?
	// A: Filename=HashOf(source filename), Extension can be used to distinguish many files (0000 to 9999?)
	//    So for one Data Compiler we only actually need to store the 'extensions'(number?) of the destination
	//    files in the stream, the path and filename is the same for each destination file.
	//
	// Q: Should we write the dependency information also in 'HashOf(source filename).dep'?
	// A: No, if we do, do we really need the 'Game Data Compiler Log' ?
	//    Yes, well the 'Game Data Compiler Log' is a very convenient way to do multi-core stream processing.
	//    We do not need to search for .dep files etc... and we do not need to keep opening, reading and
	//    closing those small files.

	// Need a database for DataUnit's that can map from Hash -> Index
	// DataUnits should be saved to and loaded from a BinaryFile 'GameDataUnits.slog'

	// A 'Game Data Unit' consists of (.GDU):
	//     - Name
	//     - Hash               (HashOf(filename of 'Game Data DLL'))
	//     - Index
	//     - State of 'Game Data DLL' (.DLL)
	//     - State of 'Game Data Compiler Log' (.GDCL)
	//     - State of 'Game Data File' and 'Game Data Relocation File' (.GDF, .GDR)
	//     - State of 'Game Data Bigfile/TOC/Filename/Hashes' (.BFN, .BFH, .BFT, .BFD)

	// gddpath    = path with all the gamedata DLL's
	// srcpath    = path containing all the 'intermediate' assets (TGA, PGN, TRI, processed FBX files)
	// dstpath    = path containing all the 'cooked' assets and databases
	// pubpath    = path where all the 'Game Data' files and Bigfiles will be written (they are also written in the dstpath)

	// Collect all Game Data DLL's that need to be processed

	// There is a dependency on 'DataUnit.Index' for the generation of FileId's.
	// If this database is deleted then ALL Game Data and Bigfiles have to be regenerated.
	// The pollution of this database with stale items is ok, it does not impact memory usage.
	// It mainly results in empty bff sections, each of them being an offset of 4 bytes.


	// Note: We could mitigate risks by adding full dependency information as a file header of each target file, or still
	//       have each DataCompiler write a .dep file to the destination.
}
