namespace AsteriskDataStream.Models.AllstarLinkStatsApi
{
    public class RootNode
    {
        public Stats stats { get; set; }
        public Node node { get; set; }
        public List<object> keyups { get; set; }
        public double time { get; set; }
    }

    public class Data
    {
        public string apprptuptime { get; set; }
        public string totalexecdcommands { get; set; }
        public string totalkeyups { get; set; }
        public string totaltxtime { get; set; }
        public string apprptvers { get; set; }
        public string timeouts { get; set; }
        public List<string> links { get; set; }
        public bool keyed { get; set; }
        public string time { get; set; }
        public string seqno { get; set; }
        public string nodes { get; set; }
        public string totalkerchunks { get; set; }
        public string keytime { get; set; }
        public List<Node> linkedNodes { get; set; }
    }

    public class Node
    {
        public int Node_ID { get; set; }
        public string User_ID { get; set; }
        public string Status { get; set; }
        public string name { get; set; }
        public string ipaddr { get; set; }
        public int port { get; set; }
        public int regseconds { get; set; }
        public string iptime { get; set; }
        public string node_frequency { get; set; }
        public string node_tone { get; set; }
        public bool node_remotebase { get; set; }
        public string node_freqagile { get; set; }
        public string callsign { get; set; }
        public string access_reverseautopatch { get; set; }
        public string access_telephoneportal { get; set; }
        public string access_webtransceiver { get; set; }
        public string access_functionlist { get; set; }
        public string reghostname { get; set; }
        public string is_nnx { get; set; }
        public Server server { get; set; }
        public Data data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class Server
    {
        public int Server_ID { get; set; }
        public string User_ID { get; set; }
        public string Server_Name { get; set; }
        public string Affiliation { get; set; }
        public string SiteName { get; set; }
        public string Logitude { get; set; }
        public string Latitude { get; set; }
        public string Location { get; set; }
        public string TimeZone { get; set; }
        public int udpport { get; set; }
        public object proxy_ip { get; set; }
    }

    public class Stats
    {
        public int id { get; set; }
        public int node { get; set; }
        public Data data { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public Node user_node { get; set; }
    }
}
