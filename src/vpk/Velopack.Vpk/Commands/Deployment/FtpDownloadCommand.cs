namespace Velopack.Vpk.Commands.Deployment;

public class FtpDownloadCommand : FtpBaseCommand
{
    public FtpDownloadCommand()
        : base("ftp", "Download latest release from an FTP source.")
    {
    }
}