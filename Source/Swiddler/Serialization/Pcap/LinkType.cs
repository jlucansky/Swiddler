using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Swiddler.Serialization.Pcap
{
	public enum LinkTypes:ushort
	{
		/// <summary>
		/// No link layer information. A packet saved with this link layer contains a raw L3 packet preceded by a 32-bit host-byte-order AF_ value indicating the specific L3 type.
		/// </summary>
		Null = 0,

		/// <summary>
		/// D/I/X and 802.3 Ethernet
		/// </summary>
		Ethernet = 1,

		/// <summary>
		/// Experimental Ethernet (3Mb)
		/// </summary>
		ExpEthernet = 2,

		/// <summary>
		/// Amateur Radio AX.25
		/// </summary>
		AX25 = 3,

		/// <summary>
		/// Proteon ProNET Token Ring
		/// </summary>
		ProNET = 4,

		/// <summary>
		/// Chaos
		/// </summary>
		Chaos = 5,

		/// <summary>
		/// IEEE 802 Networks
		/// </summary>
		TokenRing = 6,

		/// <summary>
		/// ARCNET, with BSD-style header
		/// </summary>
		ARCNET = 7,

		/// <summary>
		/// Serial Line IP
		/// </summary>
		SLIP = 8,

		/// <summary>
		/// Point-to-point Protocol
		/// </summary>
		PPP = 9,

		/// <summary>
		/// FDDI
		/// </summary>
		FDDI = 10,

		/// <summary>
		/// PPP in HDLC-like framing
		/// </summary>
		PppHdlc = 50,

		/// <summary>
		/// NetBSD PPP-over-Ethernet
		/// </summary>
		PppEthernet = 51,

		/// <summary>
		/// Symantec Enterprise Firewall
		/// </summary>
		SymantecFirewall = 99,

		/// <summary>
		/// LLC/SNAP-encapsulated ATM
		/// </summary>
		AtmRfc1483 = 100,

		/// <summary>
		/// Raw IP
		/// </summary>
		Raw = 101,

		/// <summary>
		/// BSD/OS SLIP BPF header
		/// </summary>
		SlipBsdos = 102,

		/// <summary>
		/// BSD/OS PPP BPF header
		/// </summary>
		PppBsdos = 103,

		/// <summary>
		/// Cisco HDLC
		/// </summary>
		CiscoHdlc = 104,

		/// <summary>
		/// IEEE 802.11 (wireless)
		/// </summary>
		Ieee80211 = 105,

		/// <summary>
		/// Linux Classical IP over ATM
		/// </summary>
		AtmClip = 106,

		/// <summary>
		/// Frame Relay
		/// </summary>
		FrameRelay = 107,

		/// <summary>
		/// OpenBSD loopback
		/// </summary>
		Loop = 108,

		/// <summary>
		/// OpenBSD IPSEC enc
		/// </summary>
		ENC = 109,

		/// <summary>
		/// ATM LANE + 802.3 (Reserved for future use)
		/// </summary>
		Lane8023 = 110,

		/// <summary>
		/// NetBSD HIPPI (Reserved for future use)
		/// </summary>
		HIPPI = 111,

		/// <summary>
		/// NetBSD HDLC framing (Reserved for future use)
		/// </summary>
		HDLC = 112,

		/// <summary>
		/// Linux cooked socket capture
		/// </summary>
		LinuxSll = 113,

		/// <summary>
		/// Apple LocalTalk hardware
		/// </summary>
		LocalTalk = 114,

		/// <summary>
		/// Acorn Econet
		/// </summary>
		AcornEconet = 115,

		/// <summary>
		/// Reserved for use with OpenBSD ipfilter
		/// </summary>
		IpFilter = 116,

		/// <summary>
		/// OpenBSD DLT_PFLOG
		/// </summary>
		PfLog = 117,

		/// <summary>
		/// For Cisco-internal use
		/// </summary>
		CiscoIos = 118,

		/// <summary>
		/// 802.11+Prism II monitor mode
		/// </summary>
		PrismHeader = 119,

		/// <summary>
		/// FreeBSD Aironet driver stuff
		/// </summary>
		AironetHeader = 120,

		/// <summary>
		/// Reserved for Siemens HiPath HDLC
		/// </summary>
		HHDLC = 121,

		/// <summary>
		/// RFC 2625 IP-over-Fibre Channel
		/// </summary>
		IpOverFibre = 122,

		/// <summary>
		/// Solaris+SunATM
		/// </summary>
		SunAtm = 123,

		/// <summary>
		/// RapidIO - Reserved as per request from Kent Dahlgren <kent@praesum.com> for private use.
		/// </summary>
		RapidIo = 124,

		/// <summary>
		/// PCI Express - Reserved as per request from Kent Dahlgren <kent@praesum.com> for private use.
		/// </summary>
		PciExpress = 125,

		/// <summary>
		/// Xilinx Aurora link layer - Reserved as per request from Kent Dahlgren <kent@praesum.com> for private use.
		/// </summary>
		Aurora = 126,

		/// <summary>
		/// 802.11 plus BSD radio header
		/// </summary>
		Ieee80211Radio = 127,

		/// <summary>
		/// Tazmen Sniffer Protocol - Reserved for the TZSP encapsulation, as per request from Chris Waters <chris.waters@networkchemistry.com> TZSP is a generic encapsulation for any other link type, which includes a means to include meta-information with the packet, e.g. signal strength and channel for 802.11 packets.
		/// </summary>
		TZSP = 128,

		/// <summary>
		/// Linux-style headers
		/// </summary>
		ArcnetLinux = 129,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperMlPpp = 130,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperMlfr = 131,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperES = 132,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperGgsn = 133,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperMfr = 134,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperAtm2 = 135,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperServices = 136,

		/// <summary>
		/// Juniper-private data link type, as per request from Hannes Gredler <hannes@juniper.net>. The corresponding DLT_s are used for passing on chassis-internal metainformation such as QOS profiles, etc..
		/// </summary>
		JuniperAtm1 = 137,

		/// <summary>
		/// Apple IP-over-IEEE 1394 cooked header
		/// </summary>
		AppleIpOverIeee1394 = 138,

		/// <summary>
		/// ???
		/// </summary>
		Mtp2WithPhdr = 139,

		/// <summary>
		/// ???
		/// </summary>
		Mtp2 = 140,

		/// <summary>
		/// ???
		/// </summary>
		Mtp3 = 141,

		/// <summary>
		/// Signalling Connection Control Part (SCCP)
		/// </summary>
		SCCP = 142,

		/// <summary>
		/// DOCSIS MAC frames
		/// </summary>
		DOCSIS = 143,

		/// <summary>
		/// Linux-IrDA
		/// </summary>
		LinuxIrDA = 144,

		/// <summary>
		/// Reserved for IBM SP switch and IBM Next Federation switch.
		/// </summary>
		IbmSP = 145,

		/// <summary>
		/// Reserved for IBM SP switch and IBM Next Federation switch.
		/// </summary>
		IbmSN = 146,
	}
}
