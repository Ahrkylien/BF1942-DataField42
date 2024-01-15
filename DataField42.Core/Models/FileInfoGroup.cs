using System.Collections.Generic;

public class FileInfoGroup
{
    public List<FileInfo> FileInfos { get; private set; } = new List<FileInfo>();

    public Bf1942FileTypes FileType => FileInfos.Count > 0 ? FileInfos[0].FileType : Bf1942FileTypes.None;

    public string Mod => FileInfos.Count > 0 ? FileInfos[0].Mod : "";

    public bool RepresentsAbsenceOfFile => FileInfos.Count > 0 ? FileInfos[0].RepresentsAbsenceOfFile : false;

    public string FileNameWithoutPatchNumber => FileInfos.Count > 0 ? FileInfos[0].FileNameWithoutPatchNumber : "" ;

    public string Directory => FileInfos.Count > 0 ? FileInfos[0].Directory : "" ;

    public FileInfoGroup(FileInfo fileInfo)
    {
        FileInfos.Add(fileInfo);
    }

    public static List<FileInfoGroup> GetFileInfoGroups(List<FileInfo> fileInfos)
    {
        List<FileInfoGroup> fileInfoGroups = new();
        foreach (FileInfo fileInfo in fileInfos)
        {
            var addedToList = false;
            if (fileInfo.FileType == Bf1942FileTypes.Level || fileInfo.FileType == Bf1942FileTypes.Archive)
            {
                foreach (FileInfoGroup fileInfoGroup in fileInfoGroups)
                {
                    if (fileInfoGroup.FileType == fileInfo.FileType && fileInfoGroup.FileNameWithoutPatchNumber.ToLower() == fileInfo.FileNameWithoutPatchNumber.ToLower() && fileInfoGroup.Mod.ToLower() == fileInfo.Mod.ToLower())
                    {
                        fileInfoGroup.FileInfos.Add(fileInfo);
                        addedToList = true;
                        break;
                    }
                }
            }
            if (!addedToList)
                fileInfoGroups.Add(new FileInfoGroup(fileInfo));
        }
        return fileInfoGroups;
    }
}
