
namespace Swiddler.ViewModels
{
    public class IPAdapterEmptyHeader
    {
        public static IPAdapterEmptyHeader Default { get; } = new IPAdapterEmptyHeader();
        private IPAdapterEmptyHeader() { }
    }

    public class IPAdapterHeader
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsUp { get; set; }

        public override bool Equals(object obj) => Id.Equals((obj as IPAdapterHeader)?.Id);

        public override int GetHashCode() => Id.GetHashCode();

        public IPAdapterHeader(IPAddressItem item)
        {
            Id = item.InterfaceId;
            Name = item.InterfaceName;
            Description = item.InterfaceDescription;
            IsUp = item.IsUp;
        }
    }
}
