namespace Velopack.Vpk.Commands.Deployment;

public class FtpBaseCommand : OutputCommand
{
    public string Url { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public bool PassiveMode { get; private set; }
    public double Timeout { get; private set; }

    protected FtpBaseCommand(string name, string description)
        : base(name, description)
    {
        AddOption<Uri>((v) => Url = v.ToAbsoluteOrNull(), "--url")
            .SetDescription("FTP URL to connect to.")
            .SetArgumentHelpName("URL")
            .MustBeValidFtpUri()
            .SetRequired();

        AddOption<string>((v) => Username = v, "--username")
            .SetDescription("FTP username for authentication.")
            .SetArgumentHelpName("USERNAME");

        AddOption<string>((v) => Password = v, "--password")
            .SetDescription("FTP password for authentication.")
            .SetArgumentHelpName("PASSWORD");

        AddOption<bool>((v) => PassiveMode = v, "--passive")
            .SetDescription("Use passive mode for FTP connection.")
            .SetDefault(true);

        AddOption<double>((v) => Timeout = v, "--timeout")
            .SetDescription("Network timeout in minutes.")
            .SetArgumentHelpName("MINUTES")
            .SetDefault(30);
    }
}