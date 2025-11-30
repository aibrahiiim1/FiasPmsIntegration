namespace FiasPmsIntegration.Models
{
    public class FiasServerOptions
    {
        public int Port { get; set; } = 5008;
        public string IpAddress { get; set; } = "0.0.0.0";
        public string InterfaceType { get; set; } = "WW";
        public string Version { get; set; } = "1.0";
        public string CharacterSet { get; set; } = "UTF-8";
        public int DecimalPoint { get; set; } = 2;
        public string GuestNameMacro { get; set; } = "$LN";
    }
}
