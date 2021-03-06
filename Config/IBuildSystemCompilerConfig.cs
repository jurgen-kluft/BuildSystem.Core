using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using Core;

namespace DataBuildSystem
{
    public interface IBuildSystemCompilerConfig
    {
        #region Methods & Properties

        /// <summary>
        /// The resource data file name
        /// </summary>
        string DataFilename(string name);

        /// <summary>
        /// The resource data file extension
        /// </summary>
        string DataFileExtension { get; }

        /// <summary>
        /// The resource data relocation information file extension
        /// </summary>
        string DataRelocFileExtension { get; }

        /// <summary>
        /// Write the BigfileToc and Resource data in which endian
        /// </summary>
        bool LittleEndian { get; }

        /// <summary>
        /// Treat every enum as a 32 bit integer
        /// </summary>
        bool EnumIsInt32 { get; }

        /// <summary>
        /// Treat every bool as a n byte value (1, 2 or 4)
        /// </summary>
        int SizeOfBool { get; }

        /// <summary>
        /// Force building the bigfile, even if no assets have been modified
        /// </summary>
        bool ForceBuildingBigfile { get; }
        
        #endregion
    }

    public class BuildSystemCompilerDefaultConfig : IBuildSystemCompilerConfig
    {
        #region Methods & Properties

        public string DataFilename(string name) { return "Game" + "." + name; }
        public string DataFileExtension { get { return ".gdf"; } }
        public string DataRelocFileExtension { get { return ".gdr"; } }
        public bool LittleEndian { get { return true; } }
        public bool EnumIsInt32 { get { return false; } }
        public int SizeOfBool { get { return 4; } }
        public bool ForceBuildingBigfile { get { return false; } }

        #endregion
    }

    public static class BuildSystemCompilerConfig
    {
        #region Fields

        private static EPlatform sPlatform = EPlatform.PC;
        private static EPlatform sTarget = EPlatform.PC;
        private static ETerritory sTerritory = ETerritory.USA;

        private static string sName;
        private static Filename sConfigFilename;
        private static Dirname sSrcPath;
        private static Dirname sSubPath;
        private static Dirname sDstPath;
        private static Dirname sDepPath;
        private static Dirname sToolPath;
        private static Dirname sPublishPath;

        private static List<Filename> sReferencedAssemblies = new List<Filename>();

        private static bool sBuildBigfile = false;

        private static IBuildSystemCompilerConfig sConfig = new BuildSystemCompilerDefaultConfig();

        #endregion
        #region Properties

        public static bool platformPC { get { return sPlatform == EPlatform.PC; } }
        public static bool platformXboxOne { get { return sPlatform == EPlatform.XBOX_ONE; } }
        public static bool platformXboxOneX { get { return sPlatform == EPlatform.XBOX_ONE_X; } }
        public static bool platformPS4 { get { return sPlatform == EPlatform.PS4; } }
        public static bool platformPS4Pro { get { return sPlatform == EPlatform.PS4_PRO; } }

        public static bool targetPC { get { return sTarget == EPlatform.PC; } }
		public static bool targetXboxOne { get { return sTarget == EPlatform.XBOX_ONE; } }
		public static bool targetXboxOneX { get { return sTarget == EPlatform.XBOX_ONE_X; } }
		public static bool targetPS4 { get { return sTarget == EPlatform.PS4; } }
		public static bool targetPS4Pro { get { return sTarget == EPlatform.PS4_PRO; } }

		public static EEndian Endian { get { return sConfig.LittleEndian ? EEndian.LITTLE : EEndian.BIG; } }
        public static string Name { get { return sName; } }
        public static EPlatform Platform { get { return sPlatform; } }
        public static string PlatformName { get { return sPlatform.ToString(); } }
        public static EPlatform Target { get { return sTarget; } }
        public static string TargetName { get { return sTarget.ToString(); } }
        public static ETerritory Territory { get { return sTerritory; } }
        public static string TerritoryName { get { return sTerritory.ToString(); } }
        public static bool EnumIsInt32 { get { return sConfig.EnumIsInt32; } }
        public static int SizeOfBool { get { return sConfig.SizeOfBool; } }
        public static bool BuildBigfile { get { return sBuildBigfile || sConfig.ForceBuildingBigfile; } }
        public static Dirname SrcPath { get { return sSrcPath; } }
        public static Dirname SubPath { get { return sSubPath; } }
        public static Dirname DstPath { get { return sDstPath; } }
        public static Dirname DepPath { get { return sDepPath; } }
        public static Dirname ToolPath { get { return sToolPath; } }
        public static Dirname PubPath { get { return sPublishPath; } }
        public static string DataFileExtension { get { return sConfig.DataFileExtension; } }
        public static string DataRelocFileExtension { get { return sConfig.DataRelocFileExtension; } }
        public static Filename ConfigFilename { get { return sConfigFilename; } }
        public static Filename[] ReferencedAssemblies { get { return sReferencedAssemblies.ToArray(); } }

        #endregion
        #region Methods

        public static bool SourceCodeFilterDelegate(DirectoryScanner.FilterEvent e) 
        { 
            return (e.isFolder) ? BuildSystemCompilerConfig.FolderFilter(e.name) : BuildSystemCompilerConfig.FileFilter(e.name); 
        }

        public static bool FolderFilter(string folder)
        {
            if (folder.StartsWith("bin.", true, CultureInfo.InvariantCulture))
                return true;
            if (folder.StartsWith("publish.", true, CultureInfo.InvariantCulture))
                return true;
            if (folder.EndsWith(".svn", true, CultureInfo.InvariantCulture))
                return true;
            if (String.Compare(folder, PlatformName, true, CultureInfo.InvariantCulture) == 0)
                return true;

            return false;
        }

        public static bool FileFilter(string file)
        {
            string filename = Path.GetFileNameWithoutExtension(file);
            string platformStr = Path.GetExtension(filename).TrimStart('.');

            // Name.%PLATFORM%.cs
            EPlatform platform = fromString(platformStr, Platform);
            if (platform != Platform)
                return true;

            return false;
        }

        public static bool init(string name, string config, bool bigfile, string platform, string target, string territory, string srcPath, string subPath, string dstPath, string depPath, string toolPath, string publishPath)
        {
            if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(platform) || String.IsNullOrEmpty(territory) || String.IsNullOrEmpty(srcPath) || String.IsNullOrEmpty(dstPath) || String.IsNullOrEmpty(depPath) || String.IsNullOrEmpty(toolPath) || String.IsNullOrEmpty(publishPath))
                return false;

            if (String.IsNullOrEmpty(target))
                target = platform;

			Core.Environment.addVariable("NAME", name);
			Core.Environment.addVariable("PLATFORM", platform);
			Core.Environment.addVariable("TARGET", target);
			Core.Environment.addVariable("TERRITORY", territory);
			Core.Environment.addVariable("SRCPATH", Core.Environment.expandVariables(srcPath));

            sPlatform = fromString(platform, EPlatform.PC);
            sTarget = fromString(target, EPlatform.PC);
            sTerritory = fromString(territory, ETerritory.USA);

            sBuildBigfile = bigfile;

            sName = name;
			sConfigFilename = new Filename(Core.Environment.expandVariables(config));
			sSrcPath = new Dirname(Core.Environment.expandVariables(srcPath));
			sSubPath = new Dirname(Core.Environment.expandVariables(string.IsNullOrEmpty(subPath) ? string.Empty : subPath));
			sDstPath = new Dirname(Core.Environment.expandVariables(dstPath));
			sDepPath = new Dirname(Core.Environment.expandVariables(depPath));
			sToolPath = new Dirname(Core.Environment.expandVariables(toolPath));
			sPublishPath = new Dirname(Core.Environment.expandVariables(publishPath));

            return true;
        }

        public static void AddReferencedAssembly(Filename referencedAssembly)
        {
            if (!sReferencedAssemblies.Contains(referencedAssembly))
                sReferencedAssemblies.Add(referencedAssembly);
        }

        public static void AddReferencedAssemblies(List<Filename> referencedAssemblies)
        {
            foreach (Filename a in referencedAssemblies)
                AddReferencedAssembly(a);
        }

        public static void Init(IBuildSystemCompilerConfig config)
        {
            if (config != null)
                sConfig = config;
            else
                sConfig = new BuildSystemCompilerDefaultConfig();
        }

        public static T fromString<T>(string _string, T _default)
        {
            string[] names = Enum.GetNames(typeof(T));
            string name = string.Empty;
            foreach (string p in names)
            {
                if (String.Compare(p, _string, true) == 0)
                {
                    name = p;
                    break;
                }
            }
            if (String.IsNullOrEmpty(name))
                return _default;

            return (T)Enum.Parse(typeof(T), name);
        }

        public static string DataFilename(string name)
        {
            return sConfig.DataFilename(name);
        }

        #endregion
    }
}
