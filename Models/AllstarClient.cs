using AsteriskAMIStream.Models;
using AsteriskAMIStream.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

public class AllstarClient
{
    private TcpClient tcpClient;
    private NetworkStream? stream;
    private StreamWriter? writer;
    private StreamReader? reader;
    private CancellationTokenSource cancellationTokenSource;
    private readonly string amiHost;
    private readonly int amiPort;
    private readonly string amiUsername;
    private readonly string amiPassword;
    private readonly string nodeNumber;
    private Task? readerTask;
    private BlockingCollection<string> responseQueue = new(); // Optional: use ConcurrentQueue if preferred
    private readonly SemaphoreSlim _streamLock = new(1, 1);
    private int _actionId = 0; // Action ID for the AMI commands

    public List<AllstarConnection> AllstarConnections { get; set; }

    public string ActionID => $"{Interlocked.Increment(ref _actionId)}"; // Thread-safe increment for action ID

    public AllstarClient(string amiHost, int amiPort, string amiUsername, string amiPassword, string nodeNumber)
    {
        this.amiHost = amiHost;
        this.amiPort = amiPort;
        this.amiUsername = amiUsername;
        this.amiPassword = amiPassword;
        this.nodeNumber = nodeNumber;

        tcpClient = new TcpClient();

        AllstarConnections = new List<AllstarConnection>();
        cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task ConnectAsync()
    {
        if (tcpClient.Connected)
            return;

        ConsoleHelper.Write($"Connecting to AMI server at {amiHost}:{amiPort}", "* ", ConsoleColor.DarkYellow);
        await tcpClient.ConnectAsync(amiHost, amiPort);

        if (!tcpClient.Connected)
        {
            ConsoleHelper.Write("Unable to connect to the AMI server.", "* ", ConsoleColor.Red);
            return;
        }

        ConsoleHelper.Write($"Connected to AMI server at {amiHost}:{amiPort}.", "* ", ConsoleColor.DarkYellow);

        stream = tcpClient.GetStream();
        writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };
        reader = new StreamReader(stream, Encoding.ASCII);

        // Write the login manually instead of using SendAsync()
        var loginCommand =
            $"ACTION: LOGIN\r\n" +
            $"USERNAME: {amiUsername}\r\n" +
            $"SECRET: {amiPassword}\r\n" +
            $"EVENTS: ON\r\n" +
            $"ActionID: {ActionID}\r\n\r\n"; // <- Note the double newline!


        ConsoleHelper.Write(loginCommand, ">> ", ConsoleColor.Blue);

        await writer.WriteLineAsync(loginCommand);
        await writer.FlushAsync();  // <-- critical flush

        ConsoleHelper.Write("Login command sent to AMI server.", "* ", ConsoleColor.DarkYellow);

        // Start reader loop after successful login
        readerTask = Task.Run(() => ReaderLoopAsync(cancellationTokenSource.Token));

    }

    private async Task SendAsync(string command)
    {
        await _streamLock.WaitAsync();

        try
        {
            if (tcpClient == null || !tcpClient.Connected)
            {
                ConsoleHelper.Write("TCP client is not connected. Attempting to connect...", "* ", ConsoleColor.DarkYellow);
                await ConnectAsync();
            }

            ConsoleHelper.Write(command, ">> ", ConsoleColor.Blue, ConsoleColor.Black);

            await writer!.WriteLineAsync("\r\n\r\n" + command);
            await writer.FlushAsync();
        }
        finally
        {
            _streamLock.Release();
        }
    }

    private async Task ReaderLoopAsync(CancellationToken cancellationToken)
    {
        if (reader == null)
        {
            ConsoleHelper.Write("StreamReader is null. Cannot start reading responses.", "* ", ConsoleColor.Red);
            return;
        }

        try
        {
            StringBuilder messageBuilder = new();
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();

                if (line == null)
                {
                    ConsoleHelper.Write("Stream closed by server.", "* ", ConsoleColor.Red);
                    break;
                }
                else if (line == string.Empty)
                {
                    // Message boundary (blank line)
                    string fullMessage = messageBuilder.ToString();
                    messageBuilder.Clear();

                    // Optionally queue or dispatch the message
                    responseQueue.Add(fullMessage);

                    ParseResponse(fullMessage);
                }
                else
                {
                    ConsoleHelper.Write(line, "", ConsoleColor.Green);
                    messageBuilder.AppendLine(line);
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleHelper.Write($"Reader loop exception: {ex.GetType().Name}: {ex.Message}", "* ", ConsoleColor.Red);
        }
    }

    public async Task GetNodeInfoAsync(string nodeNumber)
    {
        var rptCommand =
            $"ACTION: RptStatus\r\n" +
            $"COMMAND: XStat\r\n" +
            $"NODE: {nodeNumber}\r\n" + 
            $"ActionID: {ActionID}\r\n\r\n";

        await SendAsync(rptCommand);
    }

    private void ParseResponse(string rawMessage)
    {
        // Parse the raw message into a structured format if needed
        if (string.IsNullOrEmpty(rawMessage))
        {
            return;
        }

        var reWelcome = new Regex(@"^Asterisk Call Manager/1.0$", RegexOptions.Multiline);
        var reError = new Regex(@"^Response: Error\nMessage: (.*)$", RegexOptions.Multiline);
        var reSuccess = new Regex(@"^Response: Success\n(?:ActionID: .*\n)?Message: (.*)$", RegexOptions.Multiline);
        var reXstat = new Regex(@"^(?:ActionID: (.*)\n)?Response: Success\nNode: (.*)\n(?:Conn: (\S*)\s*(\S*)\s*(\S*)\s*(\S*)\s*(\S*)\s*(\S*)\s*\n)*(?:LinkedNodes: (.*))*\n(?:Var: RPT_TXKEYED=(.*)\n)*(?:Var: RPT_NUMLINKS=(.*)\n)?(?:Var: RPT_LINKS=(.*)\n)?(?:Var: RPT_NUMALINKS=(.*)\n)?(?:Var: RPT_ALINKS=(.*)\n)?(?:Var: RPT_RXKEYED=(.*)\n)?(?:Var: RPT_AUTOPATCHUP=(.*)\n)?(?:Var: RPT_ETXKEYED=(.*)\n)?(?:Var: TRANSFERCAPABILITY=(.*)\n)?(?:parrot_ena: (.*)\n)?(?:sys_ena: (.*)\n)?(?:tot_ena: (.*)\n)?(?:link_ena: (.*)\n)?(?:patch_ena: (.*)\n)?(?:patch_state: (.*)\n)?(?:sch_ena: (.*)\n)?(?:user_funs: (.*)\n)?(?:tail_type: (.*)\n)?(?:iconns: (.*)\n)?(?:tot_state: (.*)\n)?(?:ider_state: (.*)\n)?(?:tel_mode: (.*)\n)?", RegexOptions.Multiline);
        var reRptLinks = new Regex(@"^Event: (?:RPT_ALINKS|RPT_NUMALINKS|RPT_LINKS|RPT_NUMLINKS|RPT_TXKEYED)\n(?:Privilege: (.*)\n)(?:Node: (.*)\n)(?:Channel: (.*)\n)(?:EventValue: (.*)\n)(?:LastKeyedTime: (.*)\n)\n(?:LastTxKeyedTime: (.*)\n)", RegexOptions.Multiline);
        var reNewChannel = new Regex(@"^Event: Newchannel\n(?:Privilege: (.*)\n)(?:Channel: (.*)\n)(?:State: (.*)\n)(?:CallerIDNum: (.*)\n)(?:CallerIDName: (.*)\n)(?:Uniqueid: (.*)\n)", RegexOptions.Multiline);
        var reHangup = new Regex(@"^Event: Hangup\n(?:Privilege: (.*)\n)(?:Channel: (.*)\n)(?:Uniqueid: (.*)\n)(?:Cause: (.*)\n)(?:Cause-txt: (.*)\n)", RegexOptions.Multiline);
        List<Regex> regexList = new List<Regex>
        {
            reWelcome,
            reError,
            reSuccess,
            reXstat,
            reRptLinks,
            reNewChannel,
            reHangup
        };

        
        foreach (var regex in regexList)
        {
            var match = regex.Match(rawMessage);
            if (match.Success)
            {
                // Handle the matched response
                switch (regex)
                {
                    case var _ when regex == reWelcome:
                        ConsoleHelper.Write(rawMessage, "", ConsoleColor.Green);
                        break;
                    case var _ when regex == reError:
                        ConsoleHelper.Write($"Error: {match.Groups[1].Value}", "", ConsoleColor.Red);
                        break;
                    case var _ when regex == reSuccess:
                        ConsoleHelper.Write($"Success: {match.Groups[1].Value}", "* ", ConsoleColor.Green);
                        break;
                    case var _ when regex == reXstat:
                        var nodeNumber = match.Groups[2].Value;
                        var connection = AllstarConnection.FindOrCreate(nodeNumber, AllstarConnections);

                        //connection.IpAddress = value.Substring(10, 20).Trim();
                        //connection.SomeNumber = value.Substring(30, 12).Trim();
                        //connection.Direction = value.Substring(42, 11).Trim();
                        //connection.TimeSpanConnected = value.Substring(53, 20).Trim();
                        //connection.Status = value.Substring(73, 20).Trim();
                        //connection.Type = "Direct";
                        break;
                    case var _ when regex == reRptLinks:
                        // Handle RPT_LINKS event
                        break;
                    case var _ when regex == reNewChannel:
                        // Handle Newchannel event
                        break;
                    case var _ when regex == reHangup:
                        // Handle Hangup event
                        break;
                }
            }
        }

        //var regex = new Regex(@"(?<=^|\r\n)(Response|ActionID|Message|Node|Conn|LinkedNodes|RPT_LINKS):\s*(.*?)\r?\n", RegexOptions.Multiline);
        //foreach (Match match in regex.Matches(rawMessage))
        //{
        //    var key = match.Groups[1].Value;
        //    var value = match.Groups[2].Value;

        //    switch (key)
        //    {
        //        case "Conn":
        //            try
        //            {
        //                // Check to see if this is a SawStat result
        //                string sawStatPattern = @"^(\d+)\s(\d+)\s(\d+)\s(\d+)$";
        //                var sawStatMatch = Regex.Match(value, sawStatPattern);
        //                if (sawStatMatch.Success) // SawStat result
        //                {
        //                    // Parse as SawStat result
        //                    var connection = AllstarConnection.FindOrCreate(sawStatMatch.Groups[1].Value, AllstarConnections);
        //                    connection.Transmitting = sawStatMatch.Groups[2].Value == "1";
        //                    connection.TimeSinceTransmit = sawStatMatch.Groups[3].Value;
        //                    connection.Type = "Direct";
        //                }
        //                else // XSTAT result
        //                {

        //                }
        //            }
        //            catch (Exception)
        //            {
        //                ConsoleHelper.Write("Error parsing connection data:\r\n" + value, "** ", ConsoleColor.Red);
        //            }

        //            break;
        //        case "LinkedNodes":
        //            var arrLinkedNodes = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        //            foreach (var node in arrLinkedNodes)
        //            {
        //                var connection = AllstarConnection.FindOrCreate(node.Trim().Substring(1), AllstarConnections);
        //                connection.Node = node.Trim().Substring(1);
        //                connection.Type = "Linked";
        //                connection.Status = node.Trim().Substring(0, 1) == "R" ? "Monitoring" : "Established";
        //            }
        //            break;
        //    }
        //}
    }

    public void ClearExpiredConnections(TimeSpan expirationTime)
    {
        var expiredConnections = AllstarConnections.Where(c => DateTime.UtcNow - c.LastHeardUtc > expirationTime).ToList();
        foreach (var connection in expiredConnections)
        {
            AllstarConnections.Remove(connection);
        }
    }
}

public class AllstarConnection
{
    public string Node { get; set; }
    public string? IpAddress { get; set; }
    public string? SomeNumber { get; set; }
    public string? Direction { get; set; }
    public string? TimeSpanConnected { get; set; }
    public DateTime LastHeardUtc { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? CallSign { get; set; }
    public string? Location { get; set; }
    public string? Info1 { get; set; }
    public string? Info2 { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public bool Transmitting { get; set; }
    public string? TimeSinceTransmit { get; set; }

    public void LoadMetadata()
    {
        int nodeNumber = 0;

        string callsignPattern = @"^[a-zA-Z0-9]{1,3}[0-9][a-zA-Z0-9]{0,3}[a-zA-Z]$";
        var callsignMatch = Regex.Match(Node, callsignPattern);

        if (callsignMatch.Success)
        {
            Info2 = "RepeaterPhone / Direct connection";
        }
        else if (int.TryParse(Node, out nodeNumber))
        {
            if (nodeNumber > 3000000)
            {
                // Echolink
                Info2 = "Echolink";
            }
            else if (nodeNumber < 2000)
            {
                Info2 = "Private node";
            }
            else
            {
                // Fetch metadata for the node
                var metadata = MetadataService.GetNodeMetadata(Node);
                if (metadata != null)
                {
                    CallSign = metadata.CallSign;
                    Location = metadata.Location;
                    Info1 = metadata.Info1;
                    Info2 = metadata.Info2;
                    Latitude = metadata.Latitude.ToString();
                    Longitude = metadata.Longitude.ToString();
                }
            }
        }
    }

    public static AllstarConnection FindOrCreate(string node, List<AllstarConnection> connections)
    {
        var connection = connections.FirstOrDefault(conn => conn.Node == node);

        if (connection == default) // This connection does not exist
        {
            connection = new AllstarConnection
            {
                Node = node
            };
            connection.LoadMetadata(); // Load metadata for the new connection
            connections.Add(connection);
        }

        connection.LastHeardUtc = DateTime.UtcNow; // Update last heard time

        return connection;
    }
}