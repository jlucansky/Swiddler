namespace Swiddler.Security
{
    /// <summary>
    /// Wraps up the server SSL hello information.
    /// </summary>
    public class SslServerHello : SslHelloBase
    {
        public int CipherSuite { get; set; }
        public string CipherSuiteName => SslCiphers.GetName(CipherSuite);

        public byte CompressionMethod { get; set; }
    }
}
