namespace Velopack.Vpk.Commands.Deployment;

public class FtpUploadCommand : FtpBaseCommand
{
    public int KeepMaxReleases { get; private set; }

    public FtpUploadCommand()
        : base("ftp", "Upload releases to an FTP server.")
    {
        AddOption<int>((x) => KeepMaxReleases = x, "--keepMaxReleases")
            .SetDescription("The maximum number of releases to keep on the FTP server, anything older will be deleted.")
            .SetArgumentHelpName("COUNT");
        
        ReleaseDirectoryOption.SetRequired();
        ReleaseDirectoryOption.MustNotBeEmpty();
    }
}