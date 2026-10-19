using System;

namespace Wireframe
{
    [Serializable]
    public class DepotFileMapping : VDFFile
    {
        public override string FileName => "FileMapping";

        // This can be a full path, or a path relative to ContentRoot
        public string LocalPath;

        // This is a path relative to the install folder of your game
        public string DepotPath;

        // If LocalPath contains wildcards, setting this means that all
        // matching files within subdirectories of LocalPath will also
        // be included.
        public bool recursive;

        public DepotFileMapping()
        {
            
        }
        
        public DepotFileMapping(DepotFileMapping fileMapping)
        {
            LocalPath = fileMapping.LocalPath;
            DepotPath = fileMapping.DepotPath;
            recursive = fileMapping.recursive;
        }
    }

    [Serializable]
    public class DepotVDFFile : VDFFile
    {
        public override string FileName => "DepotBuildConfig";

        // Set your assigned depot ID here. 0 means not set yet.
        public int DepotID;

        // include all files recursively
        public DepotFileMapping FileMapping = new DepotFileMapping();

        // but exclude all symbol files  
        // This can be a full path, or a path relative to ContentRoot
        public string FileExclusion;

        public DepotVDFFile()
        {
            
        }
        
        public DepotVDFFile(DepotVDFFile depot)
        {
            DepotID = depot.DepotID;
            FileMapping = new DepotFileMapping(depot.FileMapping);
            FileExclusion = depot.FileExclusion;
        }
    }
}